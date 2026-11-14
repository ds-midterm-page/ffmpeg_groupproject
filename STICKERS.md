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
frame time as a segment start or held frame. Browser previews depend on browser
codec support; numeric time entry works for other FFmpeg-supported formats.
Conversion runs one job at a time. Download the output using **Save sticker**;
its final size is shown in bytes and KB. Temporary output expires after one hour
(cleanup checks every five minutes), or is deleted after download.

Docker includes Python, Pillow, FFmpeg and the shared converter:

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
Options: `speed`, `first`, `segment`, `frame`. `start` is the frame timestamp in
frame mode. `length` is used for segment/first mode. `crop_black=False` disables
black-border detection. `FFMPEG` and `FFPROBE` can override tool executable paths.

```powershell
.\.venv-stickers\Scripts\python -m backend.stickers video input.mp4 output.webm --option speed
.\.venv-stickers\Scripts\python -m backend.stickers video input.mp4 output.webm --option frame --start 12.5
.\.venv-stickers\Scripts\python -m backend.stickers static input.png output.webp
```

The CLI returns JSON containing `path` and final `size`, or `error` and a nonzero exit code.

## Batch compatibility and loop behavior

Read from `convert_to_telegram_webm v4.bat`: `setpts=(PTS-STARTPTS)/factor`,
factor `duration/2.95` for inputs longer than three seconds, 30 FPS,
long side 512 with even short side, 2.99-second output cap, libvpx-vp9,
yuva420p, no audio, starting CRF 24 and increasing compression in steps of four.
The provided batch contains no frame-selection or looping command.

Frame mode holds the selected frame for a seamless stationary loop. Moving
modes play the selected content forward and backward, avoiding an abrupt jump
at the loop boundary; this changes the motion to a boomerang. Crop detection
samples up to three seconds at the selection start, uses a cumulative rectangle,
and is optional because very dark content can be mistaken for a black border.
No padding is ever added. This is geometric border removal, not background removal.

Oversized video retries use progressively lower VP9 bitrates and higher CRF.
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
offers mode/format buttons, video options, and timestamp entry for frame/segment
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
