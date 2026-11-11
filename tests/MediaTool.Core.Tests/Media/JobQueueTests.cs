using FluentAssertions;
using MediaTool.Core.Download;
using MediaTool.Core.Formats;
using MediaTool.Media;

namespace MediaTool.Core.Tests.Media;

public class JobQueueTests
{
    private static readonly DownloadRequest Request = new(
        new Uri("https://youtu.be/abc"), "Title",
        new QualityOption("360p", QualityKind.Video, "18", "mp4", 640, 360, true, 1000, false, false));

    [Fact]
    public async Task Enqueued_job_is_queued_in_store_and_readable_from_channel()
    {
        var store = new JobStore();
        var queue = new JobQueue(store);

        var id = await queue.EnqueueAsync(Request);

        store.Get(id)!.Status.Should().Be(JobStatus.Queued);
        store.Get(id)!.Title.Should().Be("Title");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var item in queue.ReadAllAsync(cts.Token))
        {
            item.Id.Should().Be(id);
            item.Request.Should().Be(Request);
            break;
        }
    }

    [Fact]
    public async Task Jobs_come_out_in_order()
    {
        var queue = new JobQueue(new JobStore());
        var first = await queue.EnqueueAsync(Request);
        var second = await queue.EnqueueAsync(Request);

        var ids = new List<Guid>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var item in queue.ReadAllAsync(cts.Token))
        {
            ids.Add(item.Id);
            if (ids.Count == 2)
            {
                break;
            }
        }

        ids.Should().Equal(first, second);
    }

    [Fact]
    public void Store_raises_changed_on_update_and_ignores_unknown_ids()
    {
        var store = new JobStore();
        var job = store.Create(Request);
        JobSnapshot? seen = null;
        store.Changed += s => seen = s;

        store.Update(job.Id, s => s with { Status = JobStatus.Downloading, Percent = 10 });
        store.Update(Guid.NewGuid(), s => s with { Status = JobStatus.Failed });

        seen!.Status.Should().Be(JobStatus.Downloading);
        seen.Percent.Should().Be(10);
        store.Get(Guid.NewGuid()).Should().BeNull();
    }
}
