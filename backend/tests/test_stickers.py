import json
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from PIL import Image
from backend.stickers import StickerError, convert_static, convert_video, validate_video, validate_static, video_filter


class LoopScriptTests(unittest.TestCase):
    def test_original_quality_search_and_fps_fallback(self):
        from backend.stickers import encode_matching_loop
        attempts = []
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / 'output.webm'

            def render(args, **kwargs):
                crf = int(args[args.index('-crf') + 1])
                fps = int(args[args.index('-r') + 1])
                attempts.append((fps, crf))
                Path(args[-1]).write_bytes(b'x' * (270000 if fps == 30 or crf < 40 else 100))

            with patch('backend.stickers.run_tool', side_effect=render):
                encode_matching_loop('input', output, 'fps=30[v]', 2.95, 'h264')
            self.assertEqual(output.stat().st_size, 100)
            self.assertIn((30, 63), attempts)
            self.assertIn((24, 40), attempts)
            self.assertNotIn(20, [fps for fps, _ in attempts])

    def test_matching_frames_select_a_moving_loop(self):
        import numpy as np
        from backend.sticker_loop import distance_matrix, find_loop, build_filter
        phase = np.arange(120) * (2 * np.pi / 30)
        frames = np.column_stack([np.sin(phase), np.cos(phase)]) * 100
        found = find_loop(distance_matrix(frames), 20, 89)
        self.assertIsNotNone(found)
        start, end, cost = found
        self.assertIn(end - start, (30, 60))
        self.assertLess(cost, 0.001)
        chain = build_filter(start, end, 4, 1, 30)
        self.assertIn('blend=', chain)
        self.assertNotIn('reverse', chain)


class StickerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def test_static_dimensions_and_alpha(self):
        source = self.root / 'source.png'
        image = Image.new('RGBA', (160, 80), (255, 0, 0, 0))
        image.paste((255, 0, 0, 128), (40, 20, 120, 60))
        image.save(source)
        for extension in ('png', 'webp'):
            output = convert_static(source, self.root / f'out.{extension}')
            with Image.open(output) as result:
                self.assertEqual(result.size, (512, 256))
                self.assertEqual(result.convert('RGBA').getpixel((0, 0))[3], 0)
                self.assertEqual(result.convert('RGBA').getpixel((256, 128))[3], 128)
            validate_static(output, require_alpha=True)

    def test_bad_static_dimensions_and_extension(self):
        path = self.root / 'bad.png'
        Image.new('RGB', (300, 300)).save(path)
        with self.assertRaisesRegex(StickerError, '512'):
            validate_static(path)
        with self.assertRaises(StickerError):
            convert_static(path, self.root / 'bad.jpg')

    def test_video_rule_rejections(self):
        output = self.root / 'out.webm'
        output.write_bytes(b'\x1a\x45\xdf\xa3\x42\x82\x84webm')
        base = {'streams': [{'codec_type': 'video', 'codec_name': 'vp9', 'width': 512,
                            'height': 256, 'avg_frame_rate': '30/1'}],
                'format': {'format_name': 'matroska,webm', 'duration': '2.99'}}
        for field, value in [('width', 510), ('height', 514), ('codec_name', 'h264'),
                             ('avg_frame_rate', '31/1'), ('avg_frame_rate', '0/0')]:
            data = json.loads(json.dumps(base))
            data['streams'][0][field] = value
            with patch('backend.stickers.probe', return_value=data), self.assertRaises(StickerError):
                validate_video(output)
        for duration in ('3.01', 'nan', '0'):
            data = json.loads(json.dumps(base))
            data['format']['duration'] = duration
            with patch('backend.stickers.probe', return_value=data), self.assertRaises(StickerError):
                validate_video(output)
        data = json.loads(json.dumps(base))
        data['streams'].append({'codec_type': 'audio'})
        with patch('backend.stickers.probe', return_value=data), self.assertRaisesRegex(StickerError, 'audio'):
            validate_video(output)
        output.write_bytes(b'x' * (256 * 1024 + 1))
        with self.assertRaisesRegex(StickerError, '256'):
            validate_video(output)

    def test_batch_parameters_and_options(self):
        chain, duration = video_filter(12, 'speed', 0, 3, None)
        self.assertIn('setpts=(PTS-STARTPTS)/4.067', chain)
        self.assertIn('fps=30', chain)
        self.assertIn('512', chain)
        self.assertLessEqual(duration, 2.99)
        with self.assertRaises(StickerError):
            video_filter(5, 'segment', 4, 2, None)
        with self.assertRaises(StickerError):
            video_filter(5, 'frame', float('nan'), 3, None)

    def test_compression_retries_and_never_returns_oversize(self):
        source = self.root / 'input.mp4'
        output = self.root / 'out.webm'
        metadata = {'streams': [{'codec_type': 'video'}], 'format': {'duration': '4'}}
        bitrates = []

        def encode(arguments, **kwargs):
            bitrates.append(arguments[arguments.index('-b:v') + 1])
            Path(arguments[-1]).write_bytes(b'webm' + b'x' * (270000 if len(bitrates) < 3 else 1000))

        with patch('backend.stickers.probe', return_value=metadata), \
             patch('backend.stickers.run_tool', side_effect=encode), \
             patch('backend.stickers.validate_video') as validate:
            self.assertEqual(convert_video(source, output, crop_black=False), output.resolve())
            self.assertEqual(bitrates, ['0k', '480k', '312k'])
            validate.assert_called_once()
        original = output.read_bytes()

        def oversize(arguments, **kwargs):
            Path(arguments[-1]).write_bytes(b'x' * 270000)

        with patch('backend.stickers.probe', return_value=metadata), \
             patch('backend.stickers.run_tool', side_effect=oversize), \
             self.assertRaisesRegex(StickerError, '256 KB'):
            convert_video(source, output, crop_black=False)
        self.assertEqual(output.read_bytes(), original)


@unittest.skipUnless(shutil.which('ffmpeg') and shutil.which('ffprobe'), 'FFmpeg tools required')
class RealVideoTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def make_clip(self):
        path = self.root / 'source.mp4'
        subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'lavfi', '-i',
                        'testsrc2=size=160x90:rate=60:duration=4', '-f', 'lavfi', '-i',
                        'sine=duration=4', '-c:v', 'libx264', '-c:a', 'aac', '-shortest', str(path)], check=True)
        return path

    def test_all_video_modes_meet_specs(self):
        source = self.make_clip()
        for mode in ('speed', 'first', 'segment', 'frame', 'loop'):
            output = convert_video(source, self.root / f'{mode}.webm', option=mode,
                                   start=0.5 if mode in ('segment', 'frame') else 0,
                                   length=1, crop_black=False)
            info = validate_video(output)
            self.assertEqual(max(info['width'], info['height']), 512)
            self.assertLessEqual(info['duration'], 3)
            self.assertLessEqual(info['fps'], 30)
            self.assertLessEqual(info['size'], 262144)
            # The matched-frame loop should avoid a large scene jump at its seam.
            result = subprocess.run(['ffmpeg', '-v', 'error', '-i', str(output), '-vf', 'scale=32:18,format=gray',
                                     '-f', 'rawvideo', '-'], capture_output=True, check=True)
            frames = [result.stdout[i:i + 576] for i in range(0, len(result.stdout), 576)]
            self.assertGreater(len(frames), 1)
            seam = sum(abs(a - b) for a, b in zip(frames[0], frames[-1])) / 576
            if mode in ('loop', 'frame'):
                self.assertLess(seam, 20)

    def test_same_source_can_be_converted_between_image_and_video_repeatedly(self):
        source = self.root / 'source.png'
        Image.new('RGBA', (128, 64), (200, 0, 0, 100)).save(source)
        for extension in ('png', 'webm', 'webp', 'webm'):
            output = self.root / f'result.{extension}'
            if extension == 'webm':
                convert_video(source, output)
                validate_video(output)
            else:
                convert_static(source, output)
                validate_static(output, require_alpha=True)
        clip = self.make_clip()
        validate_static(convert_static(clip, self.root / 'frame.png'))

    def test_black_border_crop(self):
        source = self.root / 'border.mp4'
        subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'lavfi', '-i',
                        'testsrc2=size=160x80:rate=30:duration=1', '-vf', 'pad=160:120:0:20:black',
                        '-c:v', 'libx264', str(source)], check=True)
        output = convert_video(source, self.root / 'crop.webm', option='first')
        info = validate_video(output)
        self.assertLess(info['height'], 384)


if __name__ == '__main__':
    unittest.main()
