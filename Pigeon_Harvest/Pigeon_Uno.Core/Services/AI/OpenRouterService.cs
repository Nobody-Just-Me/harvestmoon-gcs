using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Pigeon_Uno.Core.Models.AI;

namespace Pigeon_Uno.Core.Services.AI;

/// <summary>
/// OpenRouter API client implementing ILLMService interface.
/// Features: circuit breaker, retry with exponential backoff, response caching, fallback to free model.
/// </summary>
#if !__WASM__
public class OpenRouterService : ILLMService
#else
internal class OpenRouterService : ILLMService
#endif
{
    private const string DefaultBaseUrl = "https://openrouter.ai/api/v1";
    private const string DefaultFallbackModel = "openrouter/free";

    private readonly HttpClient _httpClient;
    private readonly AISettings _settings;
    private readonly Func<string> _apiKeyResolver;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    // Circuit breaker state
    private int _consecutiveFailures;
    private DateTime? _circuitOpenUntil;
    private CircuitState _circuitState = CircuitState.Closed;
    private DateTime? _lastSuccessAt;
    private DateTime? _lastFailureAt;
    private DateTime? _lastRequestAt;
    private int _lastLatencyMs;
    private string _lastError = string.Empty;
    private long _failedRequests;
    private long _fallbackUsageCount;
    private readonly object _stateLock = new();

    // Retry configuration
    private const int MaxRetries = 3;
    private const int BaseDelayMs = 1000;

    // Explicit rate limiter (per-process) for outbound OpenRouter requests.
    private const int MaxRequestsPerMinute = 60;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);
    private readonly ConcurrentQueue<DateTime> _requestWindow = new();
    private readonly SemaphoreSlim _rateLimitGate = new(1, 1);

    // Cache: key → (response, expiry)
    private readonly ConcurrentDictionary<string, (string Response, DateTime Expiry)> _cache = new();
    private long _cacheHits;
    private long _cacheMisses;

    /// <summary>
    /// Creates a new OpenRouterService with the specified settings.
    /// </summary>
    /// <param name="settings">AI settings including API key, base URL, and model configuration</param>
    public OpenRouterService(
        AISettings settings,
        Func<string>? apiKeyResolver = null,
        string? baseUrl = null,
        HttpClient? httpClient = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _apiKeyResolver = apiKeyResolver ?? ResolveDefaultApiKey;
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? ResolveOpenRouterBaseUrl(settings)
            : baseUrl.TrimEnd('/');

        _httpClient = httpClient ?? CreateDefaultHttpClient();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Gets whether the service is available (has API key and circuit breaker is closed).
    /// </summary>
    public bool IsAvailable
    {
        get
        {
            lock (_stateLock)
            {
                if (_circuitState == CircuitState.Open && _circuitOpenUntil.HasValue)
                {
                    if (DateTime.UtcNow >= _circuitOpenUntil.Value)
                    {
                        _circuitState = CircuitState.Closed;
                        _consecutiveFailures = 0;
                    }
                }
                return !string.IsNullOrEmpty(GetApiKey()) && _circuitState != CircuitState.Open;
            }
        }
    }

    /// <summary>
    /// Gets the name of the LLM provider.
    /// </summary>
    public string ProviderName => "OpenRouter";

    /// <summary>
    /// Generates a text response from the LLM using the specified role to select the model.
    /// Implements retry with exponential backoff and fallback to free model on failure.
    /// </summary>
    /// <param name="prompt">The prompt to send to the LLM</param>
    /// <param name="role">The role determining which model to use</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>LLM result containing the generated text or error</returns>
    public async Task<LLMResult> GenerateAsync(
        string prompt,
        LLMRole role = LLMRole.TelemetryAnalysis,
        CancellationToken ct = default)
    {
        var model = GetModelForRole(role);
        return await ExecuteWithFallbackAsync(prompt, model, role, ct);
    }

    /// <summary>
    /// Generates a structured response (JSON) from the LLM.
    /// Deserializes the response into the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response into</typeparam>
    /// <param name="prompt">The prompt to send to the LLM</param>
    /// <param name="role">The role determining which model to use</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The deserialized response or null on failure</returns>
    public async Task<T?> GenerateStructuredAsync<T>(
        string prompt,
        LLMRole role = LLMRole.TelemetryAnalysis,
        CancellationToken ct = default) where T : class
    {
        var model = GetModelForRole(role);
        var result = await ExecuteWithFallbackAsync(prompt, model, role, ct, isStructured: true);

        if (!result.Success || string.IsNullOrEmpty(result.Text))
            return null;

        // Strip markdown code blocks if present
        var jsonText = NormalizeStructuredJson(result.Text);

        try
        {
            return JsonSerializer.Deserialize<T>(jsonText, _jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Tests connectivity to the OpenRouter API using the free model.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the connection is successful</returns>
    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(GetApiKey()))
        {
            lock (_stateLock)
            {
                _lastError = "API key OpenRouter kosong.";
            }
            return false;
        }

        try
        {
            var sw = Stopwatch.StartNew();
            var request = new OpenRouterRequest
            {
                Model = ResolveFallbackModel(),
                Messages = new[]
                {
                    new Message { Role = "user", Content = "Hi" }
                },
                MaxTokens = 5
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri())
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(request, _jsonOptions),
                    Encoding.UTF8,
                    "application/json")
            };
            ApplyOpenRouterHeaders(httpRequest);

            lock (_stateLock)
            {
                _lastRequestAt = DateTime.UtcNow;
            }

            await WaitForRateLimitSlotAsync(ct);
            using var response = await _httpClient.SendAsync(httpRequest, ct);
            sw.Stop();

            lock (_stateLock)
            {
                _lastLatencyMs = (int)sw.ElapsedMilliseconds;
            }

            if (response.IsSuccessStatusCode)
            {
                RecordSuccess();
                return true;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var mappedError = MapHttpError(response.StatusCode, content);
            RecordFailure(mappedError);
            Interlocked.Increment(ref _failedRequests);
            return false;
        }
        catch (OperationCanceledException ex)
        {
            RecordFailure(ex.Message);
            Interlocked.Increment(ref _failedRequests);
            return false;
        }
        catch (Exception ex)
        {
            RecordFailure(ex.Message);
            Interlocked.Increment(ref _failedRequests);
            return false;
        }
    }

    /// <summary>
    /// Returns the current health status of the LLM service including
    /// circuit breaker state, cache statistics, and failure counts.
    /// </summary>
    /// <returns>LLMHealthStatus with current service health information</returns>
    public LLMHealthStatus GetHealthStatus()
    {
        lock (_stateLock)
        {
            var totalCacheOps = _cacheHits + _cacheMisses;
            var hitRate = totalCacheOps > 0 ? (double)_cacheHits / totalCacheOps : 0.0;
            var apiKeyConfigured = !string.IsNullOrWhiteSpace(GetApiKey());

            return new LLMHealthStatus
            {
                IsConnected = apiKeyConfigured && _circuitState != CircuitState.Open,
                PrimaryProvider = ProviderName,
                FallbackProvider = ProviderName,
                ActiveProvider = ProviderName,
                ActiveModel = _settings.Models.TelemetryAnalysis,
                PrimaryModel = _settings.Models.TelemetryAnalysis,
                FallbackModel = _settings.Models.Fallback,
                CircuitOpen = _circuitState == CircuitState.Open,
                ConsecutiveFailures = _consecutiveFailures,
                LastSuccessAt = _lastSuccessAt,
                LastFailureAt = _lastFailureAt,
                LastRequestAt = _lastRequestAt,
                CircuitOpenUntil = _circuitOpenUntil,
                CacheHitRate = hitRate,
                TotalRequests = _cacheHits + _cacheMisses,
                FailedRequests = _failedRequests,
                FallbackUsageCount = _fallbackUsageCount,
                PrimaryApiKeyConfigured = apiKeyConfigured,
                FallbackApiKeyConfigured = apiKeyConfigured,
                FallbackAvailable = apiKeyConfigured,
                LastLatencyMs = _lastLatencyMs,
                LastError = _lastError
            };
        }
    }

    private async Task<LLMResult> ExecuteWithFallbackAsync(
        string prompt,
        string model,
        LLMRole role,
        CancellationToken ct,
        bool isStructured = false)
    {
        if (!IsAvailable)
        {
            lock (_stateLock)
            {
                _lastError = "API key OpenRouter kosong atau circuit breaker sedang terbuka.";
            }
            return LLMResult.Fail("OpenRouter unavailable: API key kosong atau circuit breaker sedang terbuka.", model);
        }

        var cacheKey = BuildCacheKey(role, model, prompt, isStructured);

        if (TryGetCachedResponse(cacheKey, model, fallback: false, out var cachedResult))
        {
            return cachedResult;
        }

        Interlocked.Increment(ref _cacheMisses);

        // Try primary model
        var result = await ExecuteWithRetryAsync(prompt, model, role, ct, cacheKey, isStructured);

        // If failed and not already using fallback model, try fallback
        var fallbackModel = ResolveFallbackModel();
        if (!result.Success && !string.Equals(model, fallbackModel, StringComparison.Ordinal))
        {
            var fallbackCacheKey = BuildCacheKey(role, fallbackModel, prompt, isStructured);

            if (TryGetCachedResponse(fallbackCacheKey, fallbackModel, fallback: true, out var fallbackCachedResult))
            {
                Interlocked.Increment(ref _fallbackUsageCount);
                return fallbackCachedResult;
            }

            Interlocked.Increment(ref _cacheMisses);
            var fallbackResult = await ExecuteWithRetryAsync(prompt, fallbackModel, role, ct, fallbackCacheKey, isStructured);
            if (fallbackResult.Success)
            {
                Interlocked.Increment(ref _fallbackUsageCount);
                fallbackResult = LLMResult.Ok(
                    fallbackResult.Text,
                    fallbackResult.ModelUsed,
                    fallbackResult.WasCached,
                    fallback: true,
                    latencyMs: fallbackResult.LatencyMs);
                return fallbackResult;
            }
        }

        return result;
    }

    private async Task<LLMResult> ExecuteWithRetryAsync(
        string prompt,
        string model,
        LLMRole role,
        CancellationToken ct,
        string cacheKey,
        bool isStructured)
    {
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                lock (_stateLock)
                {
                    _lastRequestAt = DateTime.UtcNow;
                }

                var request = new OpenRouterRequest
                {
                    Model = model,
                    Messages = new[]
                    {
                        new Message { Role = "user", Content = prompt }
                    },
                    MaxTokens = 2048,
                    Temperature = 0.7
                };

                if (isStructured)
                {
                    request.ResponseFormat = new ResponseFormat
                    {
                        Type = "json_object"
                    };
                }

                var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri())
                {
                    Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
                };
                ApplyOpenRouterHeaders(httpRequest);

                await WaitForRateLimitSlotAsync(ct);
                using var response = await _httpClient.SendAsync(httpRequest, ct);
                sw.Stop();
                var latencyMs = (int)sw.ElapsedMilliseconds;

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(ct);
                    var completion = JsonSerializer.Deserialize<OpenRouterResponse>(content, _jsonOptions);
                    var text = completion?.Choices?.Length > 0
                        ? completion.Choices[0].Message.Content
                        : null;

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        RecordSuccess();
                        lock (_stateLock)
                        {
                            _lastLatencyMs = latencyMs;
                        }

                        SetCachedResponse(cacheKey, text);

                        return LLMResult.Ok(text, model, latencyMs: latencyMs);
                    }

                    var emptyResponseError = "OpenRouter mengembalikan respons kosong.";
                    if (attempt < MaxRetries)
                    {
                        await Task.Delay(GetRetryDelay(response, attempt), ct);
                        continue;
                    }

                    RecordFailure(emptyResponseError);
                    Interlocked.Increment(ref _failedRequests);
                    return LLMResult.Fail(emptyResponseError, model);
                }

                var errorContent = await response.Content.ReadAsStringAsync(ct);
                var mappedError = MapHttpError(response.StatusCode, errorContent);
                lock (_stateLock)
                {
                    _lastLatencyMs = latencyMs;
                    _lastError = mappedError;
                }

                if (ShouldRetry(response) && attempt < MaxRetries)
                {
                    await Task.Delay(GetRetryDelay(response, attempt), ct);
                    continue;
                }

                // If last attempt, return failure
                RecordFailure(mappedError);
                Interlocked.Increment(ref _failedRequests);
                return LLMResult.Fail(mappedError, model);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex) when (attempt < MaxRetries)
            {
                lock (_stateLock)
                {
                    _lastError = ex.Message;
                }
                var delay = BaseDelayMs * (1 << attempt);
                await Task.Delay(delay, ct);
            }
            catch (Exception) when (attempt < MaxRetries)
            {
                var delay = BaseDelayMs * (1 << attempt); // exponential backoff
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                RecordFailure(ex.Message);
                Interlocked.Increment(ref _failedRequests);
                return LLMResult.Fail($"Request error: {ex.Message}", model);
            }
        }

        Interlocked.Increment(ref _failedRequests);
        RecordFailure("Max retries exceeded");
        return LLMResult.Fail("Max retries exceeded", model);
    }

    private static string BuildCacheKey(LLMRole role, string model, string prompt, bool isStructured)
    {
        var payload = $"{role}|{model}|{(isStructured ? "json" : "text")}|{prompt}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    private bool TryGetCachedResponse(string cacheKey, string model, bool fallback, out LLMResult result)
    {
        result = default!;
        if (!_settings.Cache.Enabled || !_cache.TryGetValue(cacheKey, out var cached))
        {
            return false;
        }

        if (DateTime.UtcNow < cached.Expiry)
        {
            Interlocked.Increment(ref _cacheHits);
            result = LLMResult.Ok(cached.Response, model, cached: true, fallback: fallback);
            return true;
        }

        _cache.TryRemove(cacheKey, out _);
        return false;
    }

    private void SetCachedResponse(string cacheKey, string response)
    {
        if (!_settings.Cache.Enabled || _settings.Cache.TTLSeconds <= 0)
        {
            return;
        }

        PruneExpiredCacheEntries();

        var maxEntries = Math.Clamp(_settings.Cache.MaxSizeMB * 32, 32, 4096);
        if (_cache.Count >= maxEntries)
        {
            PruneExpiredCacheEntries(forceOneRemoval: true);
        }

        var expiry = DateTime.UtcNow.AddSeconds(_settings.Cache.TTLSeconds);
        _cache[cacheKey] = (response, expiry);
    }

    private void PruneExpiredCacheEntries(bool forceOneRemoval = false)
    {
        var now = DateTime.UtcNow;
        var removedOne = false;
        foreach (var item in _cache)
        {
            if (now >= item.Value.Expiry || (forceOneRemoval && !removedOne))
            {
                _cache.TryRemove(item.Key, out _);
                removedOne = true;
            }
        }
    }

    private static bool ShouldRetry(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;
        return statusCode == 408 || statusCode == 429 || statusCode >= 500;
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                return delay;
            }
        }

        return TimeSpan.FromMilliseconds(BaseDelayMs * (1 << attempt));
    }

    private async Task WaitForRateLimitSlotAsync(CancellationToken ct)
    {
        while (true)
        {
            TimeSpan delay;
            await _rateLimitGate.WaitAsync(ct);
            try
            {
                var now = DateTime.UtcNow;
                while (_requestWindow.TryPeek(out var ts) && (now - ts) >= RateLimitWindow)
                {
                    _requestWindow.TryDequeue(out _);
                }

                if (_requestWindow.Count < MaxRequestsPerMinute)
                {
                    _requestWindow.Enqueue(now);
                    return;
                }

                if (_requestWindow.TryPeek(out var oldest))
                {
                    delay = RateLimitWindow - (now - oldest);
                    if (delay < TimeSpan.FromMilliseconds(50))
                    {
                        delay = TimeSpan.FromMilliseconds(50);
                    }
                }
                else
                {
                    delay = TimeSpan.FromMilliseconds(100);
                }
            }
            finally
            {
                _rateLimitGate.Release();
            }

            await Task.Delay(delay, ct);
        }
    }

    private Uri BuildChatCompletionsUri()
    {
        return new Uri(new Uri(_baseUrl.TrimEnd('/') + "/"), "chat/completions");
    }

    private void ApplyOpenRouterHeaders(HttpRequestMessage request)
    {
        var apiKey = GetApiKey();
        request.Headers.Authorization = string.IsNullOrWhiteSpace(apiKey)
            ? null
            : new AuthenticationHeaderValue("Bearer", apiKey);

        if (!string.IsNullOrWhiteSpace(_settings.SiteUrl))
        {
            request.Headers.TryAddWithoutValidation("HTTP-Referer", _settings.SiteUrl);
        }

        if (!string.IsNullOrWhiteSpace(_settings.SiteName))
        {
            request.Headers.TryAddWithoutValidation("X-OpenRouter-Title", _settings.SiteName);
        }
    }

    private static string MapHttpError(System.Net.HttpStatusCode statusCode, string? responseContent)
    {
        var details = string.IsNullOrWhiteSpace(responseContent)
            ? string.Empty
            : $" Detail: {TrimErrorDetails(responseContent)}";

        return statusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized =>
                "OpenRouter menolak autentikasi (401). Periksa API key utama." + details,
            System.Net.HttpStatusCode.PaymentRequired =>
                "OpenRouter quota/kredit habis (402). Cek billing atau ganti model." + details,
            System.Net.HttpStatusCode.Forbidden =>
                "OpenRouter request ditolak (403). Periksa izin key atau referer." + details,
            System.Net.HttpStatusCode.NotFound =>
                "Model OpenRouter tidak ditemukan (404). Periksa ID model di AI Settings." + details,
            System.Net.HttpStatusCode.TooManyRequests =>
                "OpenRouter rate limit tercapai (429). Coba lagi beberapa saat." + details,
            _ =>
                $"HTTP {(int)statusCode} ({statusCode}) dari OpenRouter." + details
        };
    }

    private static string TrimErrorDetails(string responseContent)
    {
        var normalized = responseContent.Trim();
        const int maxLength = 600;
        return normalized.Length <= maxLength
            ? normalized
            : normalized.Substring(0, maxLength) + "...";
    }

    private string GetApiKey()
    {
        try
        {
            return _apiKeyResolver.Invoke()?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ResolveDefaultApiKey()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return _settings.ApiKey;
        }

        return Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? string.Empty;
    }

    private static string ResolveOpenRouterBaseUrl(AISettings settings)
    {
        if (string.Equals(settings.Provider, "OpenRouter", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return settings.BaseUrl.TrimEnd('/');
        }

        return DefaultBaseUrl;
    }

    private string GetModelForRole(LLMRole role)
    {
        var model = role switch
        {
            LLMRole.TelemetryAnalysis => _settings.Models.TelemetryAnalysis,
            LLMRole.AnomalyDetection => _settings.Models.AnomalyDetection,
            LLMRole.NaturalLanguageChat => _settings.Models.NaturalLanguageChat,
            LLMRole.MaintenancePrediction => _settings.Models.MaintenancePrediction,
            LLMRole.PerformanceScoring => _settings.Models.PerformanceScoring,
            LLMRole.BatteryPrediction => _settings.Models.BatteryPrediction,
            LLMRole.FlightSessionSummary => _settings.Models.FlightSessionSummary,
            LLMRole.VoiceIntent => _settings.Models.VoiceIntent,
            LLMRole.Fallback => _settings.Models.Fallback,
            _ => _settings.Models.TelemetryAnalysis
        };

        return string.IsNullOrWhiteSpace(model)
            ? ResolveFallbackModel()
            : model;
    }

    private string ResolveFallbackModel()
    {
        return string.IsNullOrWhiteSpace(_settings.Models.Fallback)
            ? DefaultFallbackModel
            : _settings.Models.Fallback;
    }

    private void RecordSuccess()
    {
        lock (_stateLock)
        {
            _consecutiveFailures = 0;
            _circuitState = CircuitState.Closed;
            _circuitOpenUntil = null;
            _lastSuccessAt = DateTime.UtcNow;
            _lastError = string.Empty;
        }
    }

    private void RecordFailure(string? error = null)
    {
        lock (_stateLock)
        {
            _consecutiveFailures++;
            _lastFailureAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(error))
            {
                _lastError = error;
            }

            if (_consecutiveFailures >= 5)
            {
                _circuitState = CircuitState.Open;
                _circuitOpenUntil = DateTime.UtcNow.AddSeconds(30);
            }
        }
    }

    private static string StripMarkdownCodeBlocks(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Remove leading/trailing markdown code block markers
        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            text = text.Substring(7);
        else if (text.StartsWith("```"))
            text = text.Substring(3);

        if (text.EndsWith("```"))
            text = text.Substring(0, text.Length - 3);

        return text.Trim();
    }

    private static string NormalizeStructuredJson(string text)
    {
        var stripped = StripMarkdownCodeBlocks(text);
        if (string.IsNullOrWhiteSpace(stripped))
        {
            return stripped;
        }

        if (IsValidJson(stripped))
        {
            return stripped;
        }

        var objectStart = stripped.IndexOf('{');
        var objectEnd = stripped.LastIndexOf('}');
        if (objectStart >= 0 && objectEnd > objectStart)
        {
            var objectCandidate = stripped.Substring(objectStart, objectEnd - objectStart + 1);
            if (IsValidJson(objectCandidate))
            {
                return objectCandidate;
            }
        }

        var arrayStart = stripped.IndexOf('[');
        var arrayEnd = stripped.LastIndexOf(']');
        if (arrayStart >= 0 && arrayEnd > arrayStart)
        {
            var arrayCandidate = stripped.Substring(arrayStart, arrayEnd - arrayStart + 1);
            if (IsValidJson(arrayCandidate))
            {
                return arrayCandidate;
            }
        }

        return stripped;
    }

    private static bool IsValidJson(string payload)
    {
        try
        {
            using var _ = JsonDocument.Parse(payload);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Request/Response models for OpenRouter API

    private class OpenRouterRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public Message[] Messages { get; set; } = Array.Empty<Message>();

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("response_format")]
        public ResponseFormat? ResponseFormat { get; set; }
    }

    private class Message
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private class ResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("json_schema")]
        public JsonSchema? JsonSchema { get; set; }
    }

    private class JsonSchema
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("schema")]
        public string Schema { get; set; } = string.Empty;
    }

    private class OpenRouterResponse
    {
        [JsonPropertyName("choices")]
        public Choice[] Choices { get; set; } = Array.Empty<Choice>();
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public ResponseMessage Message { get; set; } = new();
    }

    private class ResponseMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
