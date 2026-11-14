using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MediaTool.Media;

public sealed class StickerOptions
{
    public string PythonPath { get; set; } = "python";
}

public sealed record StickerResult(Guid Id, string Path, long Size, string Extension, DateTime Created);

/// <summary>Runs the shared Python converter outside the UI thread, one encode at a time.</summary>
public sealed class StickerConverter(IOptions<StickerOptions> options)
{
    private readonly SemaphoreSlim _gate = new(1);
    private readonly ConcurrentDictionary<Guid, StickerResult> _results = new();
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "MediaTool", "stickers");

    public async Task<StickerResult> ConvertAsync(Stream input, string mode, string extension,
        string videoOption, double start, double length, bool crop, CancellationToken token)
    {
        if (mode is not ("static" or "video") || extension is not ("png" or "webp" or "webm")
            || (mode == "video") != (extension == "webm"))
            throw new ArgumentException("Choose a valid sticker mode and format.");

        await _gate.WaitAsync(token);
        var id = Guid.NewGuid();
        var folder = Path.Combine(Root, id.ToString("N"));
        try
        {
            CleanupExpired();
            Directory.CreateDirectory(folder);
            var source = Path.Combine(folder, "input");
            var output = Path.Combine(folder, "sticker." + extension);
            await using (var file = File.Create(source))
                await input.CopyToAsync(file, token);

            var script = Path.Combine(AppContext.BaseDirectory, "stickers", "stickers.py");
            var psi = new ProcessStartInfo(options.Value.PythonPath)
            {
                UseShellExecute = false, RedirectStandardOutput = true,
                RedirectStandardError = true, CreateNoWindow = true,
            };
            foreach (var arg in new[] { script, mode, source, output, "--option", videoOption,
                "--start", start.ToString(CultureInfo.InvariantCulture), "--length", length.ToString(CultureInfo.InvariantCulture) })
                psi.ArgumentList.Add(arg);
            if (!crop) psi.ArgumentList.Add("--no-crop");
            psi.Environment["PATH"] = SearchPath.KeepUsable(psi.Environment["PATH"], SearchPath.IsReadableDirectory);
            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Cannot start the sticker converter.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromMinutes(10));
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                var text = await stdout;
                var diagnostic = await stderr;
                if (string.IsNullOrWhiteSpace(text))
                    throw new InvalidOperationException("Sticker converter unavailable. Check Python, Pillow, FFmpeg and ffprobe installation.", new Exception(diagnostic));
                using var json = JsonDocument.Parse(text);
                if (process.ExitCode != 0)
                    throw new InvalidOperationException(json.RootElement.TryGetProperty("error", out var error)
                        ? error.GetString() : "Sticker conversion failed.");
                if (!File.Exists(output)) throw new InvalidOperationException("No sticker was produced.");
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                await process.WaitForExitAsync(CancellationToken.None);
                throw;
            }
            var result = new StickerResult(id, output, new FileInfo(output).Length, extension, DateTime.UtcNow);
            _results[id] = result;
            return result;
        }
        catch
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            throw;
        }
        finally { _gate.Release(); }
    }

    public StickerResult? Take(Guid id) => _results.TryRemove(id, out var result) ? result : null;

    public static void Delete(StickerResult result)
    {
        var folder = Path.GetDirectoryName(result.Path)!;
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }

    public void CleanupExpired()
    {
        foreach (var pair in _results.Where(p => p.Value.Created < DateTime.UtcNow.AddHours(-1)))
            if (_results.TryRemove(pair.Key, out var result)) Delete(result);
        // Also remove stale uploads left by an interrupted process.
        if (!Directory.Exists(Root)) return;
        foreach (var folder in Directory.EnumerateDirectories(Root))
            if (Directory.GetCreationTimeUtc(folder) < DateTime.UtcNow.AddHours(-1))
                Directory.Delete(folder, true);
    }
}
