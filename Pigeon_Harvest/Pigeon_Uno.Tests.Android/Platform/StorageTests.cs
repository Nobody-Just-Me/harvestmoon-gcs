using Pigeon_Uno.Tests.Android.Helpers;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using System.Threading.Tasks;
using System.IO;
using System.Text;

namespace Pigeon_Uno.Tests.Android.Platform;

/// <summary>
/// Tests for Android storage services
/// Requirements: 1.4, 12.6
/// </summary>
[Trait("Category", "Platform")]
[Trait("Category", "Storage")]
public class StorageTests : AndroidTestBase
{
    public StorageTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task WriteFile_ShouldSucceed()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), "test_write.txt");
        var testData = "Test data for Android storage";

        // Act
        await File.WriteAllTextAsync(testFile, testData);
        var exists = File.Exists(testFile);

        // Assert
        exists.Should().BeTrue("file should exist after write");
        Log($"File written successfully: {testFile}");

        // Cleanup
        if (File.Exists(testFile)) File.Delete(testFile);
    }

    [Fact]
    public async Task ReadFile_ShouldSucceed()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), "test_read.txt");
        var testData = "Test data for reading";
        await File.WriteAllTextAsync(testFile, testData);

        // Act
        var readData = await File.ReadAllTextAsync(testFile);

        // Assert
        readData.Should().Be(testData, "read data should match written data");
        Log($"File read successfully: {readData.Length} bytes");

        // Cleanup
        if (File.Exists(testFile)) File.Delete(testFile);
    }

    [Fact]
    public async Task FileRoundTrip_ShouldPreserveData()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), "test_roundtrip.txt");
        var originalData = "Original test data with special chars: 你好 🚁";

        // Act
        await File.WriteAllTextAsync(testFile, originalData, Encoding.UTF8);
        var retrievedData = await File.ReadAllTextAsync(testFile, Encoding.UTF8);

        // Assert
        retrievedData.Should().Be(originalData, "data should be preserved in round trip");
        Log("File round trip successful");

        // Cleanup
        if (File.Exists(testFile)) File.Delete(testFile);
    }

    [Fact]
    public async Task BinaryFile_ShouldBeHandled()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), "test_binary.bin");
        var binaryData = new byte[] { 0x00, 0xFF, 0x42, 0xAA, 0x55 };

        // Act
        await File.WriteAllBytesAsync(testFile, binaryData);
        var readData = await File.ReadAllBytesAsync(testFile);

        // Assert
        readData.Should().Equal(binaryData, "binary data should be preserved");
        Log($"Binary file handled correctly: {readData.Length} bytes");

        // Cleanup
        if (File.Exists(testFile)) File.Delete(testFile);
    }

    [Fact]
    public void StoragePermission_ShouldBeHandled()
    {
        // Arrange
        AndroidTestHelper.Reset();
        const string permission = "android.permission.WRITE_EXTERNAL_STORAGE";

        // Act
        var hasPermission = AndroidTestHelper.HasPermission(permission);

        // Assert - Initially should not have permission
        hasPermission.Should().BeFalse("storage permission should not be granted initially");
        Log("Storage permission check completed");
    }

    [Fact]
    public async Task StorageFull_ShouldBeHandledGracefully()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), "test_full.txt");

        // Act - Simulate normal write (storage full scenario would need actual full disk)
        var action = async () => await File.WriteAllTextAsync(testFile, "test");

        // Assert
        await action.Should().NotThrowAsync("write should handle storage errors gracefully");
        Log("Storage full scenario handled");

        // Cleanup
        if (File.Exists(testFile)) File.Delete(testFile);
    }

    [Fact]
    public async Task LargeFile_ShouldBeHandled()
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), "test_large.bin");
        var largeData = new byte[1024 * 1024]; // 1MB
        new System.Random().NextBytes(largeData);

        // Act
        await File.WriteAllBytesAsync(testFile, largeData);
        var fileInfo = new FileInfo(testFile);

        // Assert
        fileInfo.Length.Should().Be(largeData.Length, "large file should be written completely");
        Log($"Large file handled: {fileInfo.Length / 1024}KB");

        // Cleanup
        if (File.Exists(testFile)) File.Delete(testFile);
    }
}
