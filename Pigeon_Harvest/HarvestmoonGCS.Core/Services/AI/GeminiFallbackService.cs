using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models.AI;

namespace HarvestmoonGCS.Core.Services.AI;

public class GeminiFallbackService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly Func<string> _apiKeyResolver;
    private readonly string _model;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    private DateTime? _lastSuccessAt;
    private DateTime? _lastFailureAt;
    private DateTime? _lastRequestAt;
    private string _lastError = string.Empty;
    private int _lastLatencyMs;
    private long _failedRequests;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public bool IsAvailable => !string.IsNullOrEmpty(GetApiKey());
    public string ProviderName => "Gemini";

    public GeminiFallbackService(HttpClient httpClient, Func<string> apiKeyResolver, string model)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _apiKeyResolver = apiKeyResolver ?? (() => string.Empty);
        _model = model ?? "gemini-2.0-flash";
    }

    public GeminiFallbackService(HttpClient httpClient, string apiKey, string model)
        : this(httpClient, () => apiKey, model)
    {
    }

    public async Task<LLMResult> GenerateAsync(
        string prompt,
        LLMRole role = LLMRole.TelemetryAnalysis,
        CancellationToken ct = default)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            _lastFailureAt = DateTime.UtcNow;
            _lastError = "API key Gemini kosong.";
            _failedRequests++;
            return LLMResult.Fail("Gemini unavailable: API key kosong.", _model);
        }

        try
        {
            _lastRequestAt = DateTime.UtcNow;
            var sw = Stopwatch.StartNew();
            var requestBody = new GeminiRequest
            {
                Contents = new[]
                {
                    new GeminiContent
                    {
                        Parts = new[] { new GeminiPart { Text = prompt } },
                        Role = "user"
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody, JsonOptions);
            using var request = new HttpRequestMessage(HttpMethod.Post, 
                $"{BaseUrl}/{_model}:generateContent");
            request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _lastFailureAt = DateTime.UtcNow;
                _lastError = MapHttpError(response.StatusCode, errorContent);
                _failedRequests++;
                return LLMResult.Fail(_lastError, _model);
            }

            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseContent, JsonOptions);

            if (geminiResponse?.Candidates == null || geminiResponse.Candidates.Length == 0)
            {
                _lastFailureAt = DateTime.UtcNow;
                _lastError = "No response candidates from Gemini API";
                _failedRequests++;
                return LLMResult.Fail("No response candidates from Gemini API", _model);
            }

            var text = geminiResponse.Candidates[0].Content?.Parts?[0]?.Text ?? string.Empty;
            sw.Stop();
            _lastSuccessAt = DateTime.UtcNow;
            _lastLatencyMs = (int)sw.ElapsedMilliseconds;
            _lastError = string.Empty;
            return LLMResult.Ok(text, _model, fallback: true, latencyMs: _lastLatencyMs);
        }
        catch (OperationCanceledException)
        {
            _lastFailureAt = DateTime.UtcNow;
            _lastError = "Request was cancelled";
            _failedRequests++;
            return LLMResult.Fail("Request was cancelled", _model);
        }
        catch (HttpRequestException ex)
        {
            _lastFailureAt = DateTime.UtcNow;
            _lastError = ex.Message;
            _failedRequests++;
            return LLMResult.Fail($"Network error: {ex.Message}", _model);
        }
        catch (Exception ex)
        {
            _lastFailureAt = DateTime.UtcNow;
            _lastError = ex.Message;
            _failedRequests++;
            return LLMResult.Fail($"Unexpected error: {ex.Message}", _model);
        }
    }

    public async Task<T?> GenerateStructuredAsync<T>(
        string prompt,
        LLMRole role = LLMRole.TelemetryAnalysis,
        CancellationToken ct = default) where T : class
    {
        var result = await GenerateAsync(prompt, role, ct);
        
        if (!result.Success || string.IsNullOrEmpty(result.Text))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(result.Text, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(GetApiKey()))
        {
            return false;
        }

        try
        {
            var result = await GenerateAsync(".", LLMRole.Fallback, ct);
            return result.Success;
        }
        catch
        {
            return false;
        }
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
            ActiveModel = _model,
            PrimaryModel = _model,
            FallbackModel = string.Empty,
            CircuitOpen = !IsAvailable,
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

    private static string MapHttpError(System.Net.HttpStatusCode statusCode, string? responseContent)
    {
        var details = string.IsNullOrWhiteSpace(responseContent)
            ? string.Empty
            : $" Detail: {responseContent}";

        return statusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized =>
                "Gemini menolak autentikasi (401). Periksa API key fallback." + details,
            System.Net.HttpStatusCode.Forbidden =>
                "Gemini request ditolak (403). Periksa izin API key." + details,
            System.Net.HttpStatusCode.NotFound =>
                "Model Gemini tidak ditemukan (404). Periksa model fallback." + details,
            System.Net.HttpStatusCode.TooManyRequests =>
                "Gemini rate limit tercapai (429). Coba lagi beberapa saat." + details,
            _ =>
                $"HTTP {(int)statusCode} ({statusCode}) dari Gemini." + details
        };
    }
}

internal class GeminiRequest
{
    [JsonPropertyName("contents")]
    public GeminiContent[]? Contents { get; set; }
}

internal class GeminiContent
{
    [JsonPropertyName("parts")]
    public GeminiPart[]? Parts { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }
}

internal class GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

internal class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public GeminiCandidate[]? Candidates { get; set; }

    [JsonPropertyName("error")]
    public GeminiError? Error { get; set; }
}

internal class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }
}

internal class GeminiError
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
