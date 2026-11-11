using MediaTool.Core.Metadata;

namespace MediaTool.Core.Formats;

/// <summary>
/// Turns the site's format list into a short quality menu: 720p, 480p, 360p (never above 1280 on the long side)
/// plus audio only, each with a size estimate checked against the 50 MB limit.
/// </summary>
public static class FormatPicker
{
    public const int MaxLongSide = 1280;

    /// <summary>Upper bounds for the short side (height for landscape, width for portrait).</summary>
    public static readonly int[] Presets = [720, 480, 360];

    public static IReadOnlyList<QualityOption> GetOptions(VideoMetadata metadata, long maxBytes = SizeLimit.MaxBytes) =>
        GetOptions(metadata, includeSound: true, maxBytes);

    /// <param name="includeSound">
    /// False: picture only. Video-only streams are preferred; a stream that has sound baked in is marked
    /// <see cref="QualityOption.RemoveAudio"/> so ffmpeg strips it after download. No "Audio only" choice.
    /// </param>
    public static IReadOnlyList<QualityOption> GetOptions(VideoMetadata metadata, bool includeSound, long maxBytes = SizeLimit.MaxBytes)
    {
        var options = new List<QualityOption>();
        var bestAudio = PickAudio(metadata.Formats);

        var videos = metadata.Formats
            .Where(f => f is { HasVideo: true, Width: > 0, Height: > 0 } && Math.Max(f.Width!.Value, f.Height!.Value) <= MaxLongSide)
            .ToList();

        foreach (var preset in Presets)
        {
            var fitting = videos.Where(f => ShortSide(f) <= preset).ToList();
            if (fitting.Count == 0)
            {
                continue;
            }

            var bestShort = fitting.Max(ShortSide);
            var sameSize = fitting.Where(f => ShortSide(f) == bestShort);
            var chosen = includeSound
                ? sameSize
                    .Select(f => BuildVideoOption(f, f.HasAudio ? null : bestAudio, maxBytes))
                    .OrderByDescending(c => c.Bitrate)
                    .ThenBy(c => c.Option.EstimatedBytes ?? long.MaxValue)
                    .First()
                    .Option
                : BuildSilentOption(
                    sameSize
                        .OrderBy(f => f.HasAudio) // video-only first: nothing to strip
                        .ThenByDescending(f => f.BitrateKbps ?? 0)
                        .First(),
                    maxBytes);

            if (options.All(o => o.FormatSelector != chosen.FormatSelector))
            {
                options.Add(chosen);
            }
        }

        if (includeSound && bestAudio is not null)
        {
            options.Add(new QualityOption(
                Label: "Audio only",
                Kind: QualityKind.Audio,
                FormatSelector: bestAudio.FormatId,
                Extension: bestAudio.Extension,
                Width: null,
                Height: null,
                HasAudio: true,
                EstimatedBytes: bestAudio.FileSizeBytes,
                IsSizeEstimated: bestAudio.IsSizeEstimated,
                ExceedsLimit: bestAudio.FileSizeBytes is { } a && SizeLimit.IsTooLarge(a, maxBytes)));
        }

        return options;
    }

    private static (QualityOption Option, double Bitrate) BuildVideoOption(MediaFormat video, MediaFormat? audio, long maxBytes)
    {
        long? size = audio is null
            ? video.FileSizeBytes
            : video.FileSizeBytes is { } v && audio.FileSizeBytes is { } a ? v + a : null;
        var estimated = video.IsSizeEstimated || (audio?.IsSizeEstimated ?? false);
        var bitrate = (video.BitrateKbps ?? 0) + (audio?.BitrateKbps ?? 0);

        var option = new QualityOption(
            Label: $"{ShortSide(video)}p",
            Kind: QualityKind.Video,
            FormatSelector: audio is null ? video.FormatId : $"{video.FormatId}+{audio.FormatId}",
            Extension: audio is null ? video.Extension : MergedExtension(video, audio),
            Width: video.Width,
            Height: video.Height,
            HasAudio: video.HasAudio || audio is not null,
            EstimatedBytes: size,
            IsSizeEstimated: estimated,
            ExceedsLimit: size is { } s && SizeLimit.IsTooLarge(s, maxBytes));

        return (option, bitrate);
    }

    private static QualityOption BuildSilentOption(MediaFormat video, long maxBytes) => new(
        Label: $"{ShortSide(video)}p",
        Kind: QualityKind.Video,
        FormatSelector: video.FormatId,
        Extension: video.Extension,
        Width: video.Width,
        Height: video.Height,
        HasAudio: false,
        EstimatedBytes: video.FileSizeBytes,
        // Stripping sound shrinks the file, so the listed size is only an upper estimate.
        IsSizeEstimated: video.IsSizeEstimated || video.HasAudio,
        ExceedsLimit: video.FileSizeBytes is { } s && SizeLimit.IsTooLarge(s, maxBytes),
        RemoveAudio: video.HasAudio);

    /// <summary>m4a first (plays everywhere and merges into mp4), then highest bitrate.</summary>
    private static MediaFormat? PickAudio(IEnumerable<MediaFormat> formats) =>
        formats
            .Where(f => f.IsAudioOnly)
            .OrderByDescending(f => f.Extension.Equals("m4a", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(f => f.BitrateKbps ?? 0)
            .FirstOrDefault();

    private static string MergedExtension(MediaFormat video, MediaFormat audio) =>
        (video.Extension.ToLowerInvariant(), audio.Extension.ToLowerInvariant()) switch
        {
            ("mp4", "m4a") => "mp4",
            ("webm", "webm") => "webm",
            _ => "mkv",
        };

    private static int ShortSide(MediaFormat f) => Math.Min(f.Width!.Value, f.Height!.Value);
}
