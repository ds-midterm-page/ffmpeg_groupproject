using FluentAssertions;
using MediaTool.Core.Metadata;

namespace MediaTool.Core.Tests.Metadata;

public class YtDlpErrorClassifierTests
{
    [Theory]
    [InlineData("private.txt", MediaError.PrivateVideo)]
    [InlineData("removed.txt", MediaError.Removed)]
    [InlineData("unavailable.txt", MediaError.Removed)]
    [InlineData("login_required.txt", MediaError.LoginRequired)]
    [InlineData("age_restricted.txt", MediaError.LoginRequired)]
    [InlineData("network.txt", MediaError.Network)]
    [InlineData("unsupported.txt", MediaError.UnsupportedLink)]
    [InlineData("no_video.txt", MediaError.NoVideo)]
    public void Classifies_saved_ytdlp_errors(string fixture, MediaError expected)
    {
        YtDlpErrorClassifier.Classify(Fixture.Read(Path.Combine("ytdlp_errors", fixture))).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ERROR: something nobody has seen before")]
    public void Unknown_output_is_unknown(string? stderr)
    {
        YtDlpErrorClassifier.Classify(stderr).Should().Be(MediaError.Unknown);
    }

    [Fact]
    public void Every_error_has_a_plain_language_message()
    {
        foreach (var error in Enum.GetValues<MediaError>().Where(e => e != MediaError.None))
        {
            var message = MediaErrorMessages.For(error);
            message.Should().NotBeNullOrWhiteSpace();
            message.Should().NotContainAny("ERROR:", "yt-dlp", "exception", "stderr");
        }
    }
}
