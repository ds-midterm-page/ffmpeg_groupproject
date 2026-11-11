using FluentAssertions;
using MediaTool.Media;

namespace MediaTool.Core.Tests.Media;

public class SearchPathTests
{
    [Fact]
    public void Keeps_only_usable_directories_in_order()
    {
        var path = string.Join(Path.PathSeparator, @"C:\ok1", @"D:\broken", @"C:\ok2");

        var result = SearchPath.KeepUsable(path, dir => dir.StartsWith("C:", StringComparison.Ordinal));

        result.Should().Be(string.Join(Path.PathSeparator, @"C:\ok1", @"C:\ok2"));
    }

    [Fact]
    public void Directory_whose_check_throws_is_dropped()
    {
        var path = string.Join(Path.PathSeparator, @"C:\ok", @"D:\trash\ffmpeg\bin");

        var result = SearchPath.KeepUsable(path, dir => dir.StartsWith("D:", StringComparison.Ordinal)
            ? throw new IOException("The request failed due to a fatal device hardware error")
            : true);

        result.Should().Be(@"C:\ok");
    }

    [Fact]
    public void Empty_entries_and_duplicates_are_removed()
    {
        var sep = Path.PathSeparator;
        var path = $@"C:\a{sep}{sep}  {sep}C:\a{sep}C:\b";

        SearchPath.KeepUsable(path, _ => true).Should().Be($@"C:\a{sep}C:\b");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_path_gives_empty_string(string? path)
    {
        SearchPath.KeepUsable(path, _ => true).Should().BeEmpty();
    }
}
