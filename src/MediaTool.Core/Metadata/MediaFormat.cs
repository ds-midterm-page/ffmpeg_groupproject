namespace MediaTool.Core.Metadata;

/// <summary>One downloadable stream offered by the site. Codecs are null when the stream has no such track.</summary>
public sealed record MediaFormat(
    string FormatId,
    string Extension,
    int? Width,
    int? Height,
    double? Fps,
    string? VideoCodec,
    string? AudioCodec,
    double? BitrateKbps,
    long? FileSizeBytes,
    bool IsSizeEstimated)
{
    public bool HasVideo => VideoCodec is not null;

    public bool HasAudio => AudioCodec is not null;

    public bool IsAudioOnly => HasAudio && !HasVideo;

    public bool IsVideoOnly => HasVideo && !HasAudio;
}
