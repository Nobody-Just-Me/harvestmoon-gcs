using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models.AI;

namespace HarvestmoonGCS.Core.Services.AI;

/// <summary>
/// Base service for providers exposing OpenAI-compatible chat completions API.
/// </summary>
public abstract class OpenAICompatibleLLMService : ILLMService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly AISettings _settings;
    private readonly Func<string> _apiKeyResolver;
    private readonly string _providerName;
    private readonly string _baseUrl;
    private readonly string _defaultModel;
    private DateTime? _lastSuccessAt;
    private DateTime? _lastFailureAt;
    private DateTime? _lastRequestAt;
    private string _lastError = string.Empty;
    private int _lastLatencyMs;
    private long _failedRequests;

    private const int MaxRetries = 2;
    private const int MaxRequestsPerMinute = 60;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);
    private readonly ConcurrentQueue<DateTime> _requestWindow = new();
    private readonly SemaphoreSlim _rateLimitGate = new(1, 1);

    protected OpenAICompatibleLLMService(
        HttpClient httpClient,
        AISettings settings,
        string providerName,
        Func<string> apiKeyResolver,
        string baseUrl,
        string defaultModel)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _providerName = providerName ?? "OpenAI-Compatible";
        _apiKeyResolver = apiKeyResolver ?? (() => string.Empty);
        _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
        _defaultModel = defaultModel ?? "gpt-4o-mini";
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(GetApiKey());
    public string ProviderName => _providerName;

    public async Task<LLMResult> GenerateAsync(
        string prompt,
        LLMRole role = LLMRole.TelemetryAnalysis,
        CancellationToken ct = default)
    {
        return await ExecuteAsync(prompt, role, isStructured: false, ct);
    }

    public async Task<T?> GenerateStructuredAsync<T>(
        string prompt,
        LLMRole role = LLMRole.TelemetryAnalysis,
        CancellationToken ct = default) where T : class
    {
        var result = await ExecuteAsync(prompt, role, isStructured: true, ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(StripMarkdownCodeBlocks(result.Text), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        var result = await GenerateAsync("respond with ok", LLMRole.Fallback, ct);
        return result.Success;
    }

    public LLMHealthStatus GetHealthStatus()
    {
        var apiKeyConfigured = !string.IsNullOrWhiteSpace(GetApiKey());
        return new LLMHealthStatus
        {
            IsConnected = IsAvailable,
            PrimaryProvider = ProviderName,
            FallbackProvider = ProviderName,
            ActiveProvider = ProviderName,
            ActiveModel = ResolveModel(LLMRole.TelemetryAnalysis),
            PrimaryModel = ResolveModel(LLMRole.TelemetryAnalysis),
            FallbackModel = ResolveModel(LLMRole.Fallback),
            CircuitOpen = false,
            LastSuccessAt = _lastSuccessAt,
            LastFailureAt = _lastFailureAt,
            LastRequestAt = _lastRequestAt,
            FailedRequests = _failedRequests,
            PrimaryApiKeyConfigured = apiKeyConfigured,
            FallbackApiKeyConfigured = apiKeyConfigured,
            FallbackAvailable = apiKeyConfigured,
            LastLatencyMs = _lastLatencyMs,
            LastError = _lastError
        };
    }

    private async Task<LLMResult> ExecuteAsync(
        string prompt,
        LLMRole role,
        bool isStructured,
        CancellationToken ct)
    {
        if (!IsAvailable)
        {
            return LLMResult.Fail($"{_providerName} API key is not configured");
        }

        var apiKey = GetApiKey();
        var model = ResolveModel(role);
        var endpoint = $"{_baseUrl}/chat/completions";

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _lastRequestAt = DateTime.UtcNow;
                var sw = Stopwatch.StartNew();
                var request = new ChatCompletionsRequest
                {
                    Model = model,
                    Messages = new[]
                    {
                        new ChatMessagePayload
                        {
                            Role = "user",
                            Content = prompt
                        }
                    },
                    MaxTokens = 2048,
                    Temperature = 0.7,
                    ResponseFormat = isStructured
                        ? new ChatResponseFormat { Type = "json_object" }
                        : null
                };

                await WaitForRateLimitSlotAsync(ct);

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(request, JsonOptions),
                        Encoding.UTF8,
                        "application/json")
                };
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await _httpClient.SendAsync(httpRequest, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    var parsed = JsonSerializer.Deserialize<ChatCompletionsResponse>(content, JsonOptions);
                    var text = parsed?.Choices?.Length > 0
                        ? parsed.Choices[0].Message.Content ?? string.Empty
                        : string.Empty;

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sw.Stop();
                        _lastSuccessAt = DateTime.UtcNow;
                        _lastLatencyMs = (int)sw.ElapsedMilliseconds;
                        _lastError = string.Empty;
                        return LLMResult.Ok(text, model, latencyMs: _lastLatencyMs);
                    }
                }

                if (attempt < MaxRetries && ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(450 * (attempt + 1)), ct);
                    continue;
                }

                _lastFailureAt = DateTime.UtcNow;
                _lastError = $"{_providerName} HTTP {(int)response.StatusCode}: {content}";
                _failedRequests++;
                return LLMResult.Fail($"{_providerName} HTTP {(int)response.StatusCode}: {content}", model);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(450 * (attempt + 1)), ct);
                if (attempt == MaxRetries - 1)
                {
                    _lastFailureAt = DateTime.UtcNow;
                    _lastError = ex.Message;
                    _failedRequests++;
                    return LLMResult.Fail($"{_providerName} error: {ex.Message}", model);
                }
            }
            catch (Exception ex)
            {
                _lastFailureAt = DateTime.UtcNow;
                _lastError = ex.Message;
                _failedRequests++;
                return LLMResult.Fail($"{_providerName} error: {ex.Message}", model);
            }
        }

        _lastFailureAt = DateTime.UtcNow;
        _lastError = $"{_providerName}: max retries exceeded";
        _failedRequests++;
        return LLMResult.Fail($"{_providerName}: max retries exceeded", model);
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

    private string ResolveModel(LLMRole role)
    {
        var resolved = role switch
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

        if (string.IsNullOrWhiteSpace(resolved))
        {
            return _defaultModel;
        }

        // Pigeon defaults are OpenRouter model IDs. When a direct OpenAI/xAI
        // provider is selected, keep the connection usable unless the user
        // explicitly supplies a provider-native model name.
        if (!IsConfiguredModelCompatible(resolved))
        {
            return _defaultModel;
        }

        return resolved;
    }

    private bool IsConfiguredModelCompatible(string model)
    {
        if (string.Equals(_providerName, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            return !model.Contains('/', StringComparison.Ordinal);
        }

        if (string.Equals(_providerName, "Grok", StringComparison.OrdinalIgnoreCase))
        {
            return model.StartsWith("grok", StringComparison.OrdinalIgnoreCase);
        }

        return true;
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

                delay = _requestWindow.TryPeek(out var oldest)
                    ? RateLimitWindow - (now - oldest)
                    : TimeSpan.FromMilliseconds(100);

                if (delay < TimeSpan.FromMilliseconds(50))
                {
                    delay = TimeSpan.FromMilliseconds(50);
                }
            }
            finally
            {
                _rateLimitGate.Release();
            }

            await Task.Delay(delay, ct);
        }
    }

    private static string StripMarkdownCodeBlocks(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            text = text[7..];
        }
        else if (text.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            text = text[3..];
        }

        if (text.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^3];
        }

        return text.Trim();
    }

    private sealed class ChatCompletionsRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public ChatMessagePayload[] Messages { get; set; } = Array.Empty<ChatMessagePayload>();

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("response_format")]
        public ChatResponseFormat? ResponseFormat { get; set; }
    }

    private sealed class ChatMessagePayload
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class ChatResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "json_object";
    }

    private sealed class ChatCompletionsResponse
    {
        [JsonPropertyName("choices")]
        public ChatChoice[]? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessagePayload Message { get; set; } = new();
    }
}

public sealed class OpenAIService : OpenAICompatibleLLMService
{
    public OpenAIService(HttpClient httpClient, AISettings settings, Func<string> apiKeyResolver)
        : base(httpClient, settings, "OpenAI", apiKeyResolver, "https://api.openai.com/v1", "gpt-4o-mini")
    {
    }

    public OpenAIService(HttpClient httpClient, AISettings settings, string apiKey)
        : this(httpClient, settings, () => apiKey)
    {
    }
}

public sealed class GrokService : OpenAICompatibleLLMService
{
    public GrokService(HttpClient httpClient, AISettings settings, Func<string> apiKeyResolver)
        : base(httpClient, settings, "Grok", apiKeyResolver, "https://api.x.ai/v1", "grok-3-mini")
    {
    }

    public GrokService(HttpClient httpClient, AISettings settings, string apiKey)
        : this(httpClient, settings, () => apiKey)
    {
    }
}
