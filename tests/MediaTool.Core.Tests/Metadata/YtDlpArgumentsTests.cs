using FluentAssertions;
using MediaTool.Core.Metadata;

namespace MediaTool.Core.Tests.Metadata;

public class YtDlpArgumentsTests
{
    private static readonly Uri Url = new("https://www.youtube.com/watch?v=abc");

    [Fact]
    public void Metadata_args_dump_single_json_without_playlist()
    {
        var args = YtDlpArguments.ForMetadata(Url, cookiesFile: null);

        args.Should().ContainInOrder("--dump-single-json", "--no-playlist");
        args.Should().Contain("--no-warnings");
        args.Should().NotContain("--cookies");
    }

    [Fact]
    public void Url_is_last_after_end_of_options_marker()
    {
        var args = YtDlpArguments.ForMetadata(Url, cookiesFile: null);

        args[^2].Should().Be("--");
        args[^1].Should().Be(Url.AbsoluteUri);
    }

    [Fact]
    public void Cookies_file_is_passed_as_separate_argument()
    {
        var args = YtDlpArguments.ForMetadata(Url, cookiesFile: @"C:\Users\me\cookies file.txt");

        var i = args.ToList().IndexOf("--cookies");
        i.Should().BeGreaterThanOrEqualTo(0);
        args[i + 1].Should().Be(@"C:\Users\me\cookies file.txt");
    }

    [Fact]
    public void Blank_cookies_file_is_ignored()
    {
        YtDlpArguments.ForMetadata(Url, cookiesFile: "  ").Should().NotContain("--cookies");
    }
}
