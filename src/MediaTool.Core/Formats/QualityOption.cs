namespace MediaTool.Core.Formats;

public enum QualityKind
{
    Video,
    Audio,
}

/// <summary>One choice in the quality picker, already mapped to a yt-dlp format selector.</summary>
/// <param name="FormatSelector">Exact yt-dlp format ids, e.g. "136+140" or "18".</param>
/// <param name="RemoveAudio">The chosen stream has sound the user doesn't want; strip it with ffmpeg after download.</param>
public sealed record QualityOption(
    string Label,
    QualityKind Kind,
    string FormatSelector,
    string Extension,
    int? Width,
    int? Height,
    bool HasAudio,
    long? EstimatedBytes,
    bool IsSizeEstimated,
    bool ExceedsLimit,
    bool RemoveAudio = false);
