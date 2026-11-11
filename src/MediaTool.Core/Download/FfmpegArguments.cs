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

    // Full paths can never be mistaken for ffmpeg options (they don't start with '-').
    private static void RequireFullPath(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException($"Expected a full file path but got '{path}'.", name);
        }
    }
}
