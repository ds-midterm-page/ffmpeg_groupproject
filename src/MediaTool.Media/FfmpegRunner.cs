using Microsoft.Extensions.Options;

namespace MediaTool.Media;

/// <summary>Bound from the "Ffmpeg" section of appsettings.json.</summary>
public sealed class FfmpegOptions
{
    public const string SectionName = "Ffmpeg";

    /// <summary>Full path to ffmpeg. Empty: tools\ffmpeg.exe next to the app, else "ffmpeg" from PATH.</summary>
    public string? ExecutablePath { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);
}

/// <summary>Thin wrapper around ffmpeg so services can be tested without it.</summary>
public interface IFfmpegRunner
{
    /// <summary>Drops all audio from the file, replacing it in place.</summary>
    /// <exception cref="FfmpegException">ffmpeg failed.</exception>
    Task RemoveAudioAsync(string path, CancellationToken cancellationToken);
}

/// <summary>ffmpeg ran but failed. <see cref="StandardError"/> is for the log, never for the user.</summary>
public sealed class FfmpegException(int exitCode, string standardError)
    : Exception($"ffmpeg exited with code {exitCode}: {standardError}")
{
    public int ExitCode { get; } = exitCode;

    public string StandardError { get; } = standardError;
}

public sealed class FfmpegRunner(IOptions<FfmpegOptions> options) : IFfmpegRunner
{
    private readonly FfmpegOptions _options = options.Value;

    public Task RemoveAudioAsync(string path, CancellationToken cancellationToken) => throw new NotImplementedException();
}
