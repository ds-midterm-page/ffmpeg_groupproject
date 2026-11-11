using System.Text.RegularExpressions;

namespace MediaTool.Core.Download;

/// <summary>
/// Only plain yt-dlp format ids ("18") or one video+audio pair ("136+140") are allowed, so a selector
/// can never smuggle in options, filters or anything a shell would interpret.
/// </summary>
public static partial class FormatSelector
{
    public static bool IsValid(string? selector) => selector is not null && Pattern().IsMatch(selector);

    public static string EnsureValid(string selector) =>
        IsValid(selector) ? selector : throw new ArgumentException($"Invalid format selector '{selector}'.", nameof(selector));

    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9_.\-]{0,63}(\+[A-Za-z0-9_][A-Za-z0-9_.\-]{0,63})?$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
