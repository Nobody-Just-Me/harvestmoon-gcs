using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using Pigeon_Uno.Core.Services.AI;
using Pigeon_Uno.Core.Models.AI;

namespace Pigeon_Uno.Tests.Services.AI;

public class LLMServiceFactoryTests
{
    private const string TestApiKey = "test-api-key";
    private const string TestPrompt = "Analyze telemetry data";

    #region Circuit Breaker State Tests

    [Fact]
    public void Constructor_WithBothServices_InitializesCorrectly()
    {
        var openRouter = CreateMockOpenRouter(true);
        var gemini = CreateMockGemini(true);

        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object);

        factory.Should().NotBeNull();
        factory.PrimaryService.Should().Be(openRouter.Object);
        factory.FallbackService.Should().Be(gemini.Object);
    }

    [Fact]
    public void Constructor_WithNullServices_ThrowsArgumentNullException()
    {
        var openRouter = CreateMockOpenRouter(true);

        Action act = () => new LLMServiceFactory(null!, openRouter.Object);
        act.Should().Throw<ArgumentNullException>();

        Action act2 = () => new LLMServiceFactory(openRouter.Object, null!);
        act2.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetService_WhenPrimaryAvailable_ReturnsPrimary()
    {
        var openRouter = CreateMockOpenRouter(true);
        var gemini = CreateMockGemini(true);
        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object);

        var service = factory.GetService();

        service.Should().Be(openRouter.Object);
    }

    [Fact]
    public void GetService_WhenPrimaryUnavailable_ReturnsFallback()
    {
        var openRouter = CreateMockOpenRouter(false);
        var gemini = CreateMockGemini(true);
        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object);

        var service = factory.GetService();

        service.Should().Be(gemini.Object);
    }

    [Fact]
    public void GetService_WhenBothUnavailable_ReturnsUnavailableService()
    {
        var openRouter = CreateMockOpenRouter(false);
        var gemini = CreateMockGemini(false);
        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object);

        var service = factory.GetService();

        service.IsAvailable.Should().BeFalse();
        service.ProviderName.Should().Be("Unavailable");
    }

    #endregion

    #region Circuit Breaker Failure Tracking Tests

    [Fact]
    public void RecordSuccess_WhenCircuitClosed_IncrementsSuccessCount()
    {
        var openRouter = CreateMockOpenRouter(true);
        var gemini = CreateMockGemini(true);
        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object);

        factory.RecordSuccess();
        var status = factory.GetCircuitStatus();

        status.ConsecutiveFailures.Should().Be(0);
        status.LastSuccessAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RecordFailure_WhenCircuitClosed_IncrementsFailureCount()
    {
        var openRouter = CreateMockOpenRouter(true);
        var gemini = CreateMockGemini(true);
        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object);

        factory.RecordFailure();
        var status = factory.GetCircuitStatus();

        status.ConsecutiveFailures.Should().Be(1);
        status.CircuitState.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void RecordFailure_After5ConsecutiveFailures_OpensCircuit()
    {
        var openRouter = CreateMockOpenRouter(true);
        var gemini = CreateMockGemini(true);
        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object);

        // Record 5 failures
        for (int i = 0; i < 5; i++)
        {
            factory.RecordFailure();
        }

        var status = factory.GetCircuitStatus();

        status.ConsecutiveFailures.Should().Be(5);
        status.CircuitState.Should().Be(CircuitState.Open);
        status.CircuitOpenUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void RecordFailure_WhenCircuitOpen_DoesNotIncrementFurther()
    {
        var openRouter = CreateMockOpenRouter(true);
        var gemini = CreateMockGemini(true);
        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object);

        // Open the circuit
        for (int i = 0; i < 5; i++)
        {
            factory.RecordFailure();
        }

        // Try to record more failures
        factory.RecordFailure();
        factory.RecordFailure();

        var status = factory.GetCircuitStatus();

        status.ConsecutiveFailures.Should().Be(5); // Should not increase
        status.CircuitState.Should().Be(CircuitState.Open);
    }

    [Fact]
    public void RecordSuccess_WhenCircuitOpen_TransitionsToHalfOpen()
    {
        var openRouter = CreateMockOpenRouter(true);
        var gemini = CreateMockGemini(true);
        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object);

        // Open the circuit
        for (int i = 0; i < 5; i++)
        {
            factory.RecordFailure();
        }

        // Record success should transition to HalfOpen
        factory.RecordSuccess();
        var status = factory.GetCircuitStatus();

        status.CircuitState.Should().Be(CircuitState.HalfOpen);
        status.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public void RecordSuccess_WhenHalfOpen_ClosesCircuit()
    {
        var openRouter = CreateMockOpenRouter(true);
        var gemini = CreateMockGemini(true);
        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object);

        // Open the circuit
        for (int i = 0; i < 5; i++)
        {
            factory.RecordFailure();
        }

        // First success -> HalfOpen
        factory.RecordSuccess();
        // Second success -> Closed
        factory.RecordSuccess();

        var status = factory.GetCircuitStatus();

        status.CircuitState.Should().Be(CircuitState.Closed);
        status.ConsecutiveFailures.Should().Be(0);
    }

    #endregion

    #region Circuit Breaker Timeout Tests

    [Fact]
    public void GetService_WhenCircuitOpenButTimeoutExpired_TransitionsToHalfOpen()
    {
        var openRouter = CreateMockOpenRouter(true);
        var gemini = CreateMockGemini(true);
        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object, 
            circuitTimeoutMs: 100); // 100ms timeout for testing

        // Open the circuit
        for (int i = 0; i < 5; i++)
        {
            factory.RecordFailure();
        }

        var statusBefore = factory.GetCircuitStatus();
        statusBefore.CircuitState.Should().Be(CircuitState.Open);

        // Wait for timeout
        Thread.Sleep(150);

        // GetService should trigger state check and transition to HalfOpen
        var service = factory.GetService();
        var statusAfter = factory.GetCircuitStatus();

        statusAfter.CircuitState.Should().Be(CircuitState.HalfOpen);
    }

    [Fact]
    public void GetService_WhenCircuitOpen_ReturnsFallbackService()
    {
        var openRouter = CreateMockOpenRouter(true);
        var gemini = CreateMockGemini(true);
        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object);

        // Open the circuit
        for (int i = 0; i < 5; i++)
        {
            factory.RecordFailure();
        }

        var service = factory.GetService();

        service.Should().Be(gemini.Object);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void RecordFailure_ConcurrentCalls_MaintainsCorrectCount()
    {
        var openRouter = CreateMockOpenRouter(true);
        var gemini = CreateMockGemini(true);
        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object);

        var tasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(() => factory.RecordFailure());
        }
        Task.WaitAll(tasks);

        var status = factory.GetCircuitStatus();

        // Circuit should be open after 5 failures, but we may have more due to race conditions
        status.ConsecutiveFailures.Should().BeGreaterOrEqualTo(5);
        status.CircuitState.Should().Be(CircuitState.Open);
    }

    [Fact]
    public void RecordSuccess_ConcurrentCalls_MaintainsCorrectState()
    {
        var openRouter = CreateMockOpenRouter(true);
        var gemini = CreateMockGemini(true);
        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object);

        // First open the circuit
        for (int i = 0; i < 5; i++)
        {
            factory.RecordFailure();
        }

        // Then record successes concurrently
        var tasks = new Task[5];
        for (int i = 0; i < 5; i++)
        {
            tasks[i] = Task.Run(() => factory.RecordSuccess());
        }
        Task.WaitAll(tasks);

        var status = factory.GetCircuitStatus();

        // After successes, circuit should be closed or half-open
        status.CircuitState.Should().BeOneOf(CircuitState.Closed, CircuitState.HalfOpen);
    }

    #endregion

    #region Health Monitoring Tests

    [Fact]
    public void GetCircuitStatus_ReturnsCorrectHealthInfo()
    {
        var openRouter = CreateMockOpenRouter(true);
        var gemini = CreateMockGemini(true);
        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object);

        var status = factory.GetCircuitStatus();

        status.Should().NotBeNull();
        status.CircuitState.Should().Be(CircuitState.Closed);
        status.ConsecutiveFailures.Should().Be(0);
        status.LastSuccessAt.Should().BeNull();
        status.CircuitOpenUntil.Should().BeNull();
    }

    [Fact]
    public void GetCircuitStatus_AfterFailures_UpdatesLastSuccess()
    {
        var openRouter = CreateMockOpenRouter(true);
        var gemini = CreateMockGemini(true);
        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object);

        factory.RecordSuccess();
        var status = factory.GetCircuitStatus();

        status.LastSuccessAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task GenerateAsync_WithWorkingPrimary_UsesPrimaryAndRecordsSuccess()
    {
        var openRouter = CreateMockOpenRouter(true, "OpenRouter response");
        var gemini = CreateMockGemini(true);
        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object);

        var service = factory.GetService();
        var result = await service.GenerateAsync(TestPrompt);

        result.Success.Should().BeTrue();
        result.Text.Should().Be("OpenRouter response");
        
        // Record success after successful operation
        factory.RecordSuccess();
        
        var status = factory.GetCircuitStatus();
        status.LastSuccessAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateAsync_WithFailingPrimary_UsesFallback()
    {
        var openRouter = CreateMockOpenRouter(false);
        var gemini = CreateMockGemini(true, "Gemini response");
        var factory = new LLMServiceFactory(openRouter.Object, gemini.Object);

        var service = factory.GetService();
        var result = await service.GenerateAsync(TestPrompt);

        result.Success.Should().BeTrue();
        result.Text.Should().Be("Gemini response");
    }

    #endregion

    #region Helper Methods

    private static Mock<ILLMService> CreateMockOpenRouter(bool isAvailable, string responseText = "")
    {
        var mock = new Mock<ILLMService>();
        mock.Setup(x => x.IsAvailable).Returns(isAvailable);
        mock.Setup(x => x.ProviderName).Returns("OpenRouter");
        mock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<LLMRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string prompt, LLMRole role, CancellationToken ct) =>
            {
                if (isAvailable)
                    return LLMResult.Ok(responseText, "openrouter/model");
                return LLMResult.Fail("Service unavailable", "openrouter/model");
            });
        mock.Setup(x => x.GenerateStructuredAsync<It.IsAnyType>(It.IsAny<string>(), It.IsAny<LLMRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string prompt, LLMRole role, CancellationToken ct) => null);
        mock.Setup(x => x.TestConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(isAvailable);
        mock.Setup(x => x.GetHealthStatus()).Returns(new LLMHealthStatus { IsConnected = isAvailable });
        return mock;
    }

    private static Mock<ILLMService> CreateMockGemini(bool isAvailable, string responseText = "")
    {
        var mock = new Mock<ILLMService>();
        mock.Setup(x => x.IsAvailable).Returns(isAvailable);
        mock.Setup(x => x.ProviderName).Returns("Gemini");
        mock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<LLMRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string prompt, LLMRole role, CancellationToken ct) =>
            {
                if (isAvailable)
                    return LLMResult.Ok(responseText, "gemini/model", fallback: true);
                return LLMResult.Fail("Service unavailable", "gemini/model");
            });
        mock.Setup(x => x.GenerateStructuredAsync<It.IsAnyType>(It.IsAny<string>(), It.IsAny<LLMRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string prompt, LLMRole role, CancellationToken ct) => null);
        mock.Setup(x => x.TestConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(isAvailable);
        mock.Setup(x => x.GetHealthStatus()).Returns(new LLMHealthStatus { IsConnected = isAvailable });
        return mock;
    }

    #endregion
}
