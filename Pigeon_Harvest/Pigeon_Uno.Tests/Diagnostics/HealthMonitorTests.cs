using Pigeon_Uno.Core.Diagnostics;
using System;
using System.Threading;
using Xunit;

namespace Pigeon_Uno.Tests.Diagnostics;

/// <summary>
/// Unit tests for HealthMonitor.
/// Tests health status transitions, overall health calculation, and timeout detection.
/// Feature: transport-layer-debugging
/// Validates: Requirements 10.3
/// </summary>
public class HealthMonitorTests
{
    /// <summary>
    /// Test that transport health transitions from Unknown to Healthy when activity is updated.
    /// </summary>
    [Fact]
    public void TransportHealth_InitiallyUnknown_BecomesHealthyAfterActivity()
    {
        // Arrange
        var logger = new DiagnosticLogger();
        var monitor = new HealthMonitor(logger);

        // Act - Check initial state
        var initialHealth = monitor.GetTransportHealth();

        // Assert - Should be Unknown initially
        Assert.Equal(HealthStatusLevel.Unknown, initialHealth.Level);
        Assert.Contains("No data received yet", initialHealth.Message);

        // Act - Update activity
        monitor.UpdateTransportActivity();
        var healthyStatus = monitor.GetTransportHealth();

        // Assert - Should be Healthy now
        Assert.Equal(HealthStatusLevel.Healthy, healthyStatus.Level);
        Assert.Contains("Receiving data", healthyStatus.Message);
    }

    /// <summary>
    /// Test that transport health transitions from Healthy to Warning after 2 seconds of inactivity.
    /// </summary>
    [Fact]
    public void TransportHealth_BecomesWarning_After2SecondsInactivity()
    {
        // Arrange
        var logger = new DiagnosticLogger();
        var monitor = new HealthMonitor(logger);
        monitor.UpdateTransportActivity();

        // Wait for 2.1 seconds
        Thread.Sleep(2100);

        // Act
        var health = monitor.GetTransportHealth();

        // Assert
        Assert.Equal(HealthStatusLevel.Warning, health.Level);
        Assert.Contains("No data for", health.Message);
    }

    /// <summary>
    /// Test that transport health transitions from Warning to Critical after 10 seconds of inactivity.
    /// </summary>
    [Fact]
    public void TransportHealth_BecomesCritical_After10SecondsInactivity()
    {
        // Arrange
        var logger = new DiagnosticLogger();
        var monitor = new HealthMonitor(logger);
        monitor.UpdateTransportActivity();

        // Wait for 10.1 seconds
        Thread.Sleep(10100);

        // Act
        var health = monitor.GetTransportHealth();

        // Assert
        Assert.Equal(HealthStatusLevel.Critical, health.Level);
        Assert.Contains("No data for", health.Message);
    }

    /// <summary>
    /// Test that walker health follows the same pattern as transport health.
    /// </summary>
    [Fact]
    public void WalkerHealth_FollowsSamePattern_AsTransportHealth()
    {
        // Arrange
        var logger = new DiagnosticLogger();
        var monitor = new HealthMonitor(logger);

        // Act & Assert - Initial state
        var initialHealth = monitor.GetWalkerHealth();
        Assert.Equal(HealthStatusLevel.Unknown, initialHealth.Level);
        Assert.Contains("Walker not active yet", initialHealth.Message);

        // Act & Assert - After activity
        monitor.UpdateWalkerActivity();
        var healthyStatus = monitor.GetWalkerHealth();
        Assert.Equal(HealthStatusLevel.Healthy, healthyStatus.Level);
        Assert.Contains("Processing data", healthyStatus.Message);
    }

    /// <summary>
    /// Test that parser health follows the same pattern.
    /// </summary>
    [Fact]
    public void ParserHealth_FollowsSamePattern()
    {
        // Arrange
        var logger = new DiagnosticLogger();
        var monitor = new HealthMonitor(logger);

        // Act & Assert - Initial state
        var initialHealth = monitor.GetParserHealth();
        Assert.Equal(HealthStatusLevel.Unknown, initialHealth.Level);
        Assert.Contains("No packets parsed yet", initialHealth.Message);

        // Act & Assert - After activity
        monitor.UpdateParserActivity();
        var healthyStatus = monitor.GetParserHealth();
        Assert.Equal(HealthStatusLevel.Healthy, healthyStatus.Level);
        Assert.Contains("Parsing packets", healthyStatus.Message);
    }

    /// <summary>
    /// Test that event chain health follows the same pattern.
    /// </summary>
    [Fact]
    public void EventChainHealth_FollowsSamePattern()
    {
        // Arrange
        var logger = new DiagnosticLogger();
        var monitor = new HealthMonitor(logger);

        // Act & Assert - Initial state
        var initialHealth = monitor.GetEventChainHealth();
        Assert.Equal(HealthStatusLevel.Unknown, initialHealth.Level);
        Assert.Contains("No telemetry events yet", initialHealth.Message);

        // Act & Assert - After activity
        monitor.UpdateEventChainActivity();
        var healthyStatus = monitor.GetEventChainHealth();
        Assert.Equal(HealthStatusLevel.Healthy, healthyStatus.Level);
        Assert.Contains("Events firing", healthyStatus.Message);
    }

    /// <summary>
    /// Test that overall health is Unknown when all components are Unknown.
    /// </summary>
    [Fact]
    public void OverallHealth_IsUnknown_WhenAllComponentsUnknown()
    {
        // Arrange
        var logger = new DiagnosticLogger();
        var monitor = new HealthMonitor(logger);

        // Act
        var overallHealth = monitor.GetOverallHealth();

        // Assert
        Assert.Equal(HealthStatusLevel.Unknown, overallHealth.Status);
        Assert.Equal(HealthStatusLevel.Unknown, overallHealth.Transport.Level);
        Assert.Equal(HealthStatusLevel.Unknown, overallHealth.Walker.Level);
        Assert.Equal(HealthStatusLevel.Unknown, overallHealth.Parser.Level);
        Assert.Equal(HealthStatusLevel.Unknown, overallHealth.EventChain.Level);
    }

    /// <summary>
    /// Test that overall health is Healthy when all components are Healthy.
    /// </summary>
    [Fact]
    public void OverallHealth_IsHealthy_WhenAllComponentsHealthy()
    {
        // Arrange
        var logger = new DiagnosticLogger();
        var monitor = new HealthMonitor(logger);

        // Update all components
        monitor.UpdateTransportActivity();
        monitor.UpdateWalkerActivity();
        monitor.UpdateParserActivity();
        monitor.UpdateEventChainActivity();

        // Act
        var overallHealth = monitor.GetOverallHealth();

        // Assert
        Assert.Equal(HealthStatusLevel.Healthy, overallHealth.Status);
        Assert.Equal(HealthStatusLevel.Healthy, overallHealth.Transport.Level);
        Assert.Equal(HealthStatusLevel.Healthy, overallHealth.Walker.Level);
        Assert.Equal(HealthStatusLevel.Healthy, overallHealth.Parser.Level);
        Assert.Equal(HealthStatusLevel.Healthy, overallHealth.EventChain.Level);
    }

    /// <summary>
    /// Test that overall health is Warning when any component is Warning.
    /// </summary>
    [Fact]
    public void OverallHealth_IsWarning_WhenAnyComponentWarning()
    {
        // Arrange
        var logger = new DiagnosticLogger();
        var monitor = new HealthMonitor(logger);

        // Update all components
        monitor.UpdateTransportActivity();
        monitor.UpdateWalkerActivity();
        monitor.UpdateParserActivity();
        monitor.UpdateEventChainActivity();

        // Wait for one component to become Warning
        Thread.Sleep(2100);

        // Act
        var overallHealth = monitor.GetOverallHealth();

        // Assert - Overall should be Warning because all components are Warning
        Assert.Equal(HealthStatusLevel.Warning, overallHealth.Status);
    }

    /// <summary>
    /// Test that overall health is Critical when any component is Critical.
    /// </summary>
    [Fact]
    public void OverallHealth_IsCritical_WhenAnyComponentCritical()
    {
        // Arrange
        var logger = new DiagnosticLogger();
        var monitor = new HealthMonitor(logger);

        // Update all components except transport
        monitor.UpdateWalkerActivity();
        monitor.UpdateParserActivity();
        monitor.UpdateEventChainActivity();

        // Update transport and wait for it to become Critical
        monitor.UpdateTransportActivity();
        Thread.Sleep(10100);

        // Act
        var overallHealth = monitor.GetOverallHealth();

        // Assert - Overall should be Critical because transport is Critical
        Assert.Equal(HealthStatusLevel.Critical, overallHealth.Status);
        Assert.Equal(HealthStatusLevel.Critical, overallHealth.Transport.Level);
    }

    /// <summary>
    /// Test that overall health prioritizes Critical over Warning.
    /// </summary>
    [Fact]
    public void OverallHealth_PrioritizesCritical_OverWarning()
    {
        // Arrange
        var logger = new DiagnosticLogger();
        var monitor = new HealthMonitor(logger);

        // Update all components
        monitor.UpdateTransportActivity();
        monitor.UpdateWalkerActivity();
        monitor.UpdateParserActivity();
        monitor.UpdateEventChainActivity();

        // Wait for transport to become Critical (10+ seconds)
        Thread.Sleep(10100);

        // Act
        var overallHealth = monitor.GetOverallHealth();

        // Assert - Overall should be Critical even though other components might be Warning
        Assert.Equal(HealthStatusLevel.Critical, overallHealth.Status);
    }

    /// <summary>
    /// Test that health status factory methods create correct instances.
    /// </summary>
    [Fact]
    public void HealthStatus_FactoryMethods_CreateCorrectInstances()
    {
        // Act
        var healthy = HealthStatus.Healthy("Test healthy");
        var warning = HealthStatus.Warning("Test warning");
        var critical = HealthStatus.Critical("Test critical");
        var unknown = HealthStatus.Unknown("Test unknown");

        // Assert
        Assert.Equal(HealthStatusLevel.Healthy, healthy.Level);
        Assert.Equal("Test healthy", healthy.Message);

        Assert.Equal(HealthStatusLevel.Warning, warning.Level);
        Assert.Equal("Test warning", warning.Message);

        Assert.Equal(HealthStatusLevel.Critical, critical.Level);
        Assert.Equal("Test critical", critical.Message);

        Assert.Equal(HealthStatusLevel.Unknown, unknown.Level);
        Assert.Equal("Test unknown", unknown.Message);
    }

    /// <summary>
    /// Test that HealthMonitor requires a logger.
    /// </summary>
    [Fact]
    public void HealthMonitor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new HealthMonitor(null!));
    }

    /// <summary>
    /// Test that multiple rapid updates keep component healthy.
    /// </summary>
    [Fact]
    public void ComponentHealth_RemainsHealthy_WithRapidUpdates()
    {
        // Arrange
        var logger = new DiagnosticLogger();
        var monitor = new HealthMonitor(logger);

        // Act - Update multiple times rapidly
        for (int i = 0; i < 10; i++)
        {
            monitor.UpdateTransportActivity();
            Thread.Sleep(100); // 100ms between updates
        }

        var health = monitor.GetTransportHealth();

        // Assert - Should still be Healthy
        Assert.Equal(HealthStatusLevel.Healthy, health.Level);
    }

    /// <summary>
    /// Test that health can recover from Warning to Healthy.
    /// </summary>
    [Fact]
    public void ComponentHealth_CanRecover_FromWarningToHealthy()
    {
        // Arrange
        var logger = new DiagnosticLogger();
        var monitor = new HealthMonitor(logger);
        monitor.UpdateTransportActivity();

        // Wait for Warning state
        Thread.Sleep(2100);
        var warningHealth = monitor.GetTransportHealth();
        Assert.Equal(HealthStatusLevel.Warning, warningHealth.Level);

        // Act - Update activity to recover
        monitor.UpdateTransportActivity();
        var recoveredHealth = monitor.GetTransportHealth();

        // Assert - Should be Healthy again
        Assert.Equal(HealthStatusLevel.Healthy, recoveredHealth.Level);
    }

    /// <summary>
    /// Test that health can recover from Critical to Healthy.
    /// </summary>
    [Fact]
    public void ComponentHealth_CanRecover_FromCriticalToHealthy()
    {
        // Arrange
        var logger = new DiagnosticLogger();
        var monitor = new HealthMonitor(logger);
        monitor.UpdateTransportActivity();

        // Wait for Critical state
        Thread.Sleep(10100);
        var criticalHealth = monitor.GetTransportHealth();
        Assert.Equal(HealthStatusLevel.Critical, criticalHealth.Level);

        // Act - Update activity to recover
        monitor.UpdateTransportActivity();
        var recoveredHealth = monitor.GetTransportHealth();

        // Assert - Should be Healthy again
        Assert.Equal(HealthStatusLevel.Healthy, recoveredHealth.Level);
    }

    /// <summary>
    /// Test edge case: health check exactly at 2 second boundary.
    /// </summary>
    [Fact]
    public void ComponentHealth_AtExact2Seconds_IsWarning()
    {
        // Arrange
        var logger = new DiagnosticLogger();
        var monitor = new HealthMonitor(logger);
        monitor.UpdateTransportActivity();

        // Wait exactly 2 seconds
        Thread.Sleep(2000);

        // Act
        var health = monitor.GetTransportHealth();

        // Assert - Should be Warning (>= 2 seconds)
        Assert.Equal(HealthStatusLevel.Warning, health.Level);
    }

    /// <summary>
    /// Test edge case: health check exactly at 10 second boundary.
    /// </summary>
    [Fact]
    public void ComponentHealth_AtExact10Seconds_IsCritical()
    {
        // Arrange
        var logger = new DiagnosticLogger();
        var monitor = new HealthMonitor(logger);
        monitor.UpdateTransportActivity();

        // Wait exactly 10 seconds
        Thread.Sleep(10000);

        // Act
        var health = monitor.GetTransportHealth();

        // Assert - Should be Critical (>= 10 seconds)
        Assert.Equal(HealthStatusLevel.Critical, health.Level);
    }
}
