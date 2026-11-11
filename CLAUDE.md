# MediaTool

Local desktop-style app for non-technical users: paste a link from YouTube, TikTok, Instagram or X, preview it, pick quality/format, trim it, and save the result. No command line, no adware sites.

## Stack

- .NET 10 (LTS), C#, nullable enabled, `TreatWarningsAsErrors` on
- ASP.NET Core + **Blazor Server** UI, runs at `http://localhost:5080` and opens the browser on start
- **YoutubeDLSharp** for yt-dlp, **FFMpegCore** for ffmpeg/ffprobe
- Background jobs: `System.Threading.Channels` + `BackgroundService` (no Redis)
- Tests: **xUnit**, **FluentAssertions**, **NSubstitute**
- CI: GitHub Actions running `dotnet build` + `dotnet test`

## Solution layout

```
MediaTool.sln
src/
  MediaTool.Core/      pure logic: models, presets, trim math, URL validation. NO I/O, NO process calls.
  MediaTool.Media/     YtDlpService, FfmpegService, JobQueue, TempFileManager (wraps external tools)
  MediaTool.App/       Blazor UI, DI setup, background worker
tests/
  MediaTool.Core.Tests/          fast unit tests, no network
  MediaTool.Integration.Tests/   real downloads + ffprobe checks, marked [Trait("Category","Integration")]
  fixtures/                      saved yt-dlp JSON outputs, small sample clips
```

## Commands

- Build: `dotnet build`
- Unit tests: `dotnet test --filter "Category!=Integration"`
- All tests: `dotnet test`
- Run: `dotnet run --project src/MediaTool.App`
- Publish single exe: `dotnet publish src/MediaTool.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`
- Docker app (Linux, yt-dlp + ffmpeg included): `docker compose -f compose.mediatool.yml up --build` → http://localhost:5080
- Docker tests (all, incl. real downloads): `docker build --target test -t mediatool-test .` then `docker run --rm mediatool-test`
- `docker-compose.yml` is the separate Python backend + JS frontend; MediaTool uses `compose.mediatool.yml`.
- Local Windows runs need `src/MediaTool.App/tools/yt-dlp.exe` (not committed) or yt-dlp on PATH.
- Tests must pass on Windows and Linux: build test paths with `Path.Combine`/`Path.GetTempPath()`, never hard-code `C:\`.

## Rules

- **Test-driven:** for every feature write failing tests first, run them, then implement until green. Never delete or weaken a test to make it pass.
- Core must stay free of I/O; services in Media depend on interfaces (`IYtDlpClient`, `IFfmpegRunner`) so they can be mocked.
- Unit tests never touch the network; use JSON fixtures in `tests/fixtures/`.
- **Never build shell command strings.** Pass ffmpeg/yt-dlp arguments as argument lists. Sanitize output filenames (strip path separators, `;`, `"`, `|`, `..`, control chars).
- Supported hosts only: youtube.com, youtu.be, tiktok.com, instagram.com, x.com, twitter.com. Anything else returns a clear error.
- Limits: max output resolution 1280 (test presets: 1280/720p, 480p, 360p, audio-only); **max file size 50 MB** — check the estimated size before download and the real size after.
- Temp files go in a per-job folder under the system temp dir and are deleted when the job finishes or after 1 hour.
- Errors shown to the user are plain-language (private video, removed, unsupported site, too large); technical details go to the log.
- Optional cookies-file setting for Instagram/X.
- Small commits, one milestone step per commit, message format: `feat: …`, `test: …`, `fix: …`, `chore: …`.

## Milestones

1. [x] Setup: solution, projects, test projects, CI workflow, .gitignore
2. [x] URL validation (Core) — supported/unsupported/malformed links
3. [x] Metadata + preview — yt-dlp wrapper, parse title/duration/thumbnail/formats from fixtures
4. [x] Format picker — resolutions ≤1280 + audio-only, 50 MB size check
5. [x] Download job — background queue, progress in UI; integration test: 720p MP4 verified with ffprobe
6. [ ] Trim timeline — time math tests; ffmpeg trim; ffprobe duration == end − start (±0.1 s)
7. [ ] Presets — preset → ffmpeg argument builder; reject malformed presets and injection attempts
8. [ ] Cleanup + polish — temp file cleanup, friendly errors, single-exe publish
9. [ ] Later: Telegram stickers (512×512, webm ≤3 s, ≤512 KB)

Mark milestones done here as they are completed.
