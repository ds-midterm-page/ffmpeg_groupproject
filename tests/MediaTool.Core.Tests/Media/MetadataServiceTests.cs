using FluentAssertions;
using MediaTool.Core.Metadata;
using MediaTool.Media;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MediaTool.Core.Tests.Media;

public class MetadataServiceTests
{
    private readonly IYtDlpClient _client = Substitute.For<IYtDlpClient>();

    private MetadataService Sut => new(_client, NullLogger<MetadataService>.Instance);

    [Fact]
    public async Task Returns_parsed_metadata_for_supported_link()
    {
        _client.GetMetadataJsonAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(Fixture.Read("youtube_video.json"));

        var result = await Sut.GetMetadataAsync("youtube.com/watch?v=dQw4w9WgXcQ");

        result.IsSuccess.Should().BeTrue();
        result.Metadata!.Title.Should().Be("Sample Music Video (Official)");
        await _client.Received(1).GetMetadataJsonAsync(
            Arg.Is<Uri>(u => u.AbsoluteUri == "https://youtube.com/watch?v=dQw4w9WgXcQ"), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("https://vimeo.com/1", MediaError.UnsupportedSite)]
    [InlineData("not a link", MediaError.InvalidLink)]
    [InlineData("", MediaError.InvalidLink)]
    public async Task Rejects_bad_links_without_calling_ytdlp(string input, MediaError expected)
    {
        var result = await Sut.GetMetadataAsync(input);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(expected);
        result.Message.Should().NotBeNullOrWhiteSpace();
        await _client.DidNotReceiveWithAnyArgs().GetMetadataJsonAsync(default!, default);
    }

    [Fact]
    public async Task Maps_ytdlp_failure_to_friendly_error()
    {
        _client.GetMetadataJsonAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new YtDlpException(1, Fixture.Read(Path.Combine("ytdlp_errors", "private.txt"))));

        var result = await Sut.GetMetadataAsync("https://youtu.be/abc123");

        result.Error.Should().Be(MediaError.PrivateVideo);
        result.Message.Should().Be(MediaErrorMessages.For(MediaError.PrivateVideo));
    }

    [Fact]
    public async Task Missing_ytdlp_is_reported_as_tool_missing()
    {
        _client.GetMetadataJsonAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new YtDlpNotFoundException("yt-dlp"));

        (await Sut.GetMetadataAsync("https://youtu.be/abc")).Error.Should().Be(MediaError.ToolMissing);
    }

    [Fact]
    public async Task Garbage_output_is_reported_as_unknown()
    {
        _client.GetMetadataJsonAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns("<html>nope</html>");

        (await Sut.GetMetadataAsync("https://youtu.be/abc")).Error.Should().Be(MediaError.Unknown);
    }

    [Fact]
    public async Task Live_streams_are_rejected()
    {
        _client.GetMetadataJsonAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(Fixture.Read("live_stream.json"));

        (await Sut.GetMetadataAsync("https://youtu.be/live1")).Error.Should().Be(MediaError.LiveStream);
    }

    [Fact]
    public async Task Timeout_is_reported_as_network_error()
    {
        _client.GetMetadataJsonAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException());

        (await Sut.GetMetadataAsync("https://youtu.be/abc")).Error.Should().Be(MediaError.Network);
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _client.GetMetadataJsonAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var act = () => Sut.GetMetadataAsync("https://youtu.be/abc", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
