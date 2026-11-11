using MediaTool.Core.Download;
using Microsoft.Extensions.Logging;

namespace MediaTool.Media;

/// <summary>Runs one download job: yt-dlp into a job folder, then the real size check.</summary>
public sealed class DownloadJobRunner(IYtDlpClient client, ITempFileManager temp, JobStore store, ILogger<DownloadJobRunner> logger)
{
    private readonly IYtDlpClient _client = client;
    private readonly ITempFileManager _temp = temp;
    private readonly JobStore _store = store;
    private readonly ILogger<DownloadJobRunner> _logger = logger;

    public Task RunAsync(Guid jobId, DownloadRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
