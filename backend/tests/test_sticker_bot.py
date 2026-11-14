from pathlib import Path
from types import SimpleNamespace
import unittest
from unittest.mock import AsyncMock, patch

from backend import sticker_bot as bot


class BotTests(unittest.IsolatedAsyncioTestCase):
    def context(self):
        return SimpleNamespace(user_data={}, bot=AsyncMock())

    def update(self, data=None, text=''):
        message = SimpleNamespace(reply_text=AsyncMock(), reply_sticker=AsyncMock(), reply_document=AsyncMock(), text=text)
        return SimpleNamespace(effective_message=message,
            callback_query=SimpleNamespace(data=data, answer=AsyncMock(), edit_message_text=AsyncMock()))

    async def test_video_mode_and_segment_prompt(self):
        context = self.context()
        self.assertEqual(await bot.choose_mode(self.update('video'), context), bot.OPTION)
        self.assertEqual(context.user_data['mode'], 'video')
        self.assertEqual(await bot.choose_option(self.update('segment'), context), bot.TIMES)
        self.assertEqual(context.user_data['option'], 'segment')

    async def test_bad_time_is_reprompted(self):
        context = self.context()
        context.user_data['option'] = 'segment'
        self.assertEqual(await bot.choose_times(self.update(text='bad'), context), bot.TIMES)

    async def test_times_are_forwarded(self):
        context = self.context()
        context.user_data['option'] = 'segment'
        with patch.object(bot, 'convert_and_send', new_callable=AsyncMock, return_value=-1) as send:
            await bot.choose_times(self.update(text='12.5 2.5'), context)
            self.assertEqual(context.user_data['start'], 12.5)
            self.assertEqual(context.user_data['length'], 2.5)
            send.assert_awaited_once()

    async def test_static_conversion_sends_sticker_and_file_then_cleans_up(self):
        context = self.context()
        context.user_data.update(file_id='test-file', mode='static', extension='png')
        update = self.update()
        created = []

        def convert(source, output):
            Path(output).write_bytes(b'converted')
            created.append(Path(output))
            return output

        with patch.object(bot, 'convert_static', side_effect=convert):
            self.assertEqual(await bot.convert_and_send(update, context), -1)
        update.effective_message.reply_sticker.assert_awaited_once()
        update.effective_message.reply_document.assert_awaited_once()
        self.assertFalse(created[0].exists())
        self.assertEqual(context.user_data, {})

    async def test_cancel_resets_choices(self):
        context = self.context()
        context.user_data['file_id'] = 'test-file'
        self.assertEqual(await bot.cancel(self.update(), context), -1)
        self.assertEqual(context.user_data, {})
