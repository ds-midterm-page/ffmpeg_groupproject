using MediaTool.Media;

namespace MediaTool.App;

/// <summary>Takes download jobs off the queue and runs them one at a time.</summary>
public sealed class DownloadWorker(JobQueue queue, DownloadJobRunner runner, ILogger<DownloadWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in queue.ReadAllAsync(stoppingToken))
            {
                await runner.RunAsync(job.Id, job.Request, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Download worker stopping");
        }
    }
}
