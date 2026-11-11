using MediaTool.Core.Metadata;
using Microsoft.Extensions.Logging;

namespace MediaTool.Media;

public sealed record MetadataResult(VideoMetadata? Metadata, MediaError Error, string? Message)
{
    public bool IsSuccess => Error == MediaError.None;
}

public sealed class MetadataService(IYtDlpClient client, ILogger<MetadataService> logger)
{
    private readonly IYtDlpClient _client = client;
    private readonly ILogger<MetadataService> _logger = logger;

    public Task<MetadataResult> GetMetadataAsync(string? input, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
