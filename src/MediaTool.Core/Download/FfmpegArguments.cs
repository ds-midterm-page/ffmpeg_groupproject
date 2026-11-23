namespace MediaTool.Core.Download;

/// <summary>Builds ffmpeg argument lists. Never a shell string: each item reaches ffmpeg as-is.</summary>
public static class FfmpegArguments
{
    /// <summary>Copy the video stream(s) without re-encoding and drop every audio stream.</summary>
    public static IReadOnlyList<string> RemoveAudio(string inputPath, string outputPath)
    {
        RequireFullPath(inputPath, nameof(inputPath));
        RequireFullPath(outputPath, nameof(outputPath));
        if (string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("ffmpeg cannot write over its own input.", nameof(outputPath));
        }

        return
        [
            "-hide_banner",
            "-nostdin",
            "-y",
            "-i", inputPath,
            "-map", "0:v",
            "-c", "copy",
            "-an",
            outputPath,
        ];
    }

    /// <summary>Encode the first audio stream to constant-bitrate MP3, dropping any video.</summary>
    public static IReadOnlyList<string> ToMp3(string inputPath, string outputPath, int kbps)
    {
        RequireFullPath(inputPath, nameof(inputPath));
        RequireFullPath(outputPath, nameof(outputPath));
        if (!AudioQuality.IsOfferedMp3Bitrate(kbps))
        {
            throw new ArgumentException($"Unsupported MP3 bitrate {kbps} kbps.", nameof(kbps));
        }

        if (!Path.GetExtension(outputPath).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("MP3 output must end in .mp3.", nameof(outputPath));
        }

        if (string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("ffmpeg cannot write over its own input.", nameof(outputPath));
        }

        return
        [
            "-hide_banner",
            "-nostdin",
            "-y",
            "-i", inputPath,
            "-map", "0:a:0",
            "-vn",
            "-c:a", "libmp3lame",
            "-b:a", kbps.ToString(System.Globalization.CultureInfo.InvariantCulture) + "k",
            outputPath,
        ];
    }

    // Full paths can never be mistaken for ffmpeg options (they don't start with '-').
    private static void RequireFullPath(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException($"Expected a full file path but got '{path}'.", name);
        }
    }
}
