namespace MediaTool.Core.Validation;

/// <summary>Checks that a pasted link points at a supported site. Pure logic, no network.</summary>
public static class UrlValidator
{
    public const int MaxLength = 2048;

    private const string SupportedSites = "YouTube, TikTok, Instagram or X (Twitter)";

    // Registrable domain -> platform. Subdomains (www., m., vm., music., mobile.) match too.
    private static readonly (string Domain, Platform Platform)[] Hosts =
    [
        ("youtube.com", Platform.YouTube),
        ("youtu.be", Platform.YouTube),
        ("tiktok.com", Platform.TikTok),
        ("instagram.com", Platform.Instagram),
        ("x.com", Platform.X),
        ("twitter.com", Platform.X),
    ];

    public static UrlValidationResult Validate(string? input)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return UrlValidationResult.Fail(UrlValidationError.Empty, "Please paste a link to a video.");
        }

        if (text.Length > MaxLength || text.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return Malformed();
        }

        // Non-technical users often paste "youtube.com/..." without a scheme.
        if (!text.Contains("://", StringComparison.Ordinal) && !text.Contains(':'))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host))
        {
            return Malformed();
        }

        if (!string.IsNullOrEmpty(uri.UserInfo) || uri.HostNameType != UriHostNameType.Dns)
        {
            return Unsupported();
        }

        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        foreach (var (domain, platform) in Hosts)
        {
            if (host == domain || host.EndsWith("." + domain, StringComparison.Ordinal))
            {
                return UrlValidationResult.Ok(uri, platform);
            }
        }

        return Unsupported();
    }

    private static UrlValidationResult Malformed() =>
        UrlValidationResult.Fail(UrlValidationError.Malformed,
            $"That doesn't look like a valid link. Copy the full address of a video from {SupportedSites}.");

    private static UrlValidationResult Unsupported() =>
        UrlValidationResult.Fail(UrlValidationError.UnsupportedSite,
            $"This site isn't supported. Paste a link from {SupportedSites}.");
}
