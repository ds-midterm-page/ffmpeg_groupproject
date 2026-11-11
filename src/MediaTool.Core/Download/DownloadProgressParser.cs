using System.Globalization;

namespace MediaTool.Core.Download;

/// <summary>
/// Reads the progress lines produced by our <c>--progress-template</c>:
/// "[mt-progress] downloaded total total_estimate", with "NA" for unknown values.
/// </summary>
public static class DownloadProgressParser
{
    public const string Prefix = "[mt-progress]";

    public const string Template =
        Prefix + " %(progress.downloaded_bytes)s %(progress.total_bytes)s %(progress.total_bytes_estimate)s";

    public static bool TryParse(string? line, out DownloadProgress progress)
    {
        progress = new DownloadProgress(0, null);
        if (line is null || !line.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = line[Prefix.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1 || ReadBytes(parts[0]) is not { } downloaded)
        {
            return false;
        }

        var total = (parts.Length > 1 ? ReadBytes(parts[1]) : null) ?? (parts.Length > 2 ? ReadBytes(parts[2]) : null);
        progress = new DownloadProgress(downloaded, total);
        return true;
    }

    private static long? ReadBytes(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d >= 0 ? (long)d : null;
}
