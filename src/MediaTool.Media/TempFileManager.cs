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
    public static string DefaultRoot => throw new NotImplementedException();

    public string Root { get; } = root;

    public string CreateJobFolder(Guid jobId) => throw new NotImplementedException();

    public OutputFile? FindOutput(Guid jobId) => throw new NotImplementedException();

    public void DeleteJobFolder(Guid jobId) => throw new NotImplementedException();
}
