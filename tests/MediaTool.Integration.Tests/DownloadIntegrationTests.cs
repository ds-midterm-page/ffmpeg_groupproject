using FFMpegCore;
using FluentAssertions;
using MediaTool.Core.Download;
using MediaTool.Core.Formats;
using MediaTool.Media;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MediaTool.Integration.Tests;

/// <summary>
/// Real network download. Needs yt-dlp (src/MediaTool.App/tools/yt-dlp.exe or MEDIATOOL_YTDLP) and ffmpeg/ffprobe on PATH.
/// Skipped, not failed, when the tools are missing.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DownloadIntegrationTests : IDisposable
{
    // 3:33 music video with an H.264 1280x720 stream (136) and m4a audio (140): about 30 MB together.
    private static readonly Uri VideoUrl = new("https://www.youtube.com/watch?v=dQw4w9WgXcQ");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "MediaTool.IntegrationTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [SkippableFact]
    public async Task Downloads_720p_mp4_and_ffprobe_confirms_it()
    {
        var ytDlp = Tools.FindYtDlp();
        Skip.If(ytDlp is null, "yt-dlp not found: put it in src/MediaTool.App/tools/ or set MEDIATOOL_YTDLP.");
        Skip.IfNot(Tools.OnPath("ffprobe") && Tools.OnPath("ffmpeg"), "ffmpeg/ffprobe not on PATH.");

        var options = Options.Create(new YtDlpOptions { ExecutablePath = ytDlp, MetadataTimeout = TimeSpan.FromMinutes(2) });
        var client = new YtDlpProcessClient(options);
        var metadata = await new MetadataService(client, NullLogger<MetadataService>.Instance).GetMetadataAsync(VideoUrl.AbsoluteUri);
        metadata.IsSuccess.Should().BeTrue(metadata.Message);

        var hd = FormatPicker.GetOptions(metadata.Metadata!).Single(o => o.Label == "720p");
        hd.Extension.Should().Be("mp4");
        hd.ExceedsLimit.Should().BeFalse();

        var store = new JobStore();
        var temp = new TempFileManager(_root);
        var runner = new DownloadJobRunner(client, temp, store, NullLogger<DownloadJobRunner>.Instance);
        var request = new DownloadRequest(VideoUrl, metadata.Metadata!.Title, hd);
        var job = store.Create(request);

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await runner.RunAsync(job.Id, request, cts.Token);

        var done = store.Get(job.Id)!;
        done.Status.Should().Be(JobStatus.Completed, done.Message);
        done.FilePath.Should().EndWith(".mp4");
        done.FileSizeBytes.Should().BeLessThanOrEqualTo(SizeLimit.MaxBytes);
        new FileInfo(done.FilePath!).Length.Should().Be(done.FileSizeBytes);

        var probe = await FFProbe.AnalyseAsync(done.FilePath!);
        probe.PrimaryVideoStream.Should().NotBeNull();
        probe.PrimaryVideoStream!.Height.Should().Be(720);
        probe.PrimaryVideoStream.Width.Should().Be(1280);
        probe.PrimaryVideoStream.CodecName.Should().Be("h264");
        probe.PrimaryAudioStream.Should().NotBeNull("the 720p stream is merged with audio");
        probe.Format.FormatName.Should().Contain("mp4");
        probe.Duration.TotalSeconds.Should().BeApproximately(213, 2);
    }
}

internal static class Tools
{
    public static string? FindYtDlp()
    {
        if (Environment.GetEnvironmentVariable("MEDIATOOL_YTDLP") is { Length: > 0 } fromEnv && File.Exists(fromEnv))
        {
            return fromEnv;
        }

        var exe = OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp";
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "MediaTool.App", "tools", exe);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return OnPath("yt-dlp") ? "yt-dlp" : null;
    }

    public static bool OnPath(string name)
    {
        var exe = OperatingSystem.IsWindows() ? name + ".exe" : name;
        var path = SearchPath.KeepUsable(Environment.GetEnvironmentVariable("PATH"), SearchPath.IsReadableDirectory);
        return path.Split(Path.PathSeparator).Any(dir => File.Exists(Path.Combine(dir, exe)));
    }
}
