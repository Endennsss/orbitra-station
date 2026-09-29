"""Публикация каждого коммита push в master в Discord."""

import json
import os
import re
import sys
from pathlib import Path

from discord_changelog import EMBED_COLOR, plain_text, send_payload, split_text, webhook_url


MAX_FILES = 8
REPOSITORY_URL = 'https://github.com/Endennsss/orbitra-station'


def short(text, limit):
    value = plain_text(' '.join(str(text or '').split()))
    return split_text(value, limit)[0] if value else '-'


def file_list(commit, key, label):
    files = commit.get(key) or []
    names = []
    for item in files:
        filename = item.get('filename') if isinstance(item, dict) else item
        if filename:
            names.append(short(filename, 100))
    if not names:
        return None
    visible = names[:MAX_FILES]
    if len(names) > MAX_FILES:
        visible.append(f'… и ещё {len(names) - MAX_FILES}')
    lines = []
    for name in visible:
        line = f'• `{name}`'
        candidate = '\n'.join(lines + [line])
        if len(candidate.encode('utf-16-le')) // 2 > 1000:
            break
        lines.append(line)
    return {'name': label, 'value': '\n'.join(lines), 'inline': False}


def payload(commit, repository, sender=''):
    if not isinstance(commit, dict):
        raise ValueError('Коммит должен быть объектом.')
    sha = commit.get('id', '')
    if not re.fullmatch(r'[0-9a-f]{7,40}', str(sha)):
        raise ValueError('Некорректный SHA коммита.')
    if not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', repository):
        raise ValueError('Некорректный репозиторий.')

    sha = str(sha)
    url = f'https://github.com/{repository}/commit/{sha}'
    author = commit.get('author') or {}
    committer = commit.get('committer') or {}
    fields = [
        {'name': '👤 Автор', 'value': short(author.get('name') or author.get('email'), 180), 'inline': True},
        {'name': '📤 Отправитель', 'value': short(sender, 180), 'inline': True},
    ]
    for key, label in (
        ('added', '➕ Добавлено'),
        ('modified', '✏️ Изменено'),
        ('removed', '➖ Удалено'),
    ):
        field = file_list(commit, key, label)
        if field:
            fields.append(field)

    message = short(commit.get('message'), 900)
    committer_name = short(committer.get('name') or committer.get('email'), 180)
    fields.append({'name': '🧾 Git committer', 'value': committer_name, 'inline': True})
    fields.append({'name': '🔗 Просмотр', 'value': f'[Открыть коммит]({url})', 'inline': False})
    return {
        'username': 'Orbitra • Коммиты',
        'allowed_mentions': {'parse': []},
        'embeds': [{
            'author': {'name': '🛰️ ORBITRA • История коммитов', 'url': REPOSITORY_URL},
            'title': f'📌 Коммит {sha[:7]} • {short(message.splitlines()[0] if message else "без сообщения", 200)}',
            'url': url,
            'description': message,
            'color': EMBED_COLOR,
            'fields': fields,
            'footer': {'text': 'Orbitra • Изменения в репозитории'},
        }],
    }


def main():
    event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text(encoding='utf-8'))
    repository = os.environ.get('GITHUB_REPOSITORY', '')
    commits = event.get('commits') or []
    if not commits:
        print('В push нет коммитов для публикации.')
        return
    messages = [payload(commit, repository, event.get('sender', {}).get('login', '')) for commit in commits]
    if os.environ.get('COMMIT_NOTIFY_DRY_RUN', '').lower() == 'true':
        print(json.dumps(messages, ensure_ascii=False, indent=2))
        return
    raw_url = os.environ.get('DISCORD_WEBHOOK_URL', '')
    if not raw_url:
        raise ValueError('Добавьте Actions secret ORBITRA_CHANGELOG_WEBHOOK.')
    url = webhook_url(raw_url)
    for index, message in enumerate(messages, 1):
        send_payload(url, message)
        print(f'Отправлено сообщений: {index}/{len(messages)}')


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(str(error) if isinstance(error, (ValueError, RuntimeError)) else 'Ошибка уведомления о коммите.', file=sys.stderr)
        sys.exit(1)
