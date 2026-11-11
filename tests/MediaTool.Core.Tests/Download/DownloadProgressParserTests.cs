using FluentAssertions;
using MediaTool.Core.Download;

namespace MediaTool.Core.Tests.Download;

public class DownloadProgressParserTests
{
    [Fact]
    public void Parses_downloaded_and_total_bytes()
    {
        DownloadProgressParser.TryParse("[mt-progress] 1024 4096 NA", out var p).Should().BeTrue();

        p.DownloadedBytes.Should().Be(1024);
        p.TotalBytes.Should().Be(4096);
        p.Percent.Should().Be(25);
    }

    [Fact]
    public void Falls_back_to_estimated_total()
    {
        DownloadProgressParser.TryParse("[mt-progress] 500 NA 1000.5", out var p).Should().BeTrue();

        p.TotalBytes.Should().Be(1000);
        p.Percent.Should().BeApproximately(50, 0.1);
    }

    [Fact]
    public void Unknown_total_gives_no_percent()
    {
        DownloadProgressParser.TryParse("[mt-progress] 500 NA NA", out var p).Should().BeTrue();

        p.TotalBytes.Should().BeNull();
        p.Percent.Should().BeNull();
    }

    [Fact]
    public void Percent_never_exceeds_100()
    {
        DownloadProgressParser.TryParse("[mt-progress] 5000 4000 NA", out var p).Should().BeTrue();

        p.Percent.Should().Be(100);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[download] Destination: media.f136.mp4")]
    [InlineData("[mt-progress]")]
    [InlineData("[mt-progress] abc 100 NA")]
    [InlineData("[mt-progress] -5 100 NA")]
    public void Other_lines_are_ignored(string line)
    {
        DownloadProgressParser.TryParse(line, out _).Should().BeFalse();
    }
}
