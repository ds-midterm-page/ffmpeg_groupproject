using FluentAssertions;
using MediaTool.Media;

namespace MediaTool.Core.Tests.Media;

/// <summary>Uses a private folder under the system temp dir; no network.</summary>
public sealed class TempFileManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MediaTool.Tests", Guid.NewGuid().ToString("N"));

    private TempFileManager Sut => new(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Creates_a_separate_folder_per_job_under_root()
    {
        var id = Guid.NewGuid();

        var folder = Sut.CreateJobFolder(id);

        Directory.Exists(folder).Should().BeTrue();
        Path.GetDirectoryName(folder).Should().Be(_root);
        Path.GetFileName(folder).Should().Be(id.ToString("N"));
    }

    [Fact]
    public void Default_root_is_under_system_temp()
    {
        TempFileManager.DefaultRoot.Should().StartWith(Path.GetTempPath());
    }

    [Fact]
    public void Finds_the_finished_file_and_ignores_partial_ones()
    {
        var id = Guid.NewGuid();
        var folder = Sut.CreateJobFolder(id);
        File.WriteAllBytes(Path.Combine(folder, "media.f136.mp4.part"), new byte[10]);
        File.WriteAllBytes(Path.Combine(folder, "media.mp4"), new byte[1234]);

        var output = Sut.FindOutput(id);

        output.Should().NotBeNull();
        output!.Path.Should().EndWith("media.mp4");
        output.SizeBytes.Should().Be(1234);
    }

    [Fact]
    public void Ignores_leftover_fragments_from_merging()
    {
        var id = Guid.NewGuid();
        var folder = Sut.CreateJobFolder(id);
        File.WriteAllBytes(Path.Combine(folder, "media.f136.mp4"), new byte[10]);
        File.WriteAllBytes(Path.Combine(folder, "media.f140.m4a"), new byte[10]);

        Sut.FindOutput(id).Should().BeNull();
    }

    [Fact]
    public void Missing_or_empty_folder_has_no_output()
    {
        Sut.FindOutput(Guid.NewGuid()).Should().BeNull();
        var id = Guid.NewGuid();
        Sut.CreateJobFolder(id);
        Sut.FindOutput(id).Should().BeNull();
    }

    [Fact]
    public void Delete_removes_the_job_folder_and_tolerates_missing()
    {
        var id = Guid.NewGuid();
        var folder = Sut.CreateJobFolder(id);
        File.WriteAllText(Path.Combine(folder, "media.mp4"), "x");

        Sut.DeleteJobFolder(id);
        Sut.DeleteJobFolder(id);

        Directory.Exists(folder).Should().BeFalse();
    }
}
