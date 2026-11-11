using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using MediaTool.Core.Download;
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

/// <summary>Runs the real ffmpeg. Arguments go through <see cref="ProcessStartInfo.ArgumentList"/>, never a shell.</summary>
public sealed class FfmpegRunner(IOptions<FfmpegOptions> options) : IFfmpegRunner
{
    private readonly FfmpegOptions _options = options.Value;

    public async Task RemoveAudioAsync(string path, CancellationToken cancellationToken)
    {
        var full = Path.GetFullPath(path);
        var temp = Path.Combine(Path.GetDirectoryName(full)!, "silent-" + Guid.NewGuid().ToString("N") + Path.GetExtension(full));
        try
        {
            await RunAsync(FfmpegArguments.RemoveAudio(full, temp), cancellationToken);
            File.Move(temp, full, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    private string ResolveExecutable()
    {
        if (!string.IsNullOrWhiteSpace(_options.ExecutablePath))
        {
            return _options.ExecutablePath;
        }

        var bundled = Path.Combine(AppContext.BaseDirectory, "tools", OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        return File.Exists(bundled) ? bundled : "ffmpeg";
    }

    private async Task RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var exe = ResolveExecutable();
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        psi.Environment["PATH"] = SearchPath.KeepUsable(psi.Environment["PATH"], SearchPath.IsReadableDirectory);

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new FfmpegException(-1, $"ffmpeg could not be started from '{exe}': {ex.Message}");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.Timeout);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                throw new FfmpegException(process.ExitCode, stderr);
            }
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // already exited
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException($"ffmpeg did not finish within {_options.Timeout}.");
        }
    }
}
