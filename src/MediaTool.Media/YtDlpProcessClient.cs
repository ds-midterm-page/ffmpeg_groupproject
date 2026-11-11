using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using MediaTool.Core.Download;
using MediaTool.Core.Metadata;
using MediaTool.Core.Validation;
using Microsoft.Extensions.Options;

namespace MediaTool.Media;

/// <summary>Runs the real yt-dlp executable. Arguments go through <see cref="ProcessStartInfo.ArgumentList"/>, never a shell.</summary>
public sealed class YtDlpProcessClient(IOptions<YtDlpOptions> options) : IYtDlpClient
{
    private readonly YtDlpOptions _options = options.Value;

    public async Task<string> GetMetadataJsonAsync(Uri url, CancellationToken cancellationToken)
    {
        var platform = UrlValidator.Validate(url.AbsoluteUri).Platform;
        var cookies = platform is Platform.Instagram or Platform.X && File.Exists(_options.CookiesFile) ? _options.CookiesFile : null;

        var (exitCode, stdout, stderr) = await RunAsync(YtDlpArguments.ForMetadata(url, cookies), _options.MetadataTimeout, cancellationToken);
        if (exitCode != 0)
        {
            throw new YtDlpException(exitCode, stderr);
        }

        return stdout;
    }

    public Task DownloadAsync(DownloadRequest request, string outputFolder, IProgress<DownloadProgress> progress, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    internal string ResolveExecutable()
    {
        if (!string.IsNullOrWhiteSpace(_options.ExecutablePath))
        {
            return _options.ExecutablePath;
        }

        var bundled = Path.Combine(AppContext.BaseDirectory, "tools", OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp");
        return File.Exists(bundled) ? bundled : "yt-dlp";
    }

    private async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var exe = ResolveExecutable();
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        // Force yt-dlp to write UTF-8 so non-Latin titles survive on Windows.
        psi.Environment["PYTHONIOENCODING"] = "utf-8";

        // yt-dlp crashes if any PATH folder is unreadable, so hand it only folders that work.
        psi.Environment["PATH"] = SearchPath.KeepUsable(psi.Environment["PATH"], SearchPath.IsReadableDirectory);

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new YtDlpNotFoundException(exe, ex);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            return (process.ExitCode, await stdoutTask, await stderrTask);
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
            throw new TimeoutException($"yt-dlp did not finish within {timeout}.");
        }
    }
}
