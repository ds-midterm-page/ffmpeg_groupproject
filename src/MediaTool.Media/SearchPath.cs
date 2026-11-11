namespace MediaTool.Media;

/// <summary>
/// Cleans the PATH handed to child processes. yt-dlp scans every PATH folder for ffmpeg and crashes
/// if one is unreadable (e.g. a failing or disconnected drive), so such folders are dropped first.
/// </summary>
public static class SearchPath
{
    public static string KeepUsable(string? path, Func<string, bool> isUsable)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var kept = new List<string>();
        foreach (var raw in path.Split(Path.PathSeparator))
        {
            var dir = raw.Trim();
            if (dir.Length == 0 || !seen.Add(dir))
            {
                continue;
            }

            bool usable;
            try
            {
                usable = isUsable(dir);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                usable = false;
            }

            if (usable)
            {
                kept.Add(dir);
            }
        }

        return string.Join(Path.PathSeparator, kept);
    }

    /// <summary>True when the folder exists and its contents can be listed.</summary>
    public static bool IsReadableDirectory(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return false;
        }

        using var entries = Directory.EnumerateFileSystemEntries(dir).GetEnumerator();
        _ = entries.MoveNext();
        return true;
    }
}
