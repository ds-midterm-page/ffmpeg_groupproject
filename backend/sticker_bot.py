"""Optional polling bot. Start explicitly with TELEGRAM_BOT_TOKEN set.

No bot connection is made merely by importing this module.
"""
import asyncio
import os
from pathlib import Path
import tempfile

from telegram import InlineKeyboardButton, InlineKeyboardMarkup, Update
from telegram.error import TelegramError
from telegram.ext import (Application, CallbackQueryHandler, CommandHandler, ContextTypes,
                          ConversationHandler, MessageHandler, filters)

from backend.stickers import StickerError, convert_static, convert_video

MODE, OPTION, TIMES = range(3)
ENCODE_GATE = asyncio.Semaphore(1)


def keyboard(items):
    return InlineKeyboardMarkup([[InlineKeyboardButton(text, callback_data=value)] for text, value in items])


async def start(update: Update, context: ContextTypes.DEFAULT_TYPE):
    context.user_data.clear()
    await update.effective_message.reply_text(
        'Send an image, GIF or video (up to 20 MB). Send transparent images as files to preserve transparency. '
        'Then choose a sticker format. /cancel ends the current conversion.')
    return ConversationHandler.END


async def receive(update: Update, context: ContextTypes.DEFAULT_TYPE):
    message = update.effective_message
    media = message.document or message.video or message.animation or (message.photo[-1] if message.photo else None)
    if media is None:
        return ConversationHandler.END
    if media.file_size and media.file_size > 20 * 1024 * 1024:
        await message.reply_text('Upload limit: 20 MB. Please send a smaller file.')
        return ConversationHandler.END
    context.user_data.clear()
    context.user_data['file_id'] = media.file_id
    await message.reply_text('Choose the sticker mode:', reply_markup=keyboard([
        ('Static PNG', 'static:png'), ('Static WEBP', 'static:webp'), ('Video WEBM', 'video')]))
    return MODE


async def choose_mode(update: Update, context: ContextTypes.DEFAULT_TYPE):
    query = update.callback_query
    await query.answer()
    if query.data.startswith('static:'):
        context.user_data['mode'] = 'static'
        context.user_data['extension'] = query.data.split(':')[1]
        await query.edit_message_text('Creating your static sticker…')
        return await convert_and_send(update, context)
    context.user_data.update(mode='video', extension='webm')
    await query.edit_message_text('Choose how to fit the video:', reply_markup=keyboard([
        ('Speed up to fit 3 seconds', 'speed'), ('Take the first 3 seconds', 'first'),
        ('Select a segment', 'segment'), ('Hold a selected frame as a loop', 'frame')]))
    return OPTION


async def choose_option(update: Update, context: ContextTypes.DEFAULT_TYPE):
    query = update.callback_query
    await query.answer()
    context.user_data['option'] = query.data
    if query.data in ('segment', 'frame'):
        prompt = 'Send start and length in seconds, e.g. 12.5 2.5 (length up to 3).' if query.data == 'segment' else 'Send the frame time in seconds, e.g. 12.5.'
        await query.edit_message_text(prompt)
        return TIMES
    await query.edit_message_text('Creating your video sticker…')
    return await convert_and_send(update, context)


async def choose_times(update: Update, context: ContextTypes.DEFAULT_TYPE):
    try:
        values = [float(value) for value in update.effective_message.text.split()]
        expected = 2 if context.user_data['option'] == 'segment' else 1
        if len(values) != expected:
            raise ValueError()
        context.user_data['start'] = values[0]
        context.user_data['length'] = values[1] if expected == 2 else 3
    except ValueError:
        await update.effective_message.reply_text('Enter seconds as numbers, with a space between start and length for a segment.')
        return TIMES
    return await convert_and_send(update, context)


async def convert_and_send(update: Update, context: ContextTypes.DEFAULT_TYPE):
    message = update.effective_message
    settings = dict(context.user_data)
    try:
        async with ENCODE_GATE:
            with tempfile.TemporaryDirectory(prefix='mediatool-bot-') as folder:
                source = Path(folder) / 'input'
                output = Path(folder) / ('sticker.' + settings['extension'])
                remote = await context.bot.get_file(settings['file_id'])
                await remote.download_to_drive(source)
                if settings['mode'] == 'static':
                    await asyncio.to_thread(convert_static, source, output)
                else:
                    await asyncio.to_thread(convert_video, source, output,
                                            option=settings.get('option', 'speed'),
                                            start=settings.get('start', 0), length=settings.get('length', 3))
                with output.open('rb') as sticker:
                    await message.reply_sticker(sticker=sticker)
                # Send the actual file as well, for use with @Stickers.
                with output.open('rb') as document:
                    await message.reply_document(document=document, filename=output.name,
                        caption=f'Validated sticker: {output.stat().st_size} bytes ({output.stat().st_size / 1024:.1f} KB).')
    except StickerError as exc:
        await message.reply_text(str(exc) + ' Send another file to try again.')
    except (TelegramError, OSError):
        await message.reply_text('Could not download or send this media. Please send a smaller file and try again.')
    finally:
        context.user_data.clear()
    return ConversationHandler.END


async def cancel(update: Update, context: ContextTypes.DEFAULT_TYPE):
    context.user_data.clear()
    await update.effective_message.reply_text('Cancelled. Send a new file whenever you are ready.')
    return ConversationHandler.END


def build_application(token):
    application = Application.builder().token(token).build()
    media = filters.Document.ALL | filters.VIDEO | filters.ANIMATION | filters.PHOTO
    application.add_handler(ConversationHandler(
        entry_points=[MessageHandler(media, receive), CommandHandler('start', start)],
        states={MODE: [CallbackQueryHandler(choose_mode, pattern=r'^(static:(png|webp)|video)$')],
                OPTION: [CallbackQueryHandler(choose_option, pattern=r'^(speed|first|segment|frame)$')],
                TIMES: [MessageHandler(filters.TEXT & ~filters.COMMAND, choose_times)]},
        fallbacks=[CommandHandler('cancel', cancel)], allow_reentry=True))
    return application


def main():
    token = os.getenv('TELEGRAM_BOT_TOKEN')
    if not token:
        raise SystemExit('Set TELEGRAM_BOT_TOKEN before starting the bot.')
    build_application(token).run_polling(allowed_updates=['message', 'callback_query'])


if __name__ == '__main__':
    main()
