"""Публикация только новых записей Orbitra в Discord."""

import json
import os
import re
import subprocess
import sys
import time
from pathlib import Path
from urllib.parse import parse_qsl, urlencode, urlsplit, urlunsplit

import requests
import yaml


CHANGELOG = 'Resources/Changelog/_Orbitra/updates.yml'
CATEGORIES = {'Add': 'Добавлено', 'Fix': 'Исправлено', 'Tweak': 'Изменено', 'Remove': 'Удалено'}


def load_entries(text):
    document = yaml.safe_load(text)
    if not isinstance(document, dict) or document.get('Name') != 'OrbitraUpdates' or document.get('AdminOnly') is not False:
        raise ValueError('Ожидался публичный ченджлог OrbitraUpdates.')
    entries = document.get('Entries')
    if not isinstance(entries, list):
        raise ValueError('Entries должен быть списком.')
    seen = set()
    for entry in entries:
        if not isinstance(entry, dict) or type(entry.get('id')) is not int or entry['id'] in seen:
            raise ValueError('Некорректный или повторяющийся ID записи.')
        seen.add(entry['id'])
        if not isinstance(entry.get('author'), str) or not entry['author'].strip():
            raise ValueError('У записи должен быть автор.')
        if not isinstance(entry.get('changes'), list) or not entry['changes']:
            raise ValueError('У записи должны быть изменения.')
        for change in entry['changes']:
            if not isinstance(change, dict) or change.get('type') not in CATEGORIES:
                raise ValueError('Неизвестный тип изменения.')
            if not isinstance(change.get('message'), str) or not change['message'].strip():
                raise ValueError('Пустой текст изменения.')
    return entries


def select_entries(current, previous):
    known = {entry['id'] for entry in previous}
    return sorted((entry for entry in current if entry['id'] not in known), key=lambda entry: entry['id'])


def plain_text(text):
    # Не позволяем тексту записи менять оформление или создавать упоминания.
    return re.sub(r'([\\`*_~|<>\[\]])', r'\\\1', text.strip()).replace('@', '@\u200b')


def payloads(entries):
    result = []
    for entry in entries:
        sections = []
        for kind, label in CATEGORIES.items():
            messages = [plain_text(change['message']) for change in entry['changes'] if change['type'] == kind]
            if messages:
                sections.append(f'**{label}**\n' + '\n'.join(f'• {message}' for message in messages))
        description = '\n\n'.join(sections)
        # Консервативный предел также оставляет запас для UTF-16 и заголовка.
        chunks = [description[offset:offset + 1800] for offset in range(0, len(description), 1800)]
        for index, chunk in enumerate(chunks, 1):
            suffix = f' ({index}/{len(chunks)})' if len(chunks) > 1 else ''
            result.append({
                'username': 'Orbitra • Обновления',
                'allowed_mentions': {'parse': []},
                'embeds': [{
                    'title': f'Обновление Orbitra #{entry["id"]}{suffix}',
                    'description': chunk,
                    'color': 0xC7A35C,
                    'footer': {'text': f'Автор: {entry["author"][:200]}'},
                }],
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
            delay = float(response.json().get('retry_after', 5))
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
    messages = payloads(selected)
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
