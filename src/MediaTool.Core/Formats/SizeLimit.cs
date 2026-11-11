using System.Globalization;

namespace MediaTool.Core.Formats;

public static class SizeLimit
{
    /// <summary>50 MB: the largest file the app will produce.</summary>
    public const long MaxBytes = 50L * 1024 * 1024;

    public static bool IsTooLarge(long bytes, long maxBytes = MaxBytes) => bytes > maxBytes;
}

public static class SizeFormatter
{
    private const double KB = 1024;
    private const double MB = KB * 1024;
    private const double GB = MB * 1024;

    public static string Format(long? bytes) => bytes switch
    {
        null => "size unknown",
        < (long)MB => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(bytes.Value / KB):0} KB"),
        < (long)GB => string.Create(CultureInfo.InvariantCulture, $"{bytes.Value / MB:0.0} MB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes.Value / GB:0.0} GB"),
    };
}
