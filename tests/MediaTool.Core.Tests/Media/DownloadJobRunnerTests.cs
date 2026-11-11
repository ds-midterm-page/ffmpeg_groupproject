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
    private readonly JobStore _store = new();

    private static readonly QualityOption Hd = new("720p", QualityKind.Video, "136+140", "mp4", 1280, 720, true, 30 * MB, false, false);

    private static DownloadRequest Request(QualityOption? option = null) =>
        new(new Uri("https://youtu.be/abc"), "My: \"video\"", option ?? Hd);

    private DownloadJobRunner Sut => new(_client, _temp, _store, NullLogger<DownloadJobRunner>.Instance);

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
}
