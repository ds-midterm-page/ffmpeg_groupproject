using System.Diagnostics;
using FFMpegCore;
using FluentAssertions;
using MediaTool.Media;
using Microsoft.Extensions.Options;

namespace MediaTool.Integration.Tests;

/// <summary>Real ffmpeg on a generated clip (no network). Skipped when ffmpeg/ffprobe are missing.</summary>
[Trait("Category", "Integration")]
public sealed class FfmpegIntegrationTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "MediaTool.IntegrationTests", Guid.NewGuid().ToString("N"));

    public FfmpegIntegrationTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [SkippableFact]
    public async Task Remove_audio_keeps_video_and_drops_sound_in_place()
    {
        Skip.IfNot(Tools.OnPath("ffmpeg") && Tools.OnPath("ffprobe"), "ffmpeg/ffprobe not on PATH.");

        var clip = Path.Combine(_folder, "media.mp4");
        await MakeClipWithSoundAsync(clip);
        (await FFProbe.AnalyseAsync(clip)).PrimaryAudioStream.Should().NotBeNull("the sample clip starts with sound");

        var runner = new FfmpegRunner(Options.Create(new FfmpegOptions()));
        await runner.RemoveAudioAsync(clip, CancellationToken.None);

        var probe = await FFProbe.AnalyseAsync(clip);
        probe.PrimaryAudioStream.Should().BeNull();
        probe.PrimaryVideoStream.Should().NotBeNull();
        probe.PrimaryVideoStream!.Width.Should().Be(320);
        probe.Duration.TotalSeconds.Should().BeApproximately(2, 0.1);
        Directory.GetFiles(_folder).Should().ContainSingle("the temporary silent copy replaces the original");
    }

    private static async Task MakeClipWithSoundAsync(string path)
    {
        var psi = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var a in new[]
        {
            "-hide_banner", "-nostdin", "-y",
            "-f", "lavfi", "-i", "testsrc=size=320x240:rate=25:duration=2",
            "-f", "lavfi", "-i", "sine=frequency=440:duration=2",
            "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", path,
        })
        {
            psi.ArgumentList.Add(a);
        }

        psi.Environment["PATH"] = SearchPath.KeepUsable(psi.Environment["PATH"], SearchPath.IsReadableDirectory);
        using var p = Process.Start(psi)!;
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        p.ExitCode.Should().Be(0, stderr);
    }
}
