namespace MediaTool.Core.Validation;

/// <summary>Outcome of checking a user-pasted link. <see cref="Message"/> is safe to show to the user.</summary>
public sealed record UrlValidationResult(Uri? Uri, Platform? Platform, UrlValidationError Error, string? Message)
{
    public bool IsValid => Error == UrlValidationError.None;

    public static UrlValidationResult Ok(Uri uri, Platform platform) => new(uri, platform, UrlValidationError.None, null);

    public static UrlValidationResult Fail(UrlValidationError error, string message) => new(null, null, error, message);
}
