using MediaTool.Core.Formats;
using MediaTool.Core.Metadata;

namespace MediaTool.Core.Download;

/// <summary>What the user asked to download.</summary>
public sealed record DownloadRequest(Uri Url, string Title, QualityOption Option);

/// <summary>One progress report from yt-dlp. <see cref="TotalBytes"/> is null when the site doesn't say.</summary>
public sealed record DownloadProgress(long DownloadedBytes, long? TotalBytes)
{
    public double? Percent => TotalBytes is > 0 ? Math.Min(100, DownloadedBytes * 100.0 / TotalBytes.Value) : null;
}

public enum JobStatus
{
    Queued,
    Downloading,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>Immutable view of a job for the UI. <see cref="FilePath"/> is only set when completed.</summary>
public sealed record JobSnapshot(
    Guid Id,
    string Title,
    JobStatus Status,
    double? Percent = null,
    MediaError Error = MediaError.None,
    string? Message = null,
    string? FilePath = null,
    long? FileSizeBytes = null,
    string? DownloadName = null)
{
    public bool IsFinished => Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled;
}
