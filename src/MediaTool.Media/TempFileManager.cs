namespace MediaTool.Media;

/// <summary>A finished output file in a job folder.</summary>
public sealed record OutputFile(string Path, long SizeBytes);

/// <summary>Per-job folders under the system temp dir.</summary>
public interface ITempFileManager
{
    string CreateJobFolder(Guid jobId);

    /// <summary>The single finished file in the job folder, or null if there is none.</summary>
    OutputFile? FindOutput(Guid jobId);

    void DeleteJobFolder(Guid jobId);
}

public sealed class TempFileManager(string root) : ITempFileManager
{
    /// <summary>Base name yt-dlp writes to; the final file is "media.&lt;ext&gt;".</summary>
    public const string OutputBaseName = "media";

    public static string DefaultRoot => Path.Combine(Path.GetTempPath(), "MediaTool");

    public string Root { get; } = root;

    public string CreateJobFolder(Guid jobId) => Directory.CreateDirectory(FolderFor(jobId)).FullName;

    public OutputFile? FindOutput(Guid jobId)
    {
        var folder = new DirectoryInfo(FolderFor(jobId));
        if (!folder.Exists)
        {
            return null;
        }

        // Finished file is exactly "media.<ext>". "media.f136.mp4" style names are unmerged parts.
        var file = folder.EnumerateFiles(OutputBaseName + ".*")
            .Where(f => f.Name.Count(c => c == '.') == 1 && !f.Extension.Equals(".part", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Length)
            .FirstOrDefault();

        return file is null ? null : new OutputFile(file.FullName, file.Length);
    }

    public void DeleteJobFolder(Guid jobId)
    {
        var folder = FolderFor(jobId);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private string FolderFor(Guid jobId) => Path.Combine(Root, jobId.ToString("N"));
}
