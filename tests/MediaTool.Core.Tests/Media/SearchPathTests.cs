using FluentAssertions;
using MediaTool.Media;

namespace MediaTool.Core.Tests.Media;

public class SearchPathTests
{
    // Folder names without ':' so they work with both Windows (';') and Linux (':') PATH separators.
    private static readonly string Ok1 = Path.Combine("tools", "ok1");
    private static readonly string Ok2 = Path.Combine("tools", "ok2");
    private static readonly string Broken = Path.Combine("trash", "ffmpeg", "bin");

    private static string Join(params string[] dirs) => string.Join(Path.PathSeparator, dirs);

    [Fact]
    public void Keeps_only_usable_directories_in_order()
    {
        var result = SearchPath.KeepUsable(Join(Ok1, Broken, Ok2), dir => dir.StartsWith("tools", StringComparison.Ordinal));

        result.Should().Be(Join(Ok1, Ok2));
    }

    [Fact]
    public void Directory_whose_check_throws_is_dropped()
    {
        var result = SearchPath.KeepUsable(Join(Ok1, Broken), dir => dir.StartsWith("trash", StringComparison.Ordinal)
            ? throw new IOException("The request failed due to a fatal device hardware error")
            : true);

        result.Should().Be(Ok1);
    }

    [Fact]
    public void Empty_entries_and_duplicates_are_removed()
    {
        var sep = Path.PathSeparator;
        var path = $"{Ok1}{sep}{sep}  {sep}{Ok1}{sep}{Ok2}";

        SearchPath.KeepUsable(path, _ => true).Should().Be(Join(Ok1, Ok2));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_path_gives_empty_string(string? path)
    {
        SearchPath.KeepUsable(path, _ => true).Should().BeEmpty();
    }
}
