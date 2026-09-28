"""Публикация только новых записей Orbitra в Discord."""

import json
import os
import re
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path
from urllib.parse import parse_qsl, urlencode, urlsplit, urlunsplit

import requests
import yaml


CHANGELOG = 'Resources/Changelog/_Orbitra/updates.yml'
CATEGORIES = {'Add': 'Добавлено', 'Fix': 'Исправлено', 'Tweak': 'Изменено', 'Remove': 'Удалено'}
CATEGORY_ICONS = {'Add': '✨', 'Fix': '🔧', 'Tweak': '⚙️', 'Remove': '🗑️'}
EMBED_COLOR = 0x353535
REPOSITORY_URL = 'https://github.com/Endennsss/orbitra-station'


def load_entries(text):
    document = yaml.safe_load(text)
    if not isinstance(document, dict) or document.get('Name') != 'OrbitraUpdates' or document.get('AdminOnly') is not False:
        raise ValueError('Ожидался публичный ченджлог OrbitraUpdates.')
    entries = document.get('Entries')
    if not isinstance(entries, list):
        raise ValueError('Entries должен быть списком.')
    seen = set()
    for entry in entries:
        if (not isinstance(entry, dict) or type(entry.get('id')) is not int
                or not 0 <= entry['id'] <= 2147483647 or entry['id'] in seen):
            raise ValueError('Некорректный или повторяющийся ID записи.')
        seen.add(entry['id'])
        if not isinstance(entry.get('author'), str) or not entry['author'].strip():
            raise ValueError('У записи должен быть автор.')
        if not isinstance(entry.get('changes'), list) or not entry['changes']:
            raise ValueError('У записи должны быть изменения.')
        for change in entry['changes']:
            if not isinstance(change, dict) or not isinstance(change.get('type'), str) or change['type'] not in CATEGORIES:
                raise ValueError('Неизвестный тип изменения.')
            if not isinstance(change.get('message'), str) or not change['message'].strip():
                raise ValueError('Пустой текст изменения.')
    return entries


def select_entries(current, previous):
    known = {entry['id'] for entry in previous}
    return sorted((entry for entry in current if entry['id'] not in known), key=lambda entry: entry['id'])


def plain_text(text):
    # Не позволяем тексту записи менять оформление или создавать упоминания.
    text = text.replace('—', '-').replace('–', '-')
    return re.sub(r'([\\`*_~|<>\[\]])', r'\\\1', text.strip()).replace('@', '@\u200b')


def text_length(text):
    return len(text.encode('utf-16-le')) // 2


def split_text(text, limit):
    # Экранированный символ остаётся целым даже на границе частей сообщения.
    tokens = re.findall(r'\\.|[^\\]|\\$', text, re.DOTALL)
    chunks = []
    current = ''
    length = 0
    for token in tokens:
        size = text_length(token)
        if length + size > limit and current:
            chunks.append(current)
            current, length = '', 0
        current += token
        length += size
    if current:
        chunks.append(current)
    return chunks


def entry_fields(entry):
    fields = []
    for kind, label in CATEGORIES.items():
        messages = [plain_text(change['message']) for change in entry['changes'] if change['type'] == kind]
        current = ''
        for message in messages:
            for part in split_text(message, 990):
                line = f'• {part}'
                if current and text_length(current + '\n' + line) > 1024:
                    fields.append({'name': f'{CATEGORY_ICONS[kind]} {label}', 'value': current, 'inline': False})
                    current = ''
                current = current + '\n' + line if current else line
        if current:
            fields.append({'name': f'{CATEGORY_ICONS[kind]} {label}', 'value': current, 'inline': False})
    return fields


def entry_timestamp(entry):
    try:
        value = datetime.fromisoformat(str(entry.get('time', '')).replace('Z', '+00:00'))
        return value.isoformat() if value.tzinfo is not None else None
    except ValueError:
        return None


def payloads(entries, revision=''):
    result = []
    url = f'{REPOSITORY_URL}/blob/{revision}/{CHANGELOG}' if re.fullmatch(r'[0-9a-f]{40}', revision) else f'{REPOSITORY_URL}/blob/master/{CHANGELOG}'
    for entry in entries:
        pages, page, length = [], [], 0
        for field in entry_fields(entry):
            size = text_length(field['name']) + text_length(field['value'])
            if page and (length + size > 4800 or len(page) == 20):
                pages.append(page)
                page, length = [], 0
            page.append(field)
            length += size
        if page:
            pages.append(page)
        authors = split_text(plain_text(entry['author']), 256)[0]
        for index, fields in enumerate(pages, 1):
            suffix = f' · {index}/{len(pages)}' if len(pages) > 1 else ''
            embed = {
                'author': {'name': '🛰️ ORBITRA • Журнал обновлений', 'url': REPOSITORY_URL},
                'title': f'📦 Обновление #{entry["id"]}{suffix}',
                'url': url,
                'description': f'👤 **Авторы:** {authors}\n📝 Изменений: **{len(entry["changes"])}**',
                'fields': fields,
                'color': EMBED_COLOR,
                'footer': {'text': 'Orbitra • Изменения в репозитории, не статус сервера'},
            }
            if timestamp := entry_timestamp(entry):
                embed['timestamp'] = timestamp
            result.append({
                'username': 'Orbitra • Обновления',
                'allowed_mentions': {'parse': []},
                'embeds': [embed],
            })
    return result


def webhook_url(raw):
    parts = urlsplit(raw.strip())
    if (parts.scheme != 'https' or parts.netloc not in {'discord.com', 'canary.discord.com', 'ptb.discord.com'}
            or not re.fullmatch(r'/api(?:/v\d+)?/webhooks/\d+/[\w.-]+/?', parts.path)):
        raise ValueError('Нужен обычный HTTPS webhook Discord без суффикса /github.')
    query = dict(parse_qsl(parts.query))
    query['wait'] = 'true'
    return urlunsplit((parts.scheme, parts.netloc, parts.path, urlencode(query), ''))


def send_payload(url, payload):
    for attempt in range(5):
        try:
            response = requests.post(url, json=payload, timeout=20, allow_redirects=False)
        except requests.RequestException:
            # Исключение requests может содержать URL с секретом. Не выводим его.
            raise RuntimeError('Ошибка сети при отправке. Проверьте канал перед повторным запуском.') from None
        if response.status_code in (200, 204):
            return
        if response.status_code == 429 and attempt < 4:
            try:
                delay = float(response.json().get('retry_after', 5))
            except (ValueError, TypeError, AttributeError):
                raise RuntimeError('Discord вернул некорректный интервал ожидания.') from None
            if not 0 <= delay <= 30:
                raise RuntimeError('Discord запросил длительное ожидание; отправка остановлена.')
            time.sleep(delay + 0.25)
            continue
        raise RuntimeError(f'Discord вернул HTTP {response.status_code}. Отправка остановлена.')


def main():
    current = load_entries(Path(CHANGELOG).read_text(encoding='utf-8-sig'))
    entry_id = os.environ.get('CHANGELOG_ENTRY_ID', '').strip()
    if entry_id:
        selected = [entry for entry in current if entry['id'] == int(entry_id)]
        if not selected:
            raise ValueError('Запись с указанным ID не найдена.')
    else:
        before = os.environ.get('CHANGELOG_BEFORE', '')
        if not re.fullmatch(r'[0-9a-f]{40}', before) or before == '0' * 40:
            raise ValueError('Нет предыдущего коммита. Используйте ручной запуск с конкретным ID.')
        # Ошибка чтения истории не должна приводить к публикации всего архива.
        previous = subprocess.run(['git', 'show', f'{before}:{CHANGELOG}'], check=True,
                                  capture_output=True, encoding='utf-8').stdout
        selected = select_entries(current, load_entries(previous))
    messages = payloads(selected, os.environ.get('GITHUB_SHA', ''))
    if os.environ.get('CHANGELOG_DRY_RUN', '').lower() == 'true':
        print(json.dumps(messages, ensure_ascii=False, indent=2))
        return
    if not messages:
        print('Новых записей Orbitra нет.')
        return
    raw_url = os.environ.get('DISCORD_WEBHOOK_URL', '')
    if not raw_url:
        raise ValueError('Добавьте Actions secret ORBITRA_CHANGELOG_WEBHOOK.')
    url = webhook_url(raw_url)
    for index, payload in enumerate(messages, 1):
        send_payload(url, payload)
        print(f'Отправлено сообщений: {index}/{len(messages)}')


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        # Даже ошибки сторонних библиотек не должны раскрывать URL вебхука.
        print(str(error) if isinstance(error, (ValueError, RuntimeError)) else 'Ошибка обработки ченджлога.', file=sys.stderr)
        sys.exit(1)
