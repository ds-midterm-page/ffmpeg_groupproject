using FluentAssertions;
using MediaTool.Core.Metadata;

namespace MediaTool.Core.Tests.Metadata;

public class DurationFormatterTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(5, "0:05")]
    [InlineData(213, "3:33")]
    [InlineData(3600, "1:00:00")]
    [InlineData(3725.6, "1:02:05")]
    public void Formats_like_video_players(double seconds, string expected)
    {
        DurationFormatter.Format(TimeSpan.FromSeconds(seconds)).Should().Be(expected);
    }

    [Fact]
    public void Unknown_duration_is_shown_as_dash()
    {
        DurationFormatter.Format(null).Should().Be("—");
    }
}
