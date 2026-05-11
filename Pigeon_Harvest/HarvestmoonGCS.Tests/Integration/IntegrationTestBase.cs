using System;
using Xunit;

namespace HarvestmoonGCS.Tests.Integration;

/// <summary>
/// Base class for integration tests that require external dependencies (SITL, hardware, etc.)
/// These tests are skipped by default unless INTEGRATION_TESTS environment variable is set
/// </summary>
public abstract class IntegrationTestBase
{
    protected const string SkipReason = "Integration test requires SITL or real hardware. Set INTEGRATION_TESTS=1 to run.";
    
    protected static bool ShouldSkipIntegrationTests()
    {
        var integrationTestsEnabled = Environment.GetEnvironmentVariable("INTEGRATION_TESTS");
        return string.IsNullOrEmpty(integrationTestsEnabled) || integrationTestsEnabled != "1";
    }
    
    protected static string? GetSkipReason()
    {
        return ShouldSkipIntegrationTests() ? SkipReason : null;
    }
}
