namespace MediaTool.Core.Localization;

/// <summary>Interface languages: English (default), Russian and Kazakh.</summary>
public static class SupportedCultures
{
    public const string Default = "en";

    public static readonly IReadOnlyList<string> Codes = ["en", "ru", "kk"];

    /// <summary>Maps any input ("ru-RU", "KK", junk) to one of <see cref="Codes"/>; unknown means English.</summary>
    public static string Normalize(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return Default;
        }

        var language = culture.Trim().Split('-', '_')[0].ToLowerInvariant();
        return Codes.Contains(language) ? language : Default;
    }

    /// <summary>Each language named in itself, as shown in the switcher.</summary>
    public static string NativeName(string code) => Normalize(code) switch
    {
        "ru" => "Русский",
        "kk" => "Қазақша",
        _ => "English",
    };

    /// <summary>Only same-site paths ("/..."), so the language switch can't be used to redirect elsewhere.</summary>
    public static string SafeReturnPath(string? path)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '/' || path.StartsWith("//", StringComparison.Ordinal) || path.StartsWith("/\\", StringComparison.Ordinal))
        {
            return "/";
        }

        return path;
    }
}
