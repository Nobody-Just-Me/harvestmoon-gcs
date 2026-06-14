using FluentAssertions;
using HarvestmoonGCS.Core.Services;
using Xunit;

namespace HarvestmoonGCS.Tests.Services;

public class VideoRecorderServiceTests
{
    [Fact]
    public async Task WriteFrame_WhenRecording_ShouldCreateManifestAndFrameFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "harvestmoon-recorder-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var outputPath = Path.Combine(tempRoot, "mission_recording.mp4");
        var service = new VideoRecorderService();

        var started = await service.StartRecordingAsync(outputPath, width: 640, height: 480, fps: 15);
        var wrote = service.WriteFrame(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
        await service.StopRecordingAsync();

        started.Should().BeTrue();
        wrote.Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();
        File.ReadAllText(outputPath + ".frames.txt").Should().Contain("frame_000001.jpg");
        Directory.GetFiles(tempRoot, "frame_000001.jpg", SearchOption.AllDirectories).Should().HaveCount(1);

        Directory.Delete(tempRoot, recursive: true);
    }
}
