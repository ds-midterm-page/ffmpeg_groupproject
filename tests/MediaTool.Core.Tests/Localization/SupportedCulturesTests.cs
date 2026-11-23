using FluentAssertions;
using MediaTool.Core.Localization;

namespace MediaTool.Core.Tests.Localization;

public class SupportedCulturesTests
{
    [Fact]
    public void Supports_english_russian_and_kazakh_with_english_default()
    {
        SupportedCultures.Codes.Should().Equal("en", "ru", "kk");
        SupportedCultures.Default.Should().Be("en");
    }

    [Theory]
    [InlineData("ru", "ru")]
    [InlineData("RU", "ru")]
    [InlineData("ru-RU", "ru")]
    [InlineData("kk", "kk")]
    [InlineData("kk-KZ", "kk")]
    [InlineData("en-US", "en")]
    [InlineData("de", "en")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    [InlineData("../../etc", "en")]
    [InlineData("ru\r\nSet-Cookie: x", "en")]
    public void Normalizes_any_input_to_a_supported_code(string? input, string expected)
    {
        SupportedCultures.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("en", "English")]
    [InlineData("ru", "Русский")]
    [InlineData("kk", "Қазақша")]
    public void Each_language_is_named_in_itself(string code, string name)
    {
        SupportedCultures.NativeName(code).Should().Be(name);
    }

    [Theory]
    [InlineData("/", "/")]
    [InlineData("/stickers", "/stickers")]
    [InlineData("/stickers?x=1", "/stickers?x=1")]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("https://evil.example/", "/")]
    [InlineData("//evil.example/", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData("javascript:alert(1)", "/")]
    public void Return_address_after_switching_stays_inside_the_app(string? input, string expected)
    {
        SupportedCultures.SafeReturnPath(input).Should().Be(expected);
    }
}
