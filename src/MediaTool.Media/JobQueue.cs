using MediaTool.Core.Download;

namespace MediaTool.Media;

public sealed record QueuedJob(Guid Id, DownloadRequest Request);

/// <summary>In-memory job states for the UI.</summary>
public sealed class JobStore
{
    public event Action<JobSnapshot>? Changed;

    public JobSnapshot Create(DownloadRequest request) => throw new NotImplementedException();

    public JobSnapshot? Get(Guid id) => throw new NotImplementedException();

    public void Update(Guid id, Func<JobSnapshot, JobSnapshot> change) => throw new NotImplementedException();

    internal void Raise(JobSnapshot snapshot) => Changed?.Invoke(snapshot);
}

/// <summary>Download jobs waiting for the background worker.</summary>
public sealed class JobQueue(JobStore store)
{
    public JobStore Store { get; } = store;

    public ValueTask<Guid> EnqueueAsync(DownloadRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public IAsyncEnumerable<QueuedJob> ReadAllAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
}
