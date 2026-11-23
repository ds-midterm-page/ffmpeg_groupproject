using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using MediaTool.Core.Localization;
using MediaTool.Core.Metadata;

namespace MediaTool.Core.Tests.Localization;

/// <summary>Keeps src/MediaTool.App/Resources/SharedResource*.resx complete and in step across languages.</summary>
public partial class TranslationFilesTests
{
    private static Dictionary<string, string> Load(string code)
    {
        var name = code == SupportedCultures.Default ? "SharedResource.resx" : $"SharedResource.{code}.resx";
        var path = Path.Combine(RepoRoot(), "src", "MediaTool.App", "Resources", name);
        File.Exists(path).Should().BeTrue($"{name} should exist");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MediaTool.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("MediaTool.sln not found above the test folder.");
    }

    public static TheoryData<string> Languages => new() { "en", "ru", "kk" };

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_language_has_exactly_the_english_keys(string code)
    {
        Load(code).Keys.Should().BeEquivalentTo(Load("en").Keys);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void No_translation_is_empty(string code)
    {
        Load(code).Where(p => string.IsNullOrWhiteSpace(p.Value)).Select(p => p.Key).Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Placeholders_match_english(string code)
    {
        var english = Load("en");
        foreach (var (key, value) in Load(code))
        {
            Placeholders(value).Should().BeEquivalentTo(Placeholders(english[key]), $"'{key}' in {code} must keep the same {{0}} placeholders");
        }
    }

    [Fact]
    public void Every_user_facing_error_has_a_message()
    {
        var keys = Load("en").Keys;
        foreach (var error in Enum.GetValues<MediaError>().Where(e => e != MediaError.None))
        {
            keys.Should().Contain($"Error.{error}");
        }
    }

    [Theory]
    [InlineData("ru")]
    [InlineData("kk")]
    public void Translations_are_not_just_copied_english(string code)
    {
        var english = Load("en");
        var copied = Load(code).Where(p => p.Value == english[p.Key] && p.Value.Any(char.IsLetter) && !IsUniversal(p.Value)).Select(p => p.Key);

        copied.Should().BeEmpty("every text with words should be translated");
    }

    // Product names and units that stay the same in every language.
    private static bool IsUniversal(string value) => value is "MediaTool" or "MP3" or "MP4" || value.StartsWith("https://", StringComparison.Ordinal);

    private static string[] Placeholders(string value) => PlaceholderPattern().Matches(value).Select(m => m.Value).Order().ToArray();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();
}
