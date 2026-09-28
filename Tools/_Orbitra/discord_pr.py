"""Уведомление о новом PR; запускается только из доверенной базовой ветки."""

import json
import os
import re
import sys
from pathlib import Path

from discord_changelog import EMBED_COLOR, plain_text, send_payload, split_text, webhook_url


def short(text, limit):
    return split_text(plain_text(' '.join(str(text).split())), limit)[0] if str(text).strip() else '-'


def payload(event, repository):
    if event.get('action') != 'opened':
        return None
    if not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', repository):
        raise ValueError('Некорректный репозиторий.')
    pr = event['pull_request']
    number = pr['number']
    if type(number) is not int or number <= 0:
        raise ValueError('Некорректный номер PR.')
    if pr['base']['repo']['full_name'] != repository:
        raise ValueError('Базовый репозиторий PR не совпадает с workflow.')
    url = f'https://github.com/{repository}/pull/{number}'
    draft = pr.get('draft', False)
    embed = {
        'author': {'name': '🛰️ ORBITRA • Pull requests', 'url': f'https://github.com/{repository}'},
        'title': f'🆕 PR #{number} • ' + short(pr['title'], 200),
        'url': url,
        'description': '📝 Черновик' if draft else '👀 Готов к ревью',
        'color': EMBED_COLOR,
        'fields': [
            {'name': '👤 Автор', 'value': short(pr['user']['login'], 100), 'inline': True},
            {'name': '🌿 Ветки', 'value': short(pr['head'].get('label', pr['head']['ref']), 240)
             + ' → ' + short(pr['base']['ref'], 240), 'inline': False},
            {'name': '🔗 Просмотр', 'value': f'[Открыть PR]({url}) • [Изменённые файлы]({url}/files)', 'inline': False},
        ],
        'footer': {'text': 'Orbitra • Открыт новый PR, изменения ещё не приняты'},
    }
    return {'username': 'Orbitra • Разработка', 'allowed_mentions': {'parse': []}, 'embeds': [embed]}


def main():
    event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text(encoding='utf-8'))
    message = payload(event, os.environ['GITHUB_REPOSITORY'])
    if message is None:
        print('Событие не требует уведомления.')
        return
    if os.environ.get('PR_NOTIFY_DRY_RUN', '').lower() == 'true':
        print(json.dumps(message, ensure_ascii=False, indent=2))
        return
    raw_url = os.environ.get('DISCORD_WEBHOOK_URL', '')
    if not raw_url:
        raise ValueError('Добавьте Actions secret ORBITRA_CHANGELOG_WEBHOOK.')
    send_payload(webhook_url(raw_url), message)
    print('Уведомление о новом PR отправлено.')


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(str(error) if isinstance(error, (ValueError, RuntimeError)) else 'Ошибка уведомления о PR.', file=sys.stderr)
        sys.exit(1)
