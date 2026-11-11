using System.Text.Json;

namespace MediaTool.Core.Metadata;

/// <summary>Turns yt-dlp's <c>--dump-single-json</c> output into <see cref="VideoMetadata"/>. Pure, no I/O.</summary>
public static class YtDlpMetadataParser
{
    public const string UntitledVideo = "Untitled video";

    public static VideoMetadata Parse(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new MetadataParseException("yt-dlp output is not valid JSON.", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new MetadataParseException($"Expected a JSON object but got {root.ValueKind}.");
            }

            var duration = GetDouble(root, "duration") is { } seconds && seconds >= 0
                ? TimeSpan.FromSeconds(seconds)
                : (TimeSpan?)null;

            return new VideoMetadata(
                Id: GetString(root, "id") ?? string.Empty,
                Title: GetString(root, "title") is { Length: > 0 } title ? title : UntitledVideo,
                Uploader: GetString(root, "uploader"),
                Duration: duration,
                ThumbnailUrl: PickThumbnail(root),
                WebpageUrl: ToHttpUri(GetString(root, "webpage_url")),
                IsLive: root.TryGetProperty("is_live", out var live) && live.ValueKind == JsonValueKind.True,
                Formats: ParseFormats(root, duration));
        }
    }

    private static List<MediaFormat> ParseFormats(JsonElement root, TimeSpan? duration)
    {
        var result = new List<MediaFormat>();
        if (!root.TryGetProperty("formats", out var formats) || formats.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var f in formats.EnumerateArray())
        {
            if (f.ValueKind != JsonValueKind.Object || GetString(f, "format_id") is not { Length: > 0 } id)
            {
                continue;
            }

            var videoCodec = Codec(GetString(f, "vcodec"));
            var audioCodec = Codec(GetString(f, "acodec"));
            if (videoCodec is null && audioCodec is null)
            {
                continue; // storyboards / thumbnails-as-formats
            }

            var bitrate = GetDouble(f, "tbr");
            var (size, estimated) = EstimateSize(f, bitrate, duration);

            result.Add(new MediaFormat(
                FormatId: id,
                Extension: GetString(f, "ext") ?? string.Empty,
                Width: GetInt(f, "width"),
                Height: GetInt(f, "height"),
                Fps: GetDouble(f, "fps"),
                VideoCodec: videoCodec,
                AudioCodec: audioCodec,
                BitrateKbps: bitrate,
                FileSizeBytes: size,
                IsSizeEstimated: estimated));
        }

        return result;
    }

    private static (long? Size, bool Estimated) EstimateSize(JsonElement f, double? bitrateKbps, TimeSpan? duration)
    {
        if (GetLong(f, "filesize") is { } exact)
        {
            return (exact, false);
        }

        if (GetLong(f, "filesize_approx") is { } approx)
        {
            return (approx, true);
        }

        if (bitrateKbps is > 0 && duration is { } d)
        {
            return ((long)(bitrateKbps.Value * 1000 / 8 * d.TotalSeconds), true);
        }

        return (null, false);
    }

    private static Uri? PickThumbnail(JsonElement root)
    {
        if (ToHttpUri(GetString(root, "thumbnail")) is { } direct)
        {
            return direct;
        }

        // Fallback: first usable entry in "thumbnails" (yt-dlp lists preferred ones without negative preference first).
        if (root.TryGetProperty("thumbnails", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in list.EnumerateArray())
            {
                if (t.ValueKind == JsonValueKind.Object && ToHttpUri(GetString(t, "url")) is { } uri)
                {
                    return uri;
                }
            }
        }

        return null;
    }

    /// <summary>Only http(s) URLs are allowed to reach the page (e.g. as an img src).</summary>
    private static Uri? ToHttpUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri
            : null;

    private static string? Codec(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : value;

    private static string? GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double? GetDouble(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) ? d : null;

    private static long? GetLong(JsonElement e, string name) =>
        GetDouble(e, name) is { } d && d >= 0 ? (long)d : null;

    private static int? GetInt(JsonElement e, string name) =>
        GetDouble(e, name) is { } d && d is >= 0 and <= int.MaxValue ? (int)d : null;
}
