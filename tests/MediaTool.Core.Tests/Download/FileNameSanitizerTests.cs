using FluentAssertions;
using MediaTool.Core.Download;

namespace MediaTool.Core.Tests.Download;

public class FileNameSanitizerTests
{
    [Theory]
    [InlineData("Me at the zoo", "Me at the zoo")]
    [InlineData("a/b\\c", "a b c")]
    [InlineData("rm -rf; echo \"hi\" | calc", "rm -rf echo hi calc")]
    [InlineData("..\\..\\Windows\\system32", "Windows system32")]
    [InlineData("tab\there\nnewline\u0007bell", "tab here newline bell")]
    [InlineData("what?<>:*", "what")]
    [InlineData("  dots and spaces . . ", "dots and spaces")]
    [InlineData("Видео 動画 🎬", "Видео 動画 🎬")]
    public void Strips_dangerous_characters(string input, string expected)
    {
        FileNameSanitizer.Sanitize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..")]
    [InlineData("/\\|;\"")]
    public void Empty_result_falls_back_to_default(string? input)
    {
        FileNameSanitizer.Sanitize(input).Should().Be("video");
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("nul")]
    [InlineData("com1")]
    [InlineData("LPT9")]
    public void Windows_reserved_names_are_changed(string input)
    {
        FileNameSanitizer.Sanitize(input).Should().NotBe(input).And.NotBeEmpty();
    }

    [Fact]
    public void Long_titles_are_shortened()
    {
        FileNameSanitizer.Sanitize(new string('a', 500)).Length.Should().Be(FileNameSanitizer.MaxLength);
    }

    [Fact]
    public void Result_never_contains_forbidden_characters()
    {
        var forbidden = new[] { '/', '\\', ';', '"', '|', ':', '*', '?', '<', '>' };
        var nasty = string.Concat(Enumerable.Range(0, 128).Select(i => (char)i)) + "..";

        var result = FileNameSanitizer.Sanitize(nasty);

        result.Should().NotContainAny(forbidden.Select(c => c.ToString()));
        result.Should().NotContain("..");
        result.Any(char.IsControl).Should().BeFalse();
    }
}
