using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HarvestmoonGCS.Core.Models.AI;

namespace HarvestmoonGCS.Core.Services.AI;

/// <summary>
/// Circuit breaker states for LLM service health monitoring
/// </summary>
public enum CircuitState
{
    /// <summary>Circuit is closed - normal operation</summary>
    Closed,
    /// <summary>Circuit is open - failing fast</summary>
    Open,
    /// <summary>Circuit is half-open - testing recovery</summary>
    HalfOpen
}

/// <summary>
/// Circuit breaker status information
/// </summary>
public class CircuitStatus
{
    /// <summary>Current state of the circuit breaker</summary>
    public CircuitState CircuitState { get; set; }
    /// <summary>Number of consecutive failures</summary>
    public int ConsecutiveFailures { get; set; }
    /// <summary>Timestamp of last successful request</summary>
    public DateTime? LastSuccessAt { get; set; }
    /// <summary>When the circuit will close again (if open)</summary>
    public DateTime? CircuitOpenUntil { get; set; }
}

/// <summary>
/// Factory for LLM services with circuit breaker pattern.
/// Wraps both OpenRouterService and GeminiFallbackService with automatic failover.
/// </summary>
public class LLMServiceFactory
{
    private readonly ILLMService _primaryService;
    private readonly ILLMService _fallbackService;
    private readonly int _failureThreshold;
    private readonly int _circuitTimeoutMs;

    // Circuit breaker state - using Interlocked for thread safety
    private int _consecutiveFailures = 0;
    private CircuitState _circuitState = CircuitState.Closed;
    private DateTime? _circuitOpenUntil = null;
    private DateTime? _lastSuccessAt = null;
    private long _halfOpenSuccessCount = 0;

    // Thread safety
    private readonly object _stateLock = new object();

    /// <summary>
    /// Gets the primary service (OpenRouter)
    /// </summary>
    public ILLMService PrimaryService => _primaryService;

    /// <summary>
    /// Gets the fallback service (Gemini)
    /// </summary>
    public ILLMService FallbackService => _fallbackService;

    /// <summary>
    /// Creates a new LLMServiceFactory with circuit breaker
    /// </summary>
    /// <param name="primaryService">Primary LLM service (OpenRouter)</param>
    /// <param name="fallbackService">Fallback LLM service (Gemini)</param>
    /// <param name="failureThreshold">Number of consecutive failures before opening circuit (default: 5)</param>
    /// <param name="circuitTimeoutMs">Circuit timeout in milliseconds (default: 30000 = 30s)</param>
    /// <exception cref="ArgumentNullException">Thrown when either service is null</exception>
    public LLMServiceFactory(
        ILLMService primaryService,
        ILLMService fallbackService,
        int failureThreshold = 5,
        int circuitTimeoutMs = 30000)
    {
        _primaryService = primaryService ?? throw new ArgumentNullException(nameof(primaryService));
        _fallbackService = fallbackService ?? throw new ArgumentNullException(nameof(fallbackService));
        _failureThreshold = failureThreshold > 0 ? failureThreshold : 5;
        _circuitTimeoutMs = circuitTimeoutMs > 0 ? circuitTimeoutMs : 30000;
    }

    /// <summary>
    /// Gets the appropriate LLM service based on circuit breaker state and service availability.
    /// Returns primary service when circuit is closed, fallback when open, or unavailable service if both fail.
    /// </summary>
    /// <returns>An available ILLMService or an unavailable service placeholder</returns>
    public ILLMService GetService()
    {
        lock (_stateLock)
        {
            // Check if circuit timeout has expired
            if (_circuitState == CircuitState.Open && _circuitOpenUntil.HasValue)
            {
                if (DateTime.UtcNow >= _circuitOpenUntil.Value)
                {
                    // Transition to HalfOpen to test recovery
                    _circuitState = CircuitState.HalfOpen;
                    _halfOpenSuccessCount = 0;
                }
            }

            // Determine which service to use based on circuit state
            switch (_circuitState)
            {
                case CircuitState.Closed:
                    // Normal operation - use primary if available
                    if (_primaryService.IsAvailable)
                    {
                        return _primaryService;
                    }
                    // Primary not available, try fallback
                    if (_fallbackService.IsAvailable)
                    {
                        return _fallbackService;
                    }
                    break;

                case CircuitState.Open:
                    // Circuit is open - use fallback only
                    if (_fallbackService.IsAvailable)
                    {
                        return _fallbackService;
                    }
                    break;

                case CircuitState.HalfOpen:
                    // Testing recovery - try primary
                    if (_primaryService.IsAvailable)
                    {
                        return _primaryService;
                    }
                    // Primary still not available, use fallback
                    if (_fallbackService.IsAvailable)
                    {
                        return _fallbackService;
                    }
                    break;
            }

            // Both services unavailable
            return BuildUnavailableService();
        }
    }

    /// <summary>
    /// Records a successful request. Updates circuit breaker state accordingly.
    /// </summary>
    public void RecordSuccess()
    {
        lock (_stateLock)
        {
            _lastSuccessAt = DateTime.UtcNow;
            _consecutiveFailures = 0;

            switch (_circuitState)
            {
                case CircuitState.Open:
                    // First success after circuit open - transition to HalfOpen
                    _circuitState = CircuitState.HalfOpen;
                    _halfOpenSuccessCount = 1;
                    _circuitOpenUntil = null;
                    break;

                case CircuitState.HalfOpen:
                    // Another success in half-open state
                    _halfOpenSuccessCount++;
                    // After 2 consecutive successes, close the circuit
                    if (_halfOpenSuccessCount >= 2)
                    {
                        _circuitState = CircuitState.Closed;
                        _halfOpenSuccessCount = 0;
                    }
                    break;

                case CircuitState.Closed:
                    // Already closed, just update last success time
                    break;
            }
        }
    }

    /// <summary>
    /// Records a failed request. Updates circuit breaker state and may open the circuit.
    /// </summary>
    public void RecordFailure()
    {
        lock (_stateLock)
        {
            // Don't increment failures if circuit is already open
            if (_circuitState == CircuitState.Open)
            {
                return;
            }

            _consecutiveFailures++;

            // Check if we should open the circuit
            if (_consecutiveFailures >= _failureThreshold)
            {
                _circuitState = CircuitState.Open;
                _circuitOpenUntil = DateTime.UtcNow.AddMilliseconds(_circuitTimeoutMs);
            }
        }
    }

    /// <summary>
    /// Gets the current circuit breaker status
    /// </summary>
    /// <returns>Current circuit status including state, failures, and timestamps</returns>
    public CircuitStatus GetCircuitStatus()
    {
        lock (_stateLock)
        {
            return new CircuitStatus
            {
                CircuitState = _circuitState,
                ConsecutiveFailures = _consecutiveFailures,
                LastSuccessAt = _lastSuccessAt,
                CircuitOpenUntil = _circuitOpenUntil
            };
        }
    }

    private ILLMService BuildUnavailableService()
    {
        var primaryHealth = SafeGetHealthStatus(_primaryService);
        var fallbackHealth = SafeGetHealthStatus(_fallbackService);
        var reason = BuildUnavailableReason(primaryHealth, fallbackHealth);

        var primaryProvider = string.IsNullOrWhiteSpace(primaryHealth?.PrimaryProvider)
            ? _primaryService.ProviderName
            : primaryHealth!.PrimaryProvider;
        var fallbackProvider = string.IsNullOrWhiteSpace(fallbackHealth?.PrimaryProvider)
            ? _fallbackService.ProviderName
            : fallbackHealth!.PrimaryProvider;

        return new UnavailableLLMService(reason, primaryProvider, fallbackProvider);
    }

    private static LLMHealthStatus? SafeGetHealthStatus(ILLMService service)
    {
        try
        {
            return service.GetHealthStatus();
        }
        catch
        {
            return null;
        }
    }

    private static string BuildUnavailableReason(
        LLMHealthStatus? primaryHealth,
        LLMHealthStatus? fallbackHealth)
    {
        var details = new List<string>();

        if (primaryHealth != null)
        {
            if (!primaryHealth.PrimaryApiKeyConfigured)
            {
                details.Add($"API key primary ({primaryHealth.PrimaryProvider}) kosong");
            }
            else if (primaryHealth.CircuitOpen)
            {
                details.Add($"primary ({primaryHealth.PrimaryProvider}) sedang circuit-open");
            }
            else if (!string.IsNullOrWhiteSpace(primaryHealth.LastError))
            {
                details.Add($"primary ({primaryHealth.PrimaryProvider}) error: {primaryHealth.LastError}");
            }
        }

        if (fallbackHealth != null)
        {
            if (!fallbackHealth.PrimaryApiKeyConfigured)
            {
                details.Add($"API key fallback ({fallbackHealth.PrimaryProvider}) kosong");
            }
            else if (fallbackHealth.CircuitOpen)
            {
                details.Add($"fallback ({fallbackHealth.PrimaryProvider}) sedang circuit-open");
            }
            else if (!string.IsNullOrWhiteSpace(fallbackHealth.LastError))
            {
                details.Add($"fallback ({fallbackHealth.PrimaryProvider}) error: {fallbackHealth.LastError}");
            }
        }

        if (details.Count == 0)
        {
            return "No LLM services are available. Both primary and fallback providers are down.";
        }

        return $"No LLM services are available. {string.Join("; ", details)}.";
    }
}

/// <summary>
/// Placeholder service used when both primary and fallback services are unavailable
/// </summary>
internal class UnavailableLLMService : ILLMService
{
    private readonly string _reason;
    private readonly string _primaryProvider;
    private readonly string _fallbackProvider;

    public UnavailableLLMService(
        string reason = "No LLM services are available. Both primary and fallback services are down.",
        string primaryProvider = "Unavailable",
        string fallbackProvider = "Unavailable")
    {
        _reason = string.IsNullOrWhiteSpace(reason)
            ? "No LLM services are available. Both primary and fallback services are down."
            : reason.Trim();
        _primaryProvider = string.IsNullOrWhiteSpace(primaryProvider) ? "Unavailable" : primaryProvider;
        _fallbackProvider = string.IsNullOrWhiteSpace(fallbackProvider) ? "Unavailable" : fallbackProvider;
    }

    public bool IsAvailable => false;
    public string ProviderName => "Unavailable";

    public Task<LLMResult> GenerateAsync(string prompt, LLMRole role = LLMRole.TelemetryAnalysis, CancellationToken ct = default)
    {
        return Task.FromResult(LLMResult.Fail(_reason));
    }

    public Task<T?> GenerateStructuredAsync<T>(string prompt, LLMRole role = LLMRole.TelemetryAnalysis, CancellationToken ct = default) where T : class
    {
        return Task.FromResult<T?>(null);
    }

    public Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        return Task.FromResult(false);
    }

    public LLMHealthStatus GetHealthStatus()
    {
        return new LLMHealthStatus
        {
            IsConnected = false,
            PrimaryProvider = _primaryProvider,
            FallbackProvider = _fallbackProvider,
            ActiveProvider = "Unavailable",
            ActiveModel = "Unavailable",
            PrimaryModel = "Unavailable",
            CircuitOpen = true,
            LastError = _reason
        };
    }
}
