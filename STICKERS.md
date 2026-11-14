# Telegram sticker tools

The existing Blazor application now has a **Telegram stickers** tab at `/stickers`.
It uses the same card, radio, button and status design as the downloader. Conversion
is implemented once in Python and shared by the UI, command line and optional bot.

## Run locally (PowerShell)

FFmpeg and ffprobe must be on PATH. Python 3.11+ and .NET 10 are required.

```powershell
python -m venv .venv-stickers
.\.venv-stickers\Scripts\python -m pip install -r backend/requirements-stickers.txt
$env:Stickers__PythonPath = "$PWD\.venv-stickers\Scripts\python.exe"
dotnet run --project src/MediaTool.App
```

Open http://localhost:5080/stickers. Upload limit: 50 MB. Choose static PNG/WEBP
or video WEBM. Video controls let you pause/scrub the source, then use the current
frame time as a segment start or static image. Browser previews depend on browser
codec support; numeric time entry works for other FFmpeg-supported formats.
Conversion runs one job at a time. A looping video or image preview appears before
download, and **Save sticker** replaces **Create sticker**. Changing any setting
restores Create. Mode and option choices use radio buttons in columns. The upload
area supports both file selection and drag and drop.

The source is read once into the current page session (up to 50 MB), so switching
between PNG, WEBP and WEBM, including after saving, does not reopen the browser's
file stream. Videos can supply static frames; images can become still WEBM stickers.
Reloading or leaving the page releases the cached source. Results remain previewable
and downloadable for one hour; downloads do not consume the file. Expired results
are removed by cleanup, which runs every five minutes.

Docker includes Python, Pillow, NumPy, FFmpeg and the shared converter:

```powershell
docker compose -f compose.mediatool.yml up --build
```

## Python API and CLI

```python
from backend.stickers import convert_static, convert_video

image_path = convert_static('image.png', 'sticker.webp')
video_path = convert_video('clip.mp4', 'sticker.webm', option='segment', start=10, length=2.5)
```

Both functions return a `pathlib.Path`, after validation, or raise `StickerError`.
Outputs are written atomically; failed conversions do not replace an existing file.
Options: `speed`, `first`, `segment`, `loop` (`frame` remains an alias for `loop`).
Loop mode automatically finds a matching start/end frame pair. `start` and `length`
select a segment; `convert_static(..., start=...)` extracts a video frame.
`length` is also used for first mode. `crop_black=False` disables
black-border detection. `FFMPEG` and `FFPROBE` can override tool executable paths.

```powershell
.\.venv-stickers\Scripts\python -m backend.stickers video input.mp4 output.webm --option speed
.\.venv-stickers\Scripts\python -m backend.stickers video input.mp4 output.webm --option loop
.\.venv-stickers\Scripts\python -m backend.stickers static input.png output.webp
```

The CLI returns JSON containing `path` and final `size`, or `error` and a nonzero exit code.

## Batch compatibility and loop behavior

Read from `convert_to_telegram_webm v4.bat`: `setpts=(PTS-STARTPTS)/factor`,
factor `duration/2.95` for inputs longer than three seconds, 30 FPS,
long side 512 with even short side, 2.99-second output cap, libvpx-vp9,
yuva420p, no audio, starting CRF 24 and increasing compression in steps of four.
The provided batch contains no frame-selection or looping command.

Loop mode ports `loop/make_loop.py`: 30 FPS analysis at a 64 px long side,
premultiplied RGBA distance matrix, comparison of neighboring frames ±2, rejection
of stationary pauses, and selection of a moving loop up to 2.95 seconds. It adds
the script's adaptive 0/4/8-frame seam crossfade (or whole-clip crossfade fallback),
then applies its VP9 quality search over CRF 16–63 and FPS fallback 30/24/20/15.
Analysis is bounded to the first 1500 frames (50 seconds) before capturing decoded
data. VP8/VP9 inputs use libvpx decoders to preserve alpha. NumPy is required.
Speed, first and segment modes preserve forward playback; no reversal is added.
Crop detection
samples up to three seconds at the selection start, uses a cumulative rectangle,
and is optional because very dark content can be mistaken for a black border.
No padding is ever added. This is geometric border removal, not background removal.

Outside loop mode, oversized video retries use progressively lower VP9 bitrates and higher CRF.
Resolution is preserved: lowering the long side below 512 would violate Telegram's
dimension rule. If all attempts fail, conversion raises an error instead of
returning an oversized file. For noisy static PNGs over 512 KB, choose WEBP.
Static formats use lossless encoding and preserve transparency, including partial alpha.

## Optional Telegram bot

```powershell
$env:TELEGRAM_BOT_TOKEN = '<your bot token>'
.\.venv-stickers\Scripts\python -m backend.sticker_bot
```

The bot is separate from the desktop app and is not started automatically. It
offers mode/format buttons, video options, and timestamp entry for segment
selection. It replies with the sticker and its downloadable file, including size.
Send transparent images as documents; Telegram photo uploads may discard alpha.
Cloud Bot API uploads are limited here to 20 MB. `/cancel` resets the conversation
before conversion. Requests are processed sequentially, with no persistent media storage.
Live bot delivery requires a token and is not covered by the offline handler tests.

## Tests and specifications

```powershell
.\.venv-stickers\Scripts\python -m unittest discover -s backend/tests -v
dotnet test --filter "Category!=Integration"
```

Tests create local media; no Telegram or social-media connection is needed.
CI runs Python tests with real FFmpeg on Linux alongside the existing .NET checks.

Official requirements: https://core.telegram.org/stickers#video-stickers-and-emoji
and https://core.telegram.org/stickers#static-stickers-and-emoji.
Video: WEBM/VP9, ≤3 s, ≤30 FPS, ≤262144 bytes, no audio, one side exactly 512 px.
Static: PNG/WEBP, one side exactly 512 px; the bot's 512 KB static upload cap is
also enforced. Looping is a Telegram usability recommendation; the converter
constructs loops rather than claiming it can prove perceptual quality for arbitrary input.
