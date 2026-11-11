namespace MediaTool.Core.Tests;

/// <summary>Reads saved yt-dlp outputs from tests/fixtures (copied next to the test assembly).</summary>
internal static class Fixture
{
    public static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", relativePath));
}
