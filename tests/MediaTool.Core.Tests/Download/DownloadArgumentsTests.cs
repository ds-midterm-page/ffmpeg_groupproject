using FluentAssertions;
using MediaTool.Core.Download;
using MediaTool.Core.Metadata;

namespace MediaTool.Core.Tests.Download;

public class DownloadArgumentsTests
{
    private static readonly Uri Url = new("https://www.youtube.com/watch?v=abc");
    private const string Template = @"C:\Temp\MediaTool\job\media.%(ext)s";

    private static IReadOnlyList<string> Build(string selector = "136+140", string? merge = "mp4", string? ffmpeg = null, string? cookies = null) =>
        YtDlpArguments.ForDownload(Url, selector, Template, merge, ffmpeg, cookies, maxBytes: 50L * 1024 * 1024);

    private static string ValueAfter(IReadOnlyList<string> args, string flag)
    {
        var i = args.ToList().IndexOf(flag);
        i.Should().BeGreaterThanOrEqualTo(0, $"{flag} should be present");
        return args[i + 1];
    }

    [Fact]
    public void Passes_format_output_and_limits_as_separate_arguments()
    {
        var args = Build();

        ValueAfter(args, "-f").Should().Be("136+140");
        ValueAfter(args, "-o").Should().Be(Template);
        ValueAfter(args, "--max-filesize").Should().Be("52428800");
        ValueAfter(args, "--merge-output-format").Should().Be("mp4");
        args.Should().Contain(["--no-playlist", "--newline", "--ignore-config", "--no-part"]);
    }

    [Fact]
    public void Reports_progress_in_a_machine_readable_line()
    {
        ValueAfter(Build(), "--progress-template").Should().StartWith("download:" + DownloadProgressParser.Prefix);
    }

    [Fact]
    public void Url_is_last_after_end_of_options_marker()
    {
        var args = Build();

        args[^2].Should().Be("--");
        args[^1].Should().Be(Url.AbsoluteUri);
    }

    [Fact]
    public void Merge_format_omitted_for_single_stream()
    {
        Build(selector: "18", merge: null).Should().NotContain("--merge-output-format");
    }

    [Fact]
    public void Optional_ffmpeg_location_and_cookies()
    {
        Build().Should().NotContain(["--ffmpeg-location", "--cookies"]);

        var args = Build(ffmpeg: @"C:\tools\ffmpeg.exe", cookies: @"C:\me\cookies.txt");
        ValueAfter(args, "--ffmpeg-location").Should().Be(@"C:\tools\ffmpeg.exe");
        ValueAfter(args, "--cookies").Should().Be(@"C:\me\cookies.txt");
    }

    [Theory]
    [InlineData("18")]
    [InlineData("136+140")]
    [InlineData("hls-832")]
    [InlineData("download_addr-0")]
    [InlineData("h264_540p_1100000-0+audio_0")]
    public void Accepts_plain_format_ids(string selector)
    {
        FormatSelector.IsValid(selector).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-exec")]
    [InlineData("--exec calc")]
    [InlineData("18; calc.exe")]
    [InlineData("18|calc")]
    [InlineData("18&calc")]
    [InlineData("\"18\"")]
    [InlineData("best[filesize<5M]")]
    [InlineData("18\ncalc")]
    [InlineData("1+2+3")]
    [InlineData("18+")]
    public void Rejects_anything_that_is_not_a_plain_format_id(string selector)
    {
        FormatSelector.IsValid(selector).Should().BeFalse();
        var act = () => Build(selector: selector);
        act.Should().Throw<ArgumentException>();
    }
}
