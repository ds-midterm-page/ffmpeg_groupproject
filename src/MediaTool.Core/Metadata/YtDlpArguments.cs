using System.Globalization;
using MediaTool.Core.Download;

namespace MediaTool.Core.Metadata;

/// <summary>Builds yt-dlp argument lists. Never a shell string: each item is passed to the process as-is.</summary>
public static class YtDlpArguments
{
    public static IReadOnlyList<string> ForMetadata(Uri url, string? cookiesFile)
    {
        var args = new List<string>
        {
            "--dump-single-json",
            "--no-playlist",
            "--no-warnings",
            "--no-progress",
            "--ignore-config",
        };

        if (!string.IsNullOrWhiteSpace(cookiesFile))
        {
            args.Add("--cookies");
            args.Add(cookiesFile);
        }

        // "--" ends option parsing, so the URL can never be read as a flag.
        args.Add("--");
        args.Add(url.AbsoluteUri);
        return args;
    }

    public static IReadOnlyList<string> ForDownload(
        Uri url,
        string formatSelector,
        string outputTemplate,
        string? mergeExtension,
        string? ffmpegLocation,
        string? cookiesFile,
        long maxBytes)
    {
        var args = new List<string>
        {
            "-f", FormatSelector.EnsureValid(formatSelector),
            "--no-playlist",
            "--no-warnings",
            "--ignore-config",
            "--no-part",
            "--no-mtime",
            "--newline",
            "--progress-template", "download:" + DownloadProgressParser.Template,
            "--max-filesize", maxBytes.ToString(CultureInfo.InvariantCulture),
            "-o", outputTemplate,
        };

        if (!string.IsNullOrWhiteSpace(mergeExtension))
        {
            args.Add("--merge-output-format");
            args.Add(mergeExtension);
        }

        if (!string.IsNullOrWhiteSpace(ffmpegLocation))
        {
            args.Add("--ffmpeg-location");
            args.Add(ffmpegLocation);
        }

        if (!string.IsNullOrWhiteSpace(cookiesFile))
        {
            args.Add("--cookies");
            args.Add(cookiesFile);
        }

        args.Add("--");
        args.Add(url.AbsoluteUri);
        return args;
    }
}
