namespace MediaTool.Core.Metadata;

/// <summary>Maps yt-dlp's stderr text to an error the user can understand.</summary>
public static class YtDlpErrorClassifier
{
    // Checked in order: the first matching rule wins.
    private static readonly (MediaError Error, string[] Phrases)[] Rules =
    [
        (MediaError.PrivateVideo, ["private video", "this video is private", "this account is private"]),
        (MediaError.LoginRequired, ["login required", "sign in to confirm", "log in", "--cookies", "age-restricted", "rate-limit"]),
        (MediaError.Removed, ["has been removed", "video unavailable", "no longer available", "account has been terminated", "does not exist", "http error 404", "not found"]),
        (MediaError.NoVideo, ["no video could be found", "no video formats found", "there's no video"]),
        (MediaError.UnsupportedLink, ["unsupported url"]),
        (MediaError.Network, ["unable to download webpage", "getaddrinfo failed", "timed out", "connection reset", "temporary failure in name resolution", "urlopen error", "network is unreachable"]),
    ];

    public static MediaError Classify(string? stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return MediaError.Unknown;
        }

        foreach (var (error, phrases) in Rules)
        {
            if (phrases.Any(p => stderr.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                return error;
            }
        }

        return MediaError.Unknown;
    }
}
