using System.Collections.Concurrent;
using System.Threading.Channels;
using MediaTool.Core.Download;

namespace MediaTool.Media;

public sealed record QueuedJob(Guid Id, DownloadRequest Request);

/// <summary>In-memory job states for the UI. Thread-safe; <see cref="Changed"/> fires on every update.</summary>
public sealed class JobStore
{
    private readonly ConcurrentDictionary<Guid, JobSnapshot> _jobs = new();

    public event Action<JobSnapshot>? Changed;

    public JobSnapshot Create(DownloadRequest request)
    {
        var snapshot = new JobSnapshot(Guid.NewGuid(), request.Title, JobStatus.Queued);
        _jobs[snapshot.Id] = snapshot;
        Changed?.Invoke(snapshot);
        return snapshot;
    }

    public JobSnapshot? Get(Guid id) => _jobs.GetValueOrDefault(id);

    public void Update(Guid id, Func<JobSnapshot, JobSnapshot> change)
    {
        while (_jobs.TryGetValue(id, out var current))
        {
            var updated = change(current);
            if (_jobs.TryUpdate(id, updated, current))
            {
                Changed?.Invoke(updated);
                return;
            }
        }
    }

    public void Remove(Guid id) => _jobs.TryRemove(id, out _);
}

/// <summary>Download jobs waiting for the background worker (one at a time, in order).</summary>
public sealed class JobQueue(JobStore store)
{
    private readonly Channel<QueuedJob> _channel = Channel.CreateUnbounded<QueuedJob>(new UnboundedChannelOptions { SingleReader = true });

    public JobStore Store { get; } = store;

    public async ValueTask<Guid> EnqueueAsync(DownloadRequest request, CancellationToken cancellationToken = default)
    {
        var job = Store.Create(request);
        await _channel.Writer.WriteAsync(new QueuedJob(job.Id, request), cancellationToken);
        return job.Id;
    }

    public IAsyncEnumerable<QueuedJob> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}
