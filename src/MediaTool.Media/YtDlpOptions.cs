namespace MediaTool.Media;

/// <summary>Bound from the "YtDlp" section of appsettings.json.</summary>
public sealed class YtDlpOptions
{
    public const string SectionName = "YtDlp";

    /// <summary>Full path to yt-dlp. Empty: use tools\yt-dlp.exe next to the app, else "yt-dlp" from PATH.</summary>
    public string? ExecutablePath { get; set; }

    /// <summary>Optional Netscape cookies file, only sent for Instagram and X links.</summary>
    public string? CookiesFile { get; set; }

    public TimeSpan MetadataTimeout { get; set; } = TimeSpan.FromSeconds(60);
}
