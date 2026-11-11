using System.Text;

namespace MediaTool.Core.Download;

/// <summary>Turns a video title into a safe file name (no extension).</summary>
public static class FileNameSanitizer
{
    public const int MaxLength = 100;
    public const string Fallback = "video";

    // Path separators, ';', '"', '|' (project rule) plus the other characters Windows forbids in file names.
    private static readonly HashSet<char> Forbidden = ['/', '\\', ';', '"', '|', ':', '*', '?', '<', '>'];

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Fallback;
        }

        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            // Unsafe characters become spaces so words stay apart; runs of spaces are collapsed below.
            sb.Append(char.IsControl(c) || Forbidden.Contains(c) ? ' ' : c);
        }

        var text = sb.ToString();
        while (text.Contains("..", StringComparison.Ordinal))
        {
            text = text.Replace("..", " ", StringComparison.Ordinal);
        }

        // Collapse whitespace and trim spaces/dots from the ends (Windows strips trailing dots).
        text = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim(' ', '.');
        text = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w != "."));

        if (text.Length > MaxLength)
        {
            text = text[..MaxLength].TrimEnd(' ', '.');
        }

        if (text.Length == 0)
        {
            return Fallback;
        }

        return Reserved.Contains(text) ? text + "_" : text;
    }
}
