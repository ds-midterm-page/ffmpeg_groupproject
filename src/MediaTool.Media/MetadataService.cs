using MediaTool.Core.Metadata;
using MediaTool.Core.Validation;
using Microsoft.Extensions.Logging;

namespace MediaTool.Media;

public sealed record MetadataResult(VideoMetadata? Metadata, MediaError Error, string? Message)
{
    public bool IsSuccess => Error == MediaError.None;

    public static MetadataResult Ok(VideoMetadata metadata) => new(metadata, MediaError.None, null);

    public static MetadataResult Fail(MediaError error, string? message = null) =>
        new(null, error, message ?? MediaErrorMessages.For(error));
}

/// <summary>Validates a pasted link, asks yt-dlp about it and returns a preview or a plain-language error.</summary>
public sealed class MetadataService(IYtDlpClient client, ILogger<MetadataService> logger)
{
    public async Task<MetadataResult> GetMetadataAsync(string? input, CancellationToken cancellationToken = default)
    {
        var validation = UrlValidator.Validate(input);
        if (!validation.IsValid)
        {
            var error = validation.Error == UrlValidationError.UnsupportedSite ? MediaError.UnsupportedSite : MediaError.InvalidLink;
            return MetadataResult.Fail(error, validation.Message);
        }

        var url = validation.Uri!;
        string json;
        try
        {
            json = await client.GetMetadataJsonAsync(url, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (YtDlpNotFoundException ex)
        {
            logger.LogError(ex, "yt-dlp is not available");
            return MetadataResult.Fail(MediaError.ToolMissing);
        }
        catch (YtDlpException ex)
        {
            var error = YtDlpErrorClassifier.Classify(ex.StandardError);
            logger.LogWarning("yt-dlp failed for {Url} (exit {ExitCode}, classified {Error}): {StdErr}", url, ex.ExitCode, error, ex.StandardError);
            return MetadataResult.Fail(error);
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(ex, "yt-dlp timed out for {Url}", url);
            return MetadataResult.Fail(MediaError.Network);
        }

        VideoMetadata metadata;
        try
        {
            metadata = YtDlpMetadataParser.Parse(json);
        }
        catch (MetadataParseException ex)
        {
            logger.LogError(ex, "Could not parse yt-dlp output for {Url}", url);
            return MetadataResult.Fail(MediaError.Unknown);
        }

        return metadata.IsLive ? MetadataResult.Fail(MediaError.LiveStream) : MetadataResult.Ok(metadata);
    }
}
