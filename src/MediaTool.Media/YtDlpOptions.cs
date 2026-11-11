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

    public TimeSpan DownloadTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Folder or path of ffmpeg for merging. Empty: tools\ffmpeg.exe next to the app if present, else yt-dlp searches PATH.</summary>
    public string? FfmpegLocation { get; set; }
}
