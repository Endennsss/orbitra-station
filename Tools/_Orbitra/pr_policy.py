"""Проверка минимального контракта описания PR Orbitra."""

import json
import os
import re
import sys
from pathlib import Path


TITLE = re.compile(r'^(add|fix|tweak|remove|refactor|docs|test|build|ci|chore|upstream|revert): \S.*[^.\s]$')
REQUIRED = ('Что изменено', 'Зачем', 'Проверки', 'Авторы и вклад')


def validate(pr):
    errors = []
    title = pr.get('title', '')
    if not TITLE.fullmatch(title) or not re.search('[А-Яа-яЁё]', title):
        errors.append('Заголовок: <type>: <описание на русском>, без точки в конце.')
    body = re.sub(r'<!--.*?-->', '', pr.get('body') or '', flags=re.DOTALL)
    sections = {}
    heading = None
    for line in body.splitlines():
        match = re.match(r'^##\s+(.+?)\s*$', line)
        if match:
            heading = match[1]
            sections.setdefault(heading, [])
        elif heading:
            sections[heading].append(line)
    for heading in REQUIRED:
        content = '\n'.join(sections.get(heading, [])).strip()
        if not content or content in ('...', '…', '-', 'TODO'):
            errors.append(f'Заполните раздел «{heading}».')
    return errors


if __name__ == '__main__':
    event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text(encoding='utf-8'))
    errors = validate(event['pull_request'])
    for error in errors:
        print(error)
    sys.exit(bool(errors))
