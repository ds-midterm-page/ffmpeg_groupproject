using FluentAssertions;
using MediaTool.Core.Download;
using MediaTool.Core.Formats;
using MediaTool.Core.Metadata;
using MediaTool.Media;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MediaTool.Core.Tests.Media;

public class DownloadJobRunnerTests
{
    private const long MB = 1024 * 1024;

    private readonly IYtDlpClient _client = Substitute.For<IYtDlpClient>();
    private readonly ITempFileManager _temp = Substitute.For<ITempFileManager>();
    private readonly IFfmpegRunner _ffmpeg = Substitute.For<IFfmpegRunner>();
    private readonly JobStore _store = new();

    private static readonly QualityOption Hd = new("720p", QualityKind.Video, "136+140", "mp4", 1280, 720, true, 30 * MB, false, false);

    private static DownloadRequest Request(QualityOption? option = null) =>
        new(new Uri("https://youtu.be/abc"), "My: \"video\"", option ?? Hd);

    private DownloadJobRunner Sut => new(_client, _temp, _ffmpeg, _store, NullLogger<DownloadJobRunner>.Instance);

    public DownloadJobRunnerTests()
    {
        _temp.CreateJobFolder(Arg.Any<Guid>()).Returns(ci => Path.Combine(@"C:\Temp\MediaTool", ci.Arg<Guid>().ToString("N")));
    }

    private JobSnapshot Start(DownloadRequest request) => _store.Create(request);

    [Fact]
    public async Task Successful_download_completes_with_file_and_size()
    {
        var job = Start(Request());
        _temp.FindOutput(job.Id).Returns(new OutputFile(@"C:\Temp\MediaTool\x\media.mp4", 30 * MB));

        await Sut.RunAsync(job.Id, Request(), CancellationToken.None);

        var done = _store.Get(job.Id)!;
        done.Status.Should().Be(JobStatus.Completed);
        done.FilePath.Should().Be(@"C:\Temp\MediaTool\x\media.mp4");
        done.FileSizeBytes.Should().Be(30 * MB);
        done.DownloadName.Should().Be("My video.mp4");
        done.Percent.Should().Be(100);
        _temp.DidNotReceive().DeleteJobFolder(job.Id);
    }

    [Fact]
    public async Task Passes_selector_and_job_folder_to_ytdlp()
    {
        var job = Start(Request());
        _temp.FindOutput(job.Id).Returns(new OutputFile("f.mp4", MB));

        await Sut.RunAsync(job.Id, Request(), CancellationToken.None);

        await _client.Received(1).DownloadAsync(
            Arg.Is<DownloadRequest>(r => r.Option.FormatSelector == "136+140"),
            Path.Combine(@"C:\Temp\MediaTool", job.Id.ToString("N")),
            Arg.Any<IProgress<DownloadProgress>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Progress_is_published_to_the_store()
    {
        var job = Start(Request());
        var seen = new List<double?>();
        _store.Changed += s => seen.Add(s.Percent);
        _client.DownloadAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs(ci =>
            {
                ci.Arg<IProgress<DownloadProgress>>().Report(new DownloadProgress(50, 100));
                return Task.CompletedTask;
            });
        _temp.FindOutput(job.Id).Returns(new OutputFile("f.mp4", MB));

        await Sut.RunAsync(job.Id, Request(), CancellationToken.None);

        seen.Should().Contain(50);
    }

    [Fact]
    public async Task Real_size_over_limit_fails_and_deletes_folder()
    {
        var job = Start(Request());
        _temp.FindOutput(job.Id).Returns(new OutputFile("f.mp4", 50 * MB + 1));

        await Sut.RunAsync(job.Id, Request(), CancellationToken.None);

        var failed = _store.Get(job.Id)!;
        failed.Status.Should().Be(JobStatus.Failed);
        failed.Error.Should().Be(MediaError.TooLarge);
        failed.Message.Should().Be(MediaErrorMessages.For(MediaError.TooLarge));
        failed.FilePath.Should().BeNull();
        _temp.Received().DeleteJobFolder(job.Id);
    }

    [Fact]
    public async Task No_output_file_means_ytdlp_skipped_it_for_size()
    {
        var job = Start(Request());
        _temp.FindOutput(job.Id).Returns((OutputFile?)null);

        await Sut.RunAsync(job.Id, Request(), CancellationToken.None);

        _store.Get(job.Id)!.Error.Should().Be(MediaError.TooLarge);
        _temp.Received().DeleteJobFolder(job.Id);
    }

    [Fact]
    public async Task Option_already_over_limit_is_refused_without_downloading()
    {
        var big = Hd with { EstimatedBytes = 80 * MB, ExceedsLimit = true };
        var job = Start(Request(big));

        await Sut.RunAsync(job.Id, Request(big), CancellationToken.None);

        _store.Get(job.Id)!.Error.Should().Be(MediaError.TooLarge);
        await _client.DidNotReceiveWithAnyArgs().DownloadAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Ytdlp_error_is_classified_and_folder_deleted()
    {
        var job = Start(Request());
        _client.DownloadAsync(default!, default!, default!, default)
            .ThrowsAsyncForAnyArgs(new YtDlpException(1, Fixture.Read(Path.Combine("ytdlp_errors", "removed.txt"))));

        await Sut.RunAsync(job.Id, Request(), CancellationToken.None);

        var failed = _store.Get(job.Id)!;
        failed.Status.Should().Be(JobStatus.Failed);
        failed.Error.Should().Be(MediaError.Removed);
        _temp.Received().DeleteJobFolder(job.Id);
    }

    [Fact]
    public async Task Unexpected_exception_becomes_unknown_error()
    {
        var job = Start(Request());
        _client.DownloadAsync(default!, default!, default!, default)
            .ThrowsAsyncForAnyArgs(new IOException("disk full"));

        await Sut.RunAsync(job.Id, Request(), CancellationToken.None);

        var failed = _store.Get(job.Id)!;
        failed.Error.Should().Be(MediaError.Unknown);
        failed.Message.Should().NotContain("disk full");
    }

    [Fact]
    public async Task Cancellation_marks_job_cancelled_and_deletes_folder()
    {
        var job = Start(Request());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _client.DownloadAsync(default!, default!, default!, default)
            .ThrowsAsyncForAnyArgs(new OperationCanceledException(cts.Token));

        await Sut.RunAsync(job.Id, Request(), cts.Token);

        _store.Get(job.Id)!.Status.Should().Be(JobStatus.Cancelled);
        _temp.Received().DeleteJobFolder(job.Id);
    }

    [Fact]
    public async Task Sound_is_removed_when_the_option_asks_for_it()
    {
        var silent = Hd with { FormatSelector = "18", HasAudio = false, RemoveAudio = true };
        var job = Start(Request(silent));
        _temp.FindOutput(job.Id).Returns(
            new OutputFile(@"C:\Temp\MediaTool\x\media.mp4", 20 * MB),
            new OutputFile(@"C:\Temp\MediaTool\x\media.mp4", 18 * MB));

        await Sut.RunAsync(job.Id, Request(silent), CancellationToken.None);

        await _ffmpeg.Received(1).RemoveAudioAsync(@"C:\Temp\MediaTool\x\media.mp4", Arg.Any<CancellationToken>());
        var done = _store.Get(job.Id)!;
        done.Status.Should().Be(JobStatus.Completed);
        done.FileSizeBytes.Should().Be(18 * MB, "the size is re-read after removing sound");
    }

    private static readonly QualityOption Mp3 = new(
        "MP3 192 kbps", QualityKind.Audio, "251", "mp3", null, null, true, 5 * MB, true, false, Mp3BitrateKbps: 192);

    [Fact]
    public async Task Mp3_option_converts_the_download_and_rechecks_size()
    {
        var job = Start(Request(Mp3));
        _temp.FindOutput(job.Id).Returns(
            new OutputFile(@"C:\Temp\MediaTool\x\media.webm", 4 * MB),
            new OutputFile(@"C:\Temp\MediaTool\x\media.mp3", 5 * MB));

        await Sut.RunAsync(job.Id, Request(Mp3), CancellationToken.None);

        await _ffmpeg.Received(1).ConvertToMp3Async(@"C:\Temp\MediaTool\x\media.webm", 192, Arg.Any<CancellationToken>());
        var done = _store.Get(job.Id)!;
        done.Status.Should().Be(JobStatus.Completed);
        done.FilePath.Should().EndWith(".mp3");
        done.DownloadName.Should().Be("My video.mp3");
        done.FileSizeBytes.Should().Be(5 * MB);
    }

    [Fact]
    public async Task Video_options_are_never_converted_to_mp3()
    {
        var job = Start(Request());
        _temp.FindOutput(job.Id).Returns(new OutputFile("f.mp4", MB));

        await Sut.RunAsync(job.Id, Request(), CancellationToken.None);

        await _ffmpeg.DidNotReceiveWithAnyArgs().ConvertToMp3Async(default!, default, default);
    }

    [Fact]
    public async Task Mp3_over_limit_after_conversion_fails()
    {
        var job = Start(Request(Mp3));
        _temp.FindOutput(job.Id).Returns(
            new OutputFile(@"C:\Temp\MediaTool\x\media.webm", 4 * MB),
            new OutputFile(@"C:\Temp\MediaTool\x\media.mp3", 50 * MB + 1));

        await Sut.RunAsync(job.Id, Request(Mp3), CancellationToken.None);

        _store.Get(job.Id)!.Error.Should().Be(MediaError.TooLarge);
        _temp.Received().DeleteJobFolder(job.Id);
    }

    [Fact]
    public async Task Failed_mp3_conversion_is_a_plain_unknown_error()
    {
        var job = Start(Request(Mp3));
        _temp.FindOutput(job.Id).Returns(new OutputFile(@"C:\Temp\MediaTool\x\media.webm", 4 * MB));
        _ffmpeg.ConvertToMp3Async(default!, default, default).ThrowsAsyncForAnyArgs(new FfmpegException(1, "Unknown encoder 'libmp3lame'"));

        await Sut.RunAsync(job.Id, Request(Mp3), CancellationToken.None);

        var failed = _store.Get(job.Id)!;
        failed.Error.Should().Be(MediaError.Unknown);
        failed.Message.Should().NotContain("libmp3lame");
        _temp.Received().DeleteJobFolder(job.Id);
    }

    [Fact]
    public async Task Sound_is_left_alone_for_normal_options()
    {
        var job = Start(Request());
        _temp.FindOutput(job.Id).Returns(new OutputFile("f.mp4", MB));

        await Sut.RunAsync(job.Id, Request(), CancellationToken.None);

        await _ffmpeg.DidNotReceiveWithAnyArgs().RemoveAudioAsync(default!, default);
    }

    [Fact]
    public async Task Failed_sound_removal_fails_the_job_and_deletes_folder()
    {
        var silent = Hd with { FormatSelector = "18", HasAudio = false, RemoveAudio = true };
        var job = Start(Request(silent));
        _temp.FindOutput(job.Id).Returns(new OutputFile(@"C:\Temp\MediaTool\x\media.mp4", 20 * MB));
        _ffmpeg.RemoveAudioAsync(default!, default).ThrowsAsyncForAnyArgs(new FfmpegException(1, "Invalid data found"));

        await Sut.RunAsync(job.Id, Request(silent), CancellationToken.None);

        var failed = _store.Get(job.Id)!;
        failed.Status.Should().Be(JobStatus.Failed);
        failed.Error.Should().Be(MediaError.Unknown);
        failed.Message.Should().NotContain("Invalid data");
        _temp.Received().DeleteJobFolder(job.Id);
    }
}
