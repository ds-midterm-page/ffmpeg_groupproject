using MediaTool.Core.Download;
using MediaTool.Core.Formats;
using MediaTool.Core.Metadata;
using Microsoft.Extensions.Logging;

namespace MediaTool.Media;

/// <summary>
/// Runs one download job: refuse if the estimate is over the limit, run yt-dlp into a per-job folder,
/// then check the real file size. Any failure deletes the job folder and leaves a plain-language message.
/// </summary>
public sealed class DownloadJobRunner(
    IYtDlpClient client, ITempFileManager temp, IFfmpegRunner ffmpeg, JobStore store, ILogger<DownloadJobRunner> logger)
{
    private readonly IFfmpegRunner _ffmpeg = ffmpeg;

    public async Task RunAsync(Guid jobId, DownloadRequest request, CancellationToken cancellationToken)
    {
        if (request.Option.ExceedsLimit)
        {
            Fail(jobId, MediaError.TooLarge);
            return;
        }

        store.Update(jobId, s => s with { Status = JobStatus.Downloading, Percent = 0 });

        try
        {
            var folder = temp.CreateJobFolder(jobId);
            var progress = new SyncProgress(p => store.Update(jobId, s => s with { Percent = p.Percent ?? s.Percent }));

            await client.DownloadAsync(request, folder, progress, cancellationToken);

            var output = temp.FindOutput(jobId);
            if (output is null)
            {
                // yt-dlp exits successfully but writes nothing when --max-filesize skips the download.
                logger.LogWarning("Job {JobId}: yt-dlp produced no file (skipped by max-filesize?)", jobId);
                Fail(jobId, MediaError.TooLarge, deleteFolder: true);
                return;
            }

            if (SizeLimit.IsTooLarge(output.SizeBytes))
            {
                logger.LogWarning("Job {JobId}: real size {Size} is over the limit", jobId, output.SizeBytes);
                Fail(jobId, MediaError.TooLarge, deleteFolder: true);
                return;
            }

            var downloadName = FileNameSanitizer.Sanitize(request.Title) + Path.GetExtension(output.Path);
            store.Update(jobId, s => s with
            {
                Status = JobStatus.Completed,
                Percent = 100,
                FilePath = output.Path,
                FileSizeBytes = output.SizeBytes,
                DownloadName = downloadName,
            });
            logger.LogInformation("Job {JobId}: finished {Path} ({Size} bytes)", jobId, output.Path, output.SizeBytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Job {JobId}: cancelled", jobId);
            store.Update(jobId, s => s with { Status = JobStatus.Cancelled, Message = "Download cancelled." });
            DeleteFolder(jobId);
        }
        catch (YtDlpNotFoundException ex)
        {
            logger.LogError(ex, "Job {JobId}: yt-dlp is not available", jobId);
            Fail(jobId, MediaError.ToolMissing, deleteFolder: true);
        }
        catch (YtDlpException ex)
        {
            var error = YtDlpErrorClassifier.Classify(ex.StandardError);
            logger.LogWarning("Job {JobId}: yt-dlp failed (exit {ExitCode}, classified {Error}): {StdErr}", jobId, ex.ExitCode, error, ex.StandardError);
            Fail(jobId, error, deleteFolder: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId}: unexpected failure", jobId);
            Fail(jobId, MediaError.Unknown, deleteFolder: true);
        }
    }

    private void Fail(Guid jobId, MediaError error, bool deleteFolder = false)
    {
        store.Update(jobId, s => s with
        {
            Status = JobStatus.Failed,
            Error = error,
            Message = MediaErrorMessages.For(error),
            FilePath = null,
            FileSizeBytes = null,
        });

        if (deleteFolder)
        {
            DeleteFolder(jobId);
        }
    }

    private void DeleteFolder(Guid jobId)
    {
        try
        {
            temp.DeleteJobFolder(jobId);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Job {JobId}: could not delete temp folder", jobId);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Job {JobId}: could not delete temp folder", jobId);
        }
    }

    /// <summary>Unlike <see cref="Progress{T}"/>, reports immediately on the calling thread.</summary>
    private sealed class SyncProgress(Action<DownloadProgress> report) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => report(value);
    }
}
