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

    public static IReadOnlyList<QualityOption> GetOptions(VideoMetadata metadata, bool includeSound, long maxBytes = SizeLimit.MaxBytes)
    {
        if (!includeSound)
        {
            throw new NotImplementedException();
        }

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
            var chosen = fitting
                .Where(f => ShortSide(f) == bestShort)
                .Select(f => BuildVideoOption(f, f.HasAudio ? null : bestAudio, maxBytes))
                .OrderByDescending(c => c.Bitrate)
                .ThenBy(c => c.Option.EstimatedBytes ?? long.MaxValue)
                .First()
                .Option;

            if (options.All(o => o.FormatSelector != chosen.FormatSelector))
            {
                options.Add(chosen);
            }
        }

        if (bestAudio is not null)
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
