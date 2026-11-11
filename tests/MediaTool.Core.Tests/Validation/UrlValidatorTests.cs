using FluentAssertions;
using MediaTool.Core.Validation;

namespace MediaTool.Core.Tests.Validation;

public class UrlValidatorTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", Platform.YouTube)]
    [InlineData("https://youtube.com/shorts/abc123", Platform.YouTube)]
    [InlineData("https://m.youtube.com/watch?v=abc", Platform.YouTube)]
    [InlineData("https://music.youtube.com/watch?v=abc", Platform.YouTube)]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", Platform.YouTube)]
    [InlineData("https://www.tiktok.com/@user/video/1234567890", Platform.TikTok)]
    [InlineData("https://vm.tiktok.com/ZMabc/", Platform.TikTok)]
    [InlineData("https://www.instagram.com/reel/Cxyz/", Platform.Instagram)]
    [InlineData("https://x.com/user/status/123", Platform.X)]
    [InlineData("https://twitter.com/user/status/123", Platform.X)]
    [InlineData("https://mobile.twitter.com/user/status/123", Platform.X)]
    [InlineData("http://youtube.com/watch?v=abc", Platform.YouTube)]
    public void Supported_links_are_valid(string url, Platform expected)
    {
        var result = UrlValidator.Validate(url);

        result.IsValid.Should().BeTrue();
        result.Platform.Should().Be(expected);
        result.Error.Should().Be(UrlValidationError.None);
        result.Uri.Should().NotBeNull();
    }

    [Theory]
    [InlineData("  https://youtu.be/abc  ")]
    [InlineData("HTTPS://WWW.YOUTUBE.COM/watch?v=abc")]
    [InlineData("https://www.youtube.com./watch?v=abc")]
    public void Whitespace_case_and_trailing_dot_are_tolerated(string url)
    {
        UrlValidator.Validate(url).Platform.Should().Be(Platform.YouTube);
    }

    [Theory]
    [InlineData("youtube.com/watch?v=abc", Platform.YouTube)]
    [InlineData("www.tiktok.com/@u/video/1", Platform.TikTok)]
    public void Links_without_scheme_get_https(string url, Platform expected)
    {
        var result = UrlValidator.Validate(url);

        result.IsValid.Should().BeTrue();
        result.Platform.Should().Be(expected);
        result.Uri!.Scheme.Should().Be("https");
    }

    [Theory]
    [InlineData("https://vimeo.com/123")]
    [InlineData("https://example.com/watch?v=abc")]
    [InlineData("https://youtube.com.evil.com/watch?v=abc")]
    [InlineData("https://evilyoutube.com/watch?v=abc")]
    [InlineData("https://notx.com/status/1")]
    [InlineData("https://youtube.com@evil.com/watch?v=abc")]
    [InlineData("https://127.0.0.1/watch?v=abc")]
    public void Other_sites_are_unsupported(string url)
    {
        var result = UrlValidator.Validate(url);

        result.IsValid.Should().BeFalse();
        result.Platform.Should().BeNull();
        result.Error.Should().Be(UrlValidationError.UnsupportedSite);
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("not a link")]
    [InlineData("https://")]
    [InlineData("ftp://youtube.com/video")]
    [InlineData("file:///C:/Windows/system32")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://you tube.com/watch")]
    public void Malformed_input_is_rejected(string url)
    {
        var result = UrlValidator.Validate(url);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(UrlValidationError.Malformed);
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_input_is_rejected(string? url)
    {
        var result = UrlValidator.Validate(url);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(UrlValidationError.Empty);
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Very_long_input_is_rejected_as_malformed()
    {
        var url = "https://youtube.com/watch?v=" + new string('a', 5000);

        UrlValidator.Validate(url).Error.Should().Be(UrlValidationError.Malformed);
    }

    [Fact]
    public void Error_messages_are_plain_language()
    {
        UrlValidator.Validate("https://vimeo.com/1").Message.Should()
            .Contain("YouTube").And.Contain("TikTok").And.Contain("Instagram").And.Contain("X");
    }
}
