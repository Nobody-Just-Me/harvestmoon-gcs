using System;
using System.Threading.Tasks;
using Moq;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Services;
using Xunit;

namespace Pigeon_Uno.Tests.Services;

public class AlertManagerTests
{
    private static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000, int pollMs = 25)
    {
        var started = DateTime.UtcNow;
        while ((DateTime.UtcNow - started).TotalMilliseconds < timeoutMs)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(pollMs);
        }

        return condition();
    }

    [Fact]
    public async Task InitializeAsync_CallsSpeechServiceInitialize()
    {
        // Arrange
        var mockSpeechService = new Mock<ISpeechService>();
        mockSpeechService.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        var alertManager = new AlertManager(mockSpeechService.Object);

        // Act
        await alertManager.InitializeAsync();

        // Assert
        mockSpeechService.Verify(s => s.InitializeAsync(), Times.Once);
    }

    [Fact]
    public async Task QueueBatteryWarningAsync_WithCriticalPriority_CallsSpeakAsyncWithInterrupt()
    {
        // Arrange
        var mockSpeechService = new Mock<ISpeechService>();
        mockSpeechService.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        var spoke = false;
        var interrupt = true;
        mockSpeechService
            .Setup(s => s.SpeakAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((_, i) =>
            {
                spoke = true;
                interrupt = i;
            })
            .Returns(Task.CompletedTask);
        
        var alertManager = new AlertManager(mockSpeechService.Object);
        await alertManager.InitializeAsync();

        // Act - Queue a battery warning (High priority, not critical)
        await alertManager.QueueBatteryWarningAsync(15);
        
        var processed = await WaitUntilAsync(() => spoke);

        Assert.True(processed);
        Assert.False(interrupt);
    }

    [Fact]
    public async Task QueueGpsLostAsync_WithCriticalPriority_CallsSpeakAsyncWithInterrupt()
    {
        // Arrange
        var mockSpeechService = new Mock<ISpeechService>();
        mockSpeechService.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        var spoke = false;
        var interrupt = false;
        mockSpeechService
            .Setup(s => s.SpeakAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((_, i) =>
            {
                spoke = true;
                interrupt = i;
            })
            .Returns(Task.CompletedTask);
        
        var alertManager = new AlertManager(mockSpeechService.Object);
        await alertManager.InitializeAsync();

        // Act - Queue GPS lost (Critical priority)
        await alertManager.QueueGpsLostAsync();
        
        var processed = await WaitUntilAsync(() => spoke);

        Assert.True(processed);
        Assert.True(interrupt);
    }

    [Fact]
    public async Task QueueConnectionLostAsync_WithCriticalPriority_CallsSpeakAsyncWithInterrupt()
    {
        // Arrange
        var mockSpeechService = new Mock<ISpeechService>();
        mockSpeechService.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        var spoke = false;
        var interrupt = false;
        mockSpeechService
            .Setup(s => s.SpeakAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((_, i) =>
            {
                spoke = true;
                interrupt = i;
            })
            .Returns(Task.CompletedTask);
        
        var alertManager = new AlertManager(mockSpeechService.Object);
        await alertManager.InitializeAsync();

        // Act - Queue connection lost (Critical priority)
        await alertManager.QueueConnectionLostAsync();
        
        var processed = await WaitUntilAsync(() => spoke);

        Assert.True(processed);
        Assert.True(interrupt);
    }

    [Fact]
    public async Task QueueFlightModeChangeAsync_WithNormalPriority_CallsSpeakAsyncWithoutInterrupt()
    {
        // Arrange
        var mockSpeechService = new Mock<ISpeechService>();
        mockSpeechService.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        var spoke = false;
        var interrupt = true;
        mockSpeechService
            .Setup(s => s.SpeakAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((_, i) =>
            {
                spoke = true;
                interrupt = i;
            })
            .Returns(Task.CompletedTask);
        
        var alertManager = new AlertManager(mockSpeechService.Object);
        await alertManager.InitializeAsync();

        // Act - Queue flight mode change (Normal priority)
        await alertManager.QueueFlightModeChangeAsync("STABILIZE");
        
        var processed = await WaitUntilAsync(() => spoke);

        Assert.True(processed);
        Assert.False(interrupt);
    }

    [Fact]
    public async Task StopAsync_CallsSpeechServiceStop()
    {
        // Arrange
        var mockSpeechService = new Mock<ISpeechService>();
        mockSpeechService.Setup(s => s.InitializeAsync()).Returns(Task.CompletedTask);
        mockSpeechService.Setup(s => s.StopAsync()).Returns(Task.CompletedTask);
        
        var alertManager = new AlertManager(mockSpeechService.Object);
        await alertManager.InitializeAsync();

        // Act
        await alertManager.StopAsync();

        // Assert
        mockSpeechService.Verify(s => s.StopAsync(), Times.Once);
    }
}
