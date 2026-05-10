using Pigeon_Uno.Core.Models;
using Pigeon_Uno.Core.Services;
using Pigeon_Uno.Core.ViewModels;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Pigeon_Uno.Tests.UIConversion;

/// <summary>
/// Tests for Task 3.1.2: Populate DeviceList ListView
/// Verifies that the CompassDevices collection is properly populated
/// and can be bound to the UI ListView
/// </summary>
public class CompassDeviceListPopulationTests
{
    private readonly Mock<IMavLinkService> _mockMavLinkService;
    private readonly CalibrationViewModel _viewModel;

    public CompassDeviceListPopulationTests()
    {
        _mockMavLinkService = new Mock<IMavLinkService>();
        _viewModel = new CalibrationViewModel(_mockMavLinkService.Object);
    }

    [Fact]
    [Trait("Category", "UIConversion")]
    [Trait("Category", "Task3.1.2")]
    public void CompassDevices_Collection_IsInitialized()
    {
        // Assert - CompassDevices collection should be initialized and empty
        Assert.NotNull(_viewModel.CompassDevices);
        Assert.Empty(_viewModel.CompassDevices);
    }

    [Fact]
    [Trait("Category", "UIConversion")]
    [Trait("Category", "Task3.1.2")]
    public void CompassDevices_Collection_CanAddDevices()
    {
        // Arrange
        var device1 = new CompassDevice { Number = 1, DevID = 73225, DevType = "HMC5883" };
        var device2 = new CompassDevice { Number = 2, DevID = 73226, DevType = "LSM303D" };

        // Act
        _viewModel.CompassDevices.Add(device1);
        _viewModel.CompassDevices.Add(device2);

        // Assert
        Assert.Equal(2, _viewModel.CompassDevices.Count);
        Assert.Contains(_viewModel.CompassDevices, d => d.Number == 1 && d.DevID == 73225);
        Assert.Contains(_viewModel.CompassDevices, d => d.Number == 2 && d.DevID == 73226);
    }

    [Fact]
    [Trait("Category", "UIConversion")]
    [Trait("Category", "Task3.1.2")]
    public async Task RefreshCompassListAsync_PopulatesDeviceList_WithValidParameters()
    {
        // Arrange - Setup mock to return compass parameters
        var parameters = new Dictionary<string, float>
        {
            { "COMPASS_DEV_ID", 73225 },
            { "COMPASS_DEV_ID2", 73226 },
            { "COMPASS_DEV_ID3", 73227 }
        };

        _mockMavLinkService
            .Setup(m => m.RequestParametersAsync())
            .Returns(Task.CompletedTask);

        _mockMavLinkService
            .Setup(m => m.GetParametersAsync())
            .ReturnsAsync(parameters);

        // Act
        await _viewModel.RefreshCompassListAsync();

        // Assert - DeviceList should be populated with 3 devices
        Assert.Equal(3, _viewModel.CompassDevices.Count);
        
        // Verify device 1
        var device1 = _viewModel.CompassDevices.FirstOrDefault(d => d.Number == 1);
        Assert.NotNull(device1);
        Assert.Equal(73225, device1.DevID);
        Assert.NotEmpty(device1.DevType);
        
        // Verify device 2
        var device2 = _viewModel.CompassDevices.FirstOrDefault(d => d.Number == 2);
        Assert.NotNull(device2);
        Assert.Equal(73226, device2.DevID);
        Assert.NotEmpty(device2.DevType);
        
        // Verify device 3
        var device3 = _viewModel.CompassDevices.FirstOrDefault(d => d.Number == 3);
        Assert.NotNull(device3);
        Assert.Equal(73227, device3.DevID);
        Assert.NotEmpty(device3.DevType);
    }

    [Fact]
    [Trait("Category", "UIConversion")]
    [Trait("Category", "Task3.1.2")]
    public async Task RefreshCompassListAsync_ClearsExistingDevices_BeforePopulating()
    {
        // Arrange - Add some existing devices
        _viewModel.CompassDevices.Add(new CompassDevice { Number = 1, DevID = 99999, DevType = "Old" });
        _viewModel.CompassDevices.Add(new CompassDevice { Number = 2, DevID = 88888, DevType = "Old" });
        
        Assert.Equal(2, _viewModel.CompassDevices.Count);

        // Setup mock to return new compass parameters
        var parameters = new Dictionary<string, float>
        {
            { "COMPASS_DEV_ID", 73225 }
        };

        _mockMavLinkService
            .Setup(m => m.RequestParametersAsync())
            .Returns(Task.CompletedTask);

        _mockMavLinkService
            .Setup(m => m.GetParametersAsync())
            .ReturnsAsync(parameters);

        // Act
        await _viewModel.RefreshCompassListAsync();

        // Assert - Old devices should be cleared, only new device should exist
        Assert.Single(_viewModel.CompassDevices);
        Assert.Equal(1, _viewModel.CompassDevices[0].Number);
        Assert.Equal(73225, _viewModel.CompassDevices[0].DevID);
        Assert.DoesNotContain(_viewModel.CompassDevices, d => d.DevID == 99999);
        Assert.DoesNotContain(_viewModel.CompassDevices, d => d.DevID == 88888);
    }

    [Fact]
    [Trait("Category", "UIConversion")]
    [Trait("Category", "Task3.1.2")]
    public async Task RefreshCompassListAsync_UsesDefaultDevices_WhenNoParametersFound()
    {
        // Arrange - Setup mock to return empty parameters
        var parameters = new Dictionary<string, float>();

        _mockMavLinkService
            .Setup(m => m.RequestParametersAsync())
            .Returns(Task.CompletedTask);

        _mockMavLinkService
            .Setup(m => m.GetParametersAsync())
            .ReturnsAsync(parameters);

        // Act
        await _viewModel.RefreshCompassListAsync();

        // Assert - Should have default devices (2 devices as per implementation)
        Assert.Equal(2, _viewModel.CompassDevices.Count);
        Assert.All(_viewModel.CompassDevices, device =>
        {
            Assert.True(device.Number > 0);
            Assert.True(device.DevID > 0);
            Assert.NotEmpty(device.DevType);
        });
    }

    [Fact]
    [Trait("Category", "UIConversion")]
    [Trait("Category", "Task3.1.2")]
    public void CompassDevice_Properties_AreCorrectlySet()
    {
        // Arrange & Act
        var device = new CompassDevice
        {
            Number = 1,
            DevID = 73225,
            DevType = "HMC5883"
        };

        // Assert - All properties should be set correctly
        Assert.Equal(1, device.Number);
        Assert.Equal(73225, device.DevID);
        Assert.Equal("HMC5883", device.DevType);
    }

    [Fact]
    [Trait("Category", "UIConversion")]
    [Trait("Category", "Task3.1.2")]
    public async Task RefreshCompassListAsync_IgnoresZeroDeviceIds()
    {
        // Arrange - Setup mock with some zero device IDs
        var parameters = new Dictionary<string, float>
        {
            { "COMPASS_DEV_ID", 73225 },
            { "COMPASS_DEV_ID2", 0 },      // Zero ID should be ignored
            { "COMPASS_DEV_ID3", 73227 }
        };

        _mockMavLinkService
            .Setup(m => m.RequestParametersAsync())
            .Returns(Task.CompletedTask);

        _mockMavLinkService
            .Setup(m => m.GetParametersAsync())
            .ReturnsAsync(parameters);

        // Act
        await _viewModel.RefreshCompassListAsync();

        // Assert - Only non-zero devices should be added
        Assert.Equal(2, _viewModel.CompassDevices.Count);
        Assert.Contains(_viewModel.CompassDevices, d => d.Number == 1 && d.DevID == 73225);
        Assert.Contains(_viewModel.CompassDevices, d => d.Number == 3 && d.DevID == 73227);
        Assert.DoesNotContain(_viewModel.CompassDevices, d => d.DevID == 0);
    }

    [Fact]
    [Trait("Category", "UIConversion")]
    [Trait("Category", "Task3.1.2")]
    public async Task RefreshCompassListAsync_ParsesDeviceType_Correctly()
    {
        // Arrange - Setup mock with known device IDs
        var parameters = new Dictionary<string, float>
        {
            { "COMPASS_DEV_ID", 73225 },  // Should parse to HMC5883 or similar
            { "COMPASS_DEV_ID2", 73226 }
        };

        _mockMavLinkService
            .Setup(m => m.RequestParametersAsync())
            .Returns(Task.CompletedTask);

        _mockMavLinkService
            .Setup(m => m.GetParametersAsync())
            .ReturnsAsync(parameters);

        // Act
        await _viewModel.RefreshCompassListAsync();

        // Assert - Device types should be parsed and not empty
        Assert.All(_viewModel.CompassDevices, device =>
        {
            Assert.NotNull(device.DevType);
            Assert.NotEmpty(device.DevType);
            // Device type should be a recognizable compass type or "Unknown"
            Assert.True(
                device.DevType.Contains("HMC") || 
                device.DevType.Contains("AK") || 
                device.DevType.Contains("LSM") || 
                device.DevType.Contains("Unknown"),
                $"Device type '{device.DevType}' should be a valid compass type"
            );
        });
    }
}
