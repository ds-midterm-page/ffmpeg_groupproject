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
}
