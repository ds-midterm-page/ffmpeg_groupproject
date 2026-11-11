namespace MediaTool.Media;

/// <summary>Thin wrapper around the yt-dlp executable so services can be tested without it.</summary>
public interface IYtDlpClient
{
    /// <summary>Returns yt-dlp's raw JSON for a single video.</summary>
    /// <exception cref="YtDlpException">yt-dlp exited with an error.</exception>
    /// <exception cref="YtDlpNotFoundException">yt-dlp could not be started.</exception>
    /// <exception cref="TimeoutException">yt-dlp took too long.</exception>
    Task<string> GetMetadataJsonAsync(Uri url, CancellationToken cancellationToken);
}

/// <summary>yt-dlp ran but failed. <see cref="StandardError"/> is for the log, never for the user.</summary>
public sealed class YtDlpException(int exitCode, string standardError)
    : Exception($"yt-dlp exited with code {exitCode}: {standardError}")
{
    public int ExitCode { get; } = exitCode;

    public string StandardError { get; } = standardError;
}

/// <summary>The yt-dlp executable could not be found or started.</summary>
public sealed class YtDlpNotFoundException(string path, Exception? inner = null)
    : Exception($"yt-dlp could not be started from '{path}'.", inner);
