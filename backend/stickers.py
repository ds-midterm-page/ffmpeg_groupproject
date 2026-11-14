"""Telegram sticker conversion. No shell commands; all outputs are validated.

The v4 batch filter is preserved for speed mode (2.95 target, 2.99 cap,
30 fps, aspect fit, VP9/yuva420p, CRF 24). The supplied batch has no loop
or frame-selection command. Matching-frame search and seam blending are ported
from the subsequently supplied make_loop.py.
"""
import argparse
from fractions import Fraction
import json
import math
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

from PIL import Image, ImageOps, UnidentifiedImageError
try:
    from . import sticker_loop
except ImportError:  # The Blazor bridge invokes this file directly.
    import sticker_loop

MAX_VIDEO_BYTES = 256 * 1024
MAX_STATIC_BYTES = 512 * 1024


class StickerError(ValueError):
    """An input cannot be converted to a valid Telegram sticker."""


def run_tool(arguments, timeout=180):
    try:
        result = subprocess.run([str(x) for x in arguments], capture_output=True,
                                timeout=timeout, check=False)
    except FileNotFoundError as exc:
        raise StickerError(f'{arguments[0]} is missing. Install FFmpeg and ffprobe.') from exc
    except subprocess.TimeoutExpired as exc:
        raise StickerError('Media processing timed out. Try a shorter or smaller input.') from exc
    if result.returncode:
        # Avoid disclosing uploaded file paths in UI errors. Retain details as exception cause.
        raise StickerError('FFmpeg could not process this media. Try another file.') from RuntimeError(
            result.stderr.decode('utf-8', errors='replace')[-4000:])
    return result


def probe(path):
    result = run_tool([os.getenv('FFPROBE', 'ffprobe'), '-v', 'error', '-show_streams',
                       '-show_format', '-of', 'json', Path(path).resolve()])
    try:
        return json.loads(result.stdout)
    except (ValueError, UnicodeError) as exc:
        raise StickerError('Unable to read media information.') from exc


def dimensions(width, height):
    if width <= 0 or height <= 0 or max(width, height) != 512:
        raise StickerError('One side must be exactly 512 px; the other must be between 1 and 512 px.')


def validate_static(path, require_alpha=False):
    path = Path(path)
    if path.suffix.lower() not in ('.png', '.webp'):
        raise StickerError('Static stickers must be PNG or WEBP.')
    if path.stat().st_size > MAX_STATIC_BYTES:
        raise StickerError('Static sticker exceeds 512 KB. Try WEBP instead.')
    try:
        with Image.open(path) as image:
            if image.format != {'.png': 'PNG', '.webp': 'WEBP'}[path.suffix.lower()]:
                raise StickerError('The image content does not match its file extension.')
            if getattr(image, 'n_frames', 1) != 1:
                raise StickerError('Static stickers must contain exactly one frame.')
            image.load()
            dimensions(*image.size)
            if require_alpha and image.convert('RGBA').getchannel('A').getextrema()[0] == 255:
                raise StickerError('The source transparency was lost.')
            return {'width': image.width, 'height': image.height, 'size': path.stat().st_size}
    except (OSError, UnidentifiedImageError) as exc:
        raise StickerError('This file is not a readable PNG or WEBP image.') from exc


def validate_video(path):
    path = Path(path)
    if path.suffix.lower() != '.webm':
        raise StickerError('Video stickers must use the WEBM extension.')
    if path.stat().st_size > MAX_VIDEO_BYTES:
        raise StickerError('Video sticker exceeds 256 KB.')
    # EBML DocType must be WebM, not merely a Matroska file renamed .webm.
    header = path.read_bytes()[:4096]
    if not header.startswith(b'\x1a\x45\xdf\xa3') or b'\x42\x82\x84webm' not in header:
        raise StickerError('Video must be in a WEBM container.')
    info = probe(path)
    if 'webm' not in info.get('format', {}).get('format_name', '').split(','):
        raise StickerError('Video must be in a WEBM container.')
    videos = [s for s in info.get('streams', []) if s['codec_type'] == 'video']
    if any(s['codec_type'] == 'audio' for s in info.get('streams', [])):
        raise StickerError('Video stickers must have no audio stream.')
    if len(videos) != 1 or videos[0].get('codec_name') != 'vp9':
        raise StickerError('Video must contain exactly one VP9 stream.')
    video = videos[0]
    dimensions(video.get('width', 0), video.get('height', 0))
    try:
        duration = float(info['format']['duration'])
        fps = float(Fraction(video.get('avg_frame_rate', '0/0')))
    except (KeyError, ValueError, ZeroDivisionError) as exc:
        raise StickerError('Cannot verify duration or frame rate.') from exc
    if not math.isfinite(duration) or not 0 < duration <= 3:
        raise StickerError('Video duration must be greater than zero and no more than 3 seconds.')
    if not math.isfinite(fps) or not 0 < fps <= 30:
        raise StickerError('Video frame rate must be greater than zero and no more than 30 FPS.')
    return {'width': video['width'], 'height': video['height'], 'duration': duration,
            'fps': fps, 'size': path.stat().st_size}


def decoder_args(codec):
    return {'vp9': ['-c:v', 'libvpx-vp9'], 'vp8': ['-c:v', 'libvpx']}.get(codec, [])


def convert_static(source, output, *, start=0.):
    source, output = Path(source).resolve(), Path(output).resolve()
    if source == output:
        raise StickerError('Choose an output path different from the source.')
    if output.suffix.lower() not in ('.png', '.webp'):
        raise StickerError('Choose PNG or WEBP for a static sticker.')
    output.parent.mkdir(parents=True, exist_ok=True)
    if not math.isfinite(start) or start < 0:
        raise StickerError('Choose a valid frame time.')
    # A video source can also be compared as an image sticker, without reupload.
    try:
        with Image.open(source) as image:
            image.verify()
    except (OSError, UnidentifiedImageError):
        info = probe(source)
        stream = next((s for s in info.get('streams', []) if s.get('codec_type') == 'video'), {})
        with tempfile.TemporaryDirectory() as folder:
            frame = Path(folder) / 'frame.png'
            run_tool([os.getenv('FFMPEG', 'ffmpeg'), '-v', 'error', '-y',
                      *decoder_args(stream.get('codec_name')), '-ss', str(start), '-i', source,
                      '-frames:v', '1', frame])
            if not frame.exists():
                raise StickerError('No frame exists at the selected time.')
            return convert_static(frame, output)
    try:
        with Image.open(source) as original:
            image = ImageOps.exif_transpose(original).convert('RGBA')
            require_alpha = image.getchannel('A').getextrema()[0] < 255
            width, height = image.size
            scale = 512 / max(width, height)
            image = image.resize((max(1, round(width * scale)), max(1, round(height * scale))),
                                 Image.Resampling.LANCZOS)
            with tempfile.TemporaryDirectory(dir=output.parent) as folder:
                candidate = Path(folder) / ('sticker' + output.suffix.lower())
                image.save(candidate, lossless=True, optimize=True)
                validate_static(candidate, require_alpha)
                os.replace(candidate, output)
        return output
    except (OSError, UnidentifiedImageError, Image.DecompressionBombError) as exc:
        raise StickerError('Cannot read or write this image. Try a smaller PNG or WEBP.') from exc


def video_filter(duration, option, start, length, crop):
    if not all(math.isfinite(x) for x in (duration, start, length)) or duration <= 0:
        raise StickerError('Duration and time positions must be finite numbers.')
    if option not in ('speed', 'first', 'segment', 'frame', 'loop'):
        raise StickerError('Choose speed, first, segment, or loop.')
    if start < 0 or start >= duration or not 0 < length <= 3:
        raise StickerError('Choose a valid start time and a segment length up to 3 seconds.')
    if option == 'segment' and start + length > duration + 0.001:
        raise StickerError('The selected segment extends beyond the input.')
    if option in ('loop', 'frame'):
        # Matching-frame modes build their multi-input graph after analysis.
        return '', min(duration, 2.95)
    filters = []
    if option == 'speed':
        factor = duration / 2.95 if duration > 3 else 1
        filters.append(f'setpts=(PTS-STARTPTS)/{factor:.10g}')
        target = min(duration / factor, 2.99)
    else:
        begin = start if option == 'segment' else 0
        target = min(length, duration - begin, 2.99)
        filters.extend([f'trim=start={begin:.10g}:duration={target:.10g}', 'setpts=PTS-STARTPTS'])
    if crop:
        filters.append(crop)
    filters.extend(['fps=30', "scale='if(gte(iw,ih),512,max(2,trunc(iw*512/ih/2)*2))':'if(gte(iw,ih),max(2,trunc(ih*512/iw/2)*2),512)'", 'setsar=1'])
    return ','.join(filters).replace(',[r]', ';[r]').replace(',[f][back]', ';[f][back]'), target


def matching_loop(source, stream, crop):
    """Port of make_loop.py analysis, with decoding bounded before capture."""
    import numpy as np
    factor = sticker_loop.ANALYSIS_SIDE / max(stream['width'], stream['height'])
    aw, ah = max(2, round(stream['width'] * factor)), max(2, round(stream['height'] * factor))
    analysis = f'fps=30,scale={aw}:{ah}:flags=area,format=rgba'
    result = run_tool([os.getenv('FFMPEG', 'ffmpeg'), '-v', 'error',
                       *decoder_args(stream.get('codec_name')), '-i', source,
                       '-vf', analysis, '-frames:v', str(sticker_loop.MAX_FRAMES),
                       '-f', 'rawvideo', '-pix_fmt', 'rgba', '-'])
    frame_bytes = aw * ah * 4
    count = len(result.stdout) // frame_bytes
    if count < 12:
        raise StickerError('The clip is too short to find a loop. Use first seconds instead.')
    frames = np.frombuffer(result.stdout[:count * frame_bytes], dtype=np.uint8).reshape(count, ah, aw, 4).astype(np.float32)
    frames[..., :3] *= frames[..., 3:4] / 255.0
    distances = sticker_loop.distance_matrix(frames.reshape(count, -1))
    minimum = max(6, min(30, (count - 2 * sticker_loop.WIN) // 3))
    found = sticker_loop.find_loop(distances, minimum, round(2.95 * 30))
    if found is None:
        fade = min(8, count // 4)
        start, end, cost = 0, count - fade, float(distances[0, count - 1])
    else:
        start, end, cost = found
        fade = 0 if cost < 1.5 else (4 if cost < 5 else 8)
    fade = max(0, min(fade, count - end, end - start - 1))
    duration = (end - start) / 30
    speed = duration / 2.95 if duration > 2.95 else 1
    chain = sticker_loop.build_filter(start, end, fade, speed, 30)
    if crop:
        chain = chain.replace('[0:v]fps=30,', f'[0:v]fps=30,{crop},', 1)
    return chain, min(duration / speed, 2.99)


def detect_crop(source, start=0):
    result = run_tool([os.getenv('FFMPEG', 'ffmpeg'), '-hide_banner', '-ss', str(start), '-i', source,
                       '-t', '3', '-vf', 'cropdetect=limit=24:round=2:reset=0', '-an', '-f', 'null', '-'])
    matches = re.findall(r'crop=(\d+):(\d+):(\d+):(\d+)', result.stderr.decode('utf-8', errors='replace'))
    # cropdetect's cumulative rectangle preserves content seen during the sample.
    if not matches:
        return None
    w, h, x, y = map(int, matches[-1])
    return f'crop={w}:{h}:{x}:{y}' if w > 0 and h > 0 else None


def encode_matching_loop(source, output, chain, target, codec):
    """Original loop script's quality search and frame-rate fallback."""
    output = Path(output)
    candidate = output.with_name('loop-candidate.webm')
    for fps in (30, 24, 20, 15):
        filters = chain.replace(',fps=30[v]', f',fps={fps}[v]')
        lo, hi, found = 16, 63, False
        while lo <= hi:
            crf = (lo + hi) // 2
            run_tool([os.getenv('FFMPEG', 'ffmpeg'), '-y', '-v', 'error',
                      *decoder_args(codec), '-i', source, '-filter_complex', filters,
                      '-map', '[v]', '-t', f'{target:.10g}', '-r', str(fps),
                      '-c:v', 'libvpx-vp9', '-pix_fmt', 'yuva420p', '-crf', str(crf), '-b:v', '0',
                      '-auto-alt-ref', '0', '-row-mt', '1', '-an', candidate])
            if candidate.stat().st_size <= MAX_VIDEO_BYTES:
                os.replace(candidate, output)
                found = True
                hi = crf - 1
            else:
                lo = crf + 1
        if found:
            return output
    raise StickerError('Cannot fit this loop within 256 KB. Try a shorter or simpler clip.')


def convert_video(source, output, *, option='speed', start=0., length=3., crop_black=True):
    source, output = Path(source).resolve(), Path(output).resolve()
    if source == output or output.suffix.lower() != '.webm':
        raise StickerError('Choose a different output path with the WEBM extension.')
    info = probe(source)
    if not any(s.get('codec_type') == 'video' for s in info.get('streams', [])):
        raise StickerError('The input contains no video stream.')
    still = False
    try:
        with Image.open(source) as image:
            still = getattr(image, 'n_frames', 1) == 1
    except (OSError, UnidentifiedImageError):
        pass
    stream = next(s for s in info['streams'] if s.get('codec_type') == 'video')
    try:
        duration = float(info['format']['duration'])
    except (KeyError, ValueError) as exc:
        if not still:
            raise StickerError('This input has no readable video duration.') from exc
        duration = 2.99
    # Validate options before launching crop detection.
    video_filter(duration, option, start if option == 'segment' else 0, length, None)
    crop = detect_crop(source, start if option in ('segment', 'frame') else 0) if crop_black else None
    if option in ('loop', 'frame') and not still:
        chain, target = matching_loop(source, stream, crop)
        mapped = True
    else:
        chain, target = video_filter(duration, 'first' if still else option, start if option == 'segment' else 0, length, crop)
        if still:
            chain = 'loop=loop=-1:size=1:start=0,setpts=N/(30*TB),' + chain
        mapped = False
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=output.parent) as folder:
        candidate = Path(folder) / 'sticker.webm'
        if mapped:
            encode_matching_loop(source, candidate, chain, target, stream.get('codec_name'))
            validate_video(candidate)
            os.replace(candidate, output)
            return output
        # CRF 24 is the batch default. Later attempts progressively reduce the
        # bitrate while preserving the mandatory 512-pixel side.
        for attempt in range(9):
            bitrate = 0 if attempt == 0 else max(12, int(480 * 0.65 ** (attempt - 1)))
            crf = min(24 + 4 * attempt, 56)
            run_tool([os.getenv('FFMPEG', 'ffmpeg'), '-y', '-v', 'error',
                      *decoder_args(stream.get('codec_name')), '-i', source,
                      '-filter_complex', chain, *(['-map', '[v]'] if mapped else []), '-t', f'{target:.10g}', '-r', '30',
                      '-c:v', 'libvpx-vp9', '-pix_fmt', 'yuva420p', '-b:v', f'{bitrate}k',
                      '-crf', str(crf), '-auto-alt-ref', '0', '-row-mt', '1', '-deadline', 'good', '-cpu-used', '4', '-an', '-sn', '-dn',
                      '-map_metadata', '-1', candidate])
            if candidate.stat().st_size <= MAX_VIDEO_BYTES:
                validate_video(candidate)
                os.replace(candidate, output)
                return output
    raise StickerError('Cannot fit this clip within 256 KB. Choose a simpler frame or shorter segment.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['static', 'video'])
    parser.add_argument('source')
    parser.add_argument('output')
    parser.add_argument('--option', default='speed', choices=['speed', 'first', 'segment', 'frame', 'loop'])
    parser.add_argument('--start', type=float, default=0)
    parser.add_argument('--length', type=float, default=3)
    parser.add_argument('--no-crop', action='store_true')
    args = parser.parse_args()
    try:
        if args.mode == 'static':
            output = convert_static(args.source, args.output, start=args.start)
        else:
            output = convert_video(args.source, args.output, option=args.option, start=args.start,
                                   length=args.length, crop_black=not args.no_crop)
        print(json.dumps({'path': str(output), 'size': output.stat().st_size}))
    except StickerError as exc:
        print(json.dumps({'error': str(exc)}))
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
