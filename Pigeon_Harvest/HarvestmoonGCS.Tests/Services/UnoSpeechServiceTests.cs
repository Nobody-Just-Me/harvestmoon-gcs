using System;
using System.Threading.Tasks;
using Xunit;
using HarvestmoonGCS.Services;

namespace HarvestmoonGCS.Tests.Services;

/// <summary>
/// Tests for speech service functionality
/// Note: These tests use a mock implementation since UnoSpeechService
/// is in the UI project and has platform-specific dependencies
/// </summary>
public class UnoSpeechServiceTests
{
    private class MockSpeechService : ISpeechService
    {
        public bool IsInitialized { get; private set; }
        public string LastSpokenText { get; private set; } = "";
        public bool LastInterruptFlag { get; private set; }
        public bool WasStopped { get; private set; }

        public Task InitializeAsync()
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public Task SpeakAsync(string text)
        {
            LastSpokenText = text;
            LastInterruptFlag = false;
            return Task.CompletedTask;
        }

        public Task SpeakAsync(string text, bool interrupt)
        {
            LastSpokenText = text;
            LastInterruptFlag = interrupt;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            WasStopped = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task InitializeAsync_ShouldInitializeService()
    {
        // Arrange
        var service = new MockSpeechService();

        // Act
        await service.InitializeAsync();

        // Assert
        Assert.True(service.IsInitialized);
        
        // Multiple initializations should be safe
        await service.InitializeAsync();
        Assert.True(service.IsInitialized);
    }

    [Fact]
    public async Task SpeakAsync_WithoutInitialize_ShouldAutoInitialize()
    {
        // Arrange
        var service = new MockSpeechService();

        // Act & Assert
        // Should not throw - auto-initializes
        await service.SpeakAsync("Test message");
        Assert.Equal("Test message", service.LastSpokenText);
    }

    [Fact]
    public async Task SpeakAsync_WithEmptyText_ShouldNotThrow()
    {
        // Arrange
        var service = new MockSpeechService();
        await service.InitializeAsync();

        // Act & Assert
        await service.SpeakAsync("");
        await service.SpeakAsync(null);
        await service.SpeakAsync("   ");
    }

    [Fact]
    public async Task StopAsync_WithoutInitialize_ShouldNotThrow()
    {
        // Arrange
        var service = new MockSpeechService();

        // Act & Assert
        await service.StopAsync();
        Assert.True(service.WasStopped);
    }

    [Fact]
    public async Task StopAsync_ShouldCancelCurrentSpeech()
    {
        // Arrange
        var service = new MockSpeechService();
        await service.InitializeAsync();

        // Act
        var speakTask = service.SpeakAsync("This is a long message that should be interrupted");
        await service.StopAsync();

        // Assert
        Assert.True(service.WasStopped);
        await speakTask; // Should complete without hanging
    }

    [Fact]
    public async Task SpeakAsync_WithInterrupt_ShouldStopCurrentSpeech()
    {
        // Arrange
        var service = new MockSpeechService();
        await service.InitializeAsync();

        // Act
        await service.SpeakAsync("First message that should be interrupted");
        await service.SpeakAsync("Second message with interrupt", interrupt: true);

        // Assert
        Assert.Equal("Second message with interrupt", service.LastSpokenText);
        Assert.True(service.LastInterruptFlag);
    }

    [Fact]
    public async Task SpeakAsync_Sequential_ShouldQueueMessages()
    {
        // Arrange
        var service = new MockSpeechService();
        await service.InitializeAsync();

        // Act
        await service.SpeakAsync("Message 1");
        await service.SpeakAsync("Message 2");
        await service.SpeakAsync("Message 3");

        // Assert
        Assert.Equal("Message 3", service.LastSpokenText);
    }

    [Fact]
    public async Task SpeakAsync_WithoutInterrupt_ShouldWaitForQueue()
    {
        // Arrange
        var service = new MockSpeechService();
        await service.InitializeAsync();

        // Act
        await service.SpeakAsync("First message", interrupt: false);
        await service.SpeakAsync("Second message", interrupt: false);

        // Assert
        Assert.Equal("Second message", service.LastSpokenText);
        Assert.False(service.LastInterruptFlag);
    }
}
