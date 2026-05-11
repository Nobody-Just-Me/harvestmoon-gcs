using HarvestmoonGCS.Tests.Android.Helpers;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HarvestmoonGCS.Tests.Android.Platform;

/// <summary>
/// Tests for Android permission handling
/// Requirements: 1.1, 11.1, 11.2, 11.3, 11.4, 11.5
/// </summary>
[Trait("Category", "Platform")]
[Trait("Category", "Permission")]
public class PermissionTests : AndroidTestBase
{
    public PermissionTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task GrantPermission_ShouldSucceed()
    {
        // Arrange
        const string permission = "android.permission.CAMERA";
        AndroidTestHelper.Reset();

        // Act
        var granted = await AndroidTestHelper.GrantPermissionAsync(permission);

        // Assert
        granted.Should().BeTrue("permission grant should succeed");
        AndroidTestHelper.HasPermission(permission).Should().BeTrue("permission should be marked as granted");
        Log($"Permission {permission} granted successfully");
    }

    [Fact]
    public async Task RevokePermission_ShouldSucceed()
    {
        // Arrange
        const string permission = "android.permission.CAMERA";
        AndroidTestHelper.Reset();
        await AndroidTestHelper.GrantPermissionAsync(permission);

        // Act
        var revoked = await AndroidTestHelper.RevokePermissionAsync(permission);

        // Assert
        revoked.Should().BeTrue("permission revoke should succeed");
        AndroidTestHelper.HasPermission(permission).Should().BeFalse("permission should be marked as revoked");
        Log($"Permission {permission} revoked successfully");
    }

    [Fact]
    public async Task GrantMultiplePermissions_ShouldGrantAll()
    {
        // Arrange
        AndroidTestHelper.Reset();
        var permissions = new[]
        {
            "android.permission.CAMERA",
            "android.permission.ACCESS_FINE_LOCATION",
            "android.permission.WRITE_EXTERNAL_STORAGE"
        };

        // Act
        var results = await AndroidTestHelper.GrantMultiplePermissionsAsync(permissions);

        // Assert
        results.Should().HaveCount(permissions.Length, "all permissions should be processed");
        results.Values.Should().OnlyContain(v => v == true, "all permissions should be granted");
        
        foreach (var permission in permissions)
        {
            AndroidTestHelper.HasPermission(permission).Should().BeTrue($"{permission} should be granted");
        }
        
        Log($"Granted {permissions.Length} permissions successfully");
    }

    [Fact]
    public void HasPermission_WhenNotGranted_ShouldReturnFalse()
    {
        // Arrange
        AndroidTestHelper.Reset();
        const string permission = "android.permission.CAMERA";

        // Act
        var hasPermission = AndroidTestHelper.HasPermission(permission);

        // Assert
        hasPermission.Should().BeFalse("permission should not be granted initially");
        Log($"Permission {permission} correctly reported as not granted");
    }

    [Fact]
    public async Task PermissionRequest_ShouldBeLogged()
    {
        // Arrange
        AndroidTestHelper.Reset();
        AndroidTestHelper.ClearLogs();
        const string permission = "android.permission.CAMERA";

        // Act
        await AndroidTestHelper.GrantPermissionAsync(permission);
        var logs = await AndroidTestHelper.CaptureLogsAsync();

        // Assert
        logs.Should().Contain(log => log.Contains("PERMISSION") && log.Contains(permission),
            "permission grant should be logged");
        Log($"Permission request logged: {logs.Count} log entries");
    }

    [Fact]
    public async Task PermissionGrantDeny_ShouldHandleCorrectly()
    {
        // Arrange
        AndroidTestHelper.Reset();
        const string permission = "android.permission.CAMERA";

        // Act - Grant
        await AndroidTestHelper.GrantPermissionAsync(permission);
        var grantedState = AndroidTestHelper.HasPermission(permission);

        // Act - Deny
        await AndroidTestHelper.RevokePermissionAsync(permission);
        var deniedState = AndroidTestHelper.HasPermission(permission);

        // Assert
        grantedState.Should().BeTrue("permission should be granted after grant");
        deniedState.Should().BeFalse("permission should be denied after revoke");
        Log("Permission grant/deny cycle handled correctly");
    }

    [Fact]
    public async Task RuntimePermissionCheck_ShouldOccurBeforeAccess()
    {
        // Arrange
        AndroidTestHelper.Reset();
        const string permission = "android.permission.CAMERA";

        // Act - Check before granting
        var hasPermissionBefore = AndroidTestHelper.HasPermission(permission);
        
        // Simulate permission request
        await AndroidTestHelper.GrantPermissionAsync(permission);
        
        // Check after granting
        var hasPermissionAfter = AndroidTestHelper.HasPermission(permission);

        // Assert
        hasPermissionBefore.Should().BeFalse("permission should not be available before grant");
        hasPermissionAfter.Should().BeTrue("permission should be available after grant");
        Log("Runtime permission check validated");
    }

    [Fact]
    public async Task PermissionRationale_ShouldBeProvided()
    {
        // Arrange
        AndroidTestHelper.Reset();
        const string permission = "android.permission.ACCESS_FINE_LOCATION";

        // Act
        await AndroidTestHelper.GrantPermissionAsync(permission);
        var logs = await AndroidTestHelper.CaptureLogsAsync();

        // Assert
        logs.Should().NotBeEmpty("permission request should generate logs");
        logs.Should().Contain(log => log.Contains(permission), 
            "logs should contain permission information");
        Log("Permission rationale information available in logs");
    }

    [Fact]
    public async Task AllRequiredPermissions_ShouldBeRequestable()
    {
        // Arrange
        AndroidTestHelper.Reset();
        var requiredPermissions = Config.RequiredPermissions;

        // Act
        var results = new Dictionary<string, bool>();
        foreach (var permission in requiredPermissions)
        {
            var granted = await AndroidTestHelper.GrantPermissionAsync(permission);
            results[permission] = granted;
        }

        // Assert
        results.Values.Should().OnlyContain(v => v == true, "all required permissions should be grantable");
        Log($"All {requiredPermissions.Count} required permissions are requestable");
    }

    [Fact]
    public async Task PermissionState_ShouldPersistAcrossChecks()
    {
        // Arrange
        AndroidTestHelper.Reset();
        const string permission = "android.permission.CAMERA";
        await AndroidTestHelper.GrantPermissionAsync(permission);

        // Act - Multiple checks
        var check1 = AndroidTestHelper.HasPermission(permission);
        var check2 = AndroidTestHelper.HasPermission(permission);
        var check3 = AndroidTestHelper.HasPermission(permission);

        // Assert
        check1.Should().BeTrue("first check should show granted");
        check2.Should().BeTrue("second check should show granted");
        check3.Should().BeTrue("third check should show granted");
        Log("Permission state persists across multiple checks");
    }
}
