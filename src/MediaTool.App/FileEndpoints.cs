using MediaTool.Core.Download;
using MediaTool.Media;

namespace MediaTool.App;

/// <summary>Serves a finished download once, then deletes its temp folder.</summary>
public static class FileEndpoints
{
    public static void MapFileEndpoints(this WebApplication app)
    {
        app.MapGet("/files/{id:guid}", (Guid id, HttpContext http, JobStore store, ITempFileManager temp, ILogger<WebApplication> logger) =>
        {
            var job = store.Get(id);
            if (job is not { Status: JobStatus.Completed, FilePath: { } path } || !File.Exists(path))
            {
                return Results.NotFound("This file is no longer available. Please download it again.");
            }

            http.Response.OnCompleted(() =>
            {
                try
                {
                    temp.DeleteJobFolder(id);
                }
                catch (IOException ex)
                {
                    logger.LogWarning(ex, "Could not delete temp folder for job {JobId}", id);
                }

                store.Update(id, s => s with { FilePath = null });
                return Task.CompletedTask;
            });

            return Results.File(path, ContentTypeFor(path), job.DownloadName ?? Path.GetFileName(path));
        });
    }

    private static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".mp4" => "video/mp4",
        ".m4a" => "audio/mp4",
        ".webm" => "video/webm",
        ".mkv" => "video/x-matroska",
        ".mp3" => "audio/mpeg",
        _ => "application/octet-stream",
    };
}
