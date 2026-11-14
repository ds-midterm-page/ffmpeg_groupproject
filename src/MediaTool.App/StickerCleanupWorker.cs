using MediaTool.Media;

namespace MediaTool.App;

public sealed class StickerCleanupWorker(StickerConverter converter, ILogger<StickerCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try { converter.CleanupExpired(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { logger.LogWarning(ex, "Could not remove expired stickers"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
