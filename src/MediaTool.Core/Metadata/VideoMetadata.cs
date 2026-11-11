namespace MediaTool.Core.Metadata;

/// <summary>What the preview shows about a video, parsed from yt-dlp's JSON.</summary>
public sealed record VideoMetadata(
    string Id,
    string Title,
    string? Uploader,
    TimeSpan? Duration,
    Uri? ThumbnailUrl,
    Uri? WebpageUrl,
    bool IsLive,
    IReadOnlyList<MediaFormat> Formats);
