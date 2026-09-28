"""Журнал изменений после ревью; выполняется только из доверенной базовой ветки."""

import json
import os
import re
import sys
from pathlib import Path

import requests

from discord_changelog import plain_text, split_text


SHA = re.compile(r'[0-9a-f]{40}')


def clean(text, limit=180):
    return split_text(plain_text(' '.join(str(text).split())), limit)[0] if str(text).strip() else '-'


def latest_approval(reviews, events):
    # DISMISSED сам по себе не означает бывший approval: сверяем исходное состояние в timeline.
    dismissed = {event['dismissed_review']['review_id'] for event in events
                 if event.get('event') == 'review_dismissed'
                 and event.get('dismissed_review', {}).get('state', '').lower() == 'approved'}
    approvals = [review for review in reviews
                 if (review.get('state', '').upper() == 'APPROVED' or review.get('id') in dismissed)
                 and SHA.fullmatch(review.get('commit_id', '')) and review.get('submitted_at')]
    return max(approvals, key=lambda review: (review['submitted_at'], review['id']), default=None)


def commit_lines(commits):
    lines = []
    for commit in commits[:30]:
        sha = commit['sha']
        if not SHA.fullmatch(sha):
            continue
        author = (commit.get('author') or {}).get('login') or commit['commit']['author']['name']
        subject = commit['commit']['message'].split('\n', 1)[0]
        lines.append(f'- `{sha[:12]}` - {clean(author, 80)}: {clean(subject)}')
    return lines


def audit_body(repository, number, head, sender, review, comparison):
    base = review['commit_id']
    url = f'https://github.com/{repository}'
    lines = [f'<!-- orbitra-review-audit:{number}:{head} -->', '### 🔎 Изменения после одобрения', '',
             f'Последнее одобрение: **{clean(review["user"]["login"])}**, '
             f'коммит [`{base[:12]}`]({url}/commit/{base}).',
             f'Новые изменения отправил: **{clean(sender)}**. Текущая ревизия: `{head[:12]}`.', '',
             f'[Сравнить с одобренной версией]({url}/compare/{base}...{head})', '']
    if comparison is None:
        lines.append('Сравнение недоступно: одобренный коммит мог исчезнуть после переписывания истории. '
                     'Проверьте весь diff PR заново.')
    else:
        if comparison.get('status') in ('diverged', 'behind'):
            lines.append('История разошлась с одобренной версией: требуется повторная проверка всего diff.')
        lines.extend(commit_lines(comparison.get('commits', [])))
        lines.append(f'\nКоммитов в сравнении: **{comparison.get("total_commits", 0)}**. '
                     'Выше показаны первые 30; полный список доступен по ссылке.')
        files = comparison.get('files', [])
        if files:
            lines.extend(['', '**Затронутые файлы (первые 20):**'])
            lines.extend(f'- {clean(file["filename"], 200)} (+{file["additions"]}/−{file["deletions"]})'
                         for file in files[:20])
    lines.extend(['', 'Старое одобрение относится к указанному коммиту. '
                  'Перед слиянием нужно повторное ревью актуальной версии.'])
    return '\n'.join(lines)


def merged_body(repository, pr, commits, reviews):
    sha = pr['merge_commit_sha']
    decisions = {}
    for review in sorted(reviews, key=lambda item: (item.get('submitted_at') or '', item['id'])):
        if review.get('state', '').upper() in ('APPROVED', 'CHANGES_REQUESTED', 'DISMISSED'):
            decisions[review['user']['login']] = review
    reviewers = sorted(name for name, decision in decisions.items()
                       if decision.get('state', '').upper() == 'APPROVED'
                       and decision.get('commit_id') == pr['head']['sha'])
    lines = [f'<!-- orbitra-merge-summary:{pr["number"]}:{sha} -->', '### ✅ Итог принятого PR', '',
             f'**Изменение:** {clean(pr["title"], 240)}',
             f'**Автор PR:** {clean(pr["user"]["login"])}',
             f'**Принял:** {clean((pr.get("merged_by") or {}).get("login", "Не указан"))}',
             f'**Одобрили финальную ревизию:** {", ".join(clean(name, 80) for name in reviewers[:20]) or "Нет approval на финальный SHA"}',
             f'**Ревизия:** [{sha[:12]}](https://github.com/{repository}/commit/{sha})', '',
             '**Коммиты и авторы (первые 30):**', *commit_lines(commits)]
    return '\n'.join(lines)


class GitHub:
    def __init__(self, repository, token):
        if not re.fullmatch(r'[\w.-]+/[\w.-]+', repository):
            raise ValueError('Некорректное имя репозитория.')
        self.root = f'https://api.github.com/repos/{repository}'
        self.headers = {'Authorization': f'Bearer {token}', 'Accept': 'application/vnd.github+json',
                        'X-GitHub-Api-Version': '2022-11-28'}

    def request(self, path, method='GET', payload=None, optional=False):
        try:
            response = requests.request(method, self.root + path, headers=self.headers,
                                        json=payload, timeout=30, allow_redirects=False)
        except requests.RequestException:
            raise RuntimeError('Ошибка сети GitHub API.') from None
        if optional and response.status_code in (404, 422):
            return None
        if response.status_code not in (200, 201):
            raise RuntimeError(f'GitHub API вернул HTTP {response.status_code}.')
        return response.json()

    def pages(self, path):
        result = []
        for page in range(1, 101):
            items = self.request(f'{path}?per_page=100&page={page}')
            result.extend(items)
            if len(items) < 100:
                return result
        raise RuntimeError('Превышен предел страниц GitHub API; журнал не опубликован.')


def run(event, repository, api):
    pr = event['pull_request']
    number = pr['number']
    if type(number) is not int or number <= 0:
        raise ValueError('Некорректный номер PR.')
    action = event['action']
    if action == 'synchronize':
        head = event.get('after', '')
        if not SHA.fullmatch(head):
            raise ValueError('Некорректная ревизия PR.')
        current = api.request(f'/pulls/{number}')
        if current['state'] != 'open' or current['head']['sha'] != head:
            return
        reviews = api.pages(f'/pulls/{number}/reviews')
        review = latest_approval(reviews, api.pages(f'/issues/{number}/timeline'))
        if review is None or review['commit_id'] == head:
            return
        comparison = api.request(f'/compare/{review["commit_id"]}...{head}?per_page=100', optional=True)
        body = audit_body(repository, number, head, event['sender']['login'], review, comparison)
    elif action == 'closed' and pr.get('merged'):
        pr = api.request(f'/pulls/{number}')
        if not pr.get('merged') or not SHA.fullmatch(pr.get('merge_commit_sha') or ''):
            return
        body = merged_body(repository, pr, api.pages(f'/pulls/{number}/commits'), api.pages(f'/pulls/{number}/reviews'))
    else:
        return
    marker = body.split('\n', 1)[0]
    comments = api.pages(f'/issues/{number}/comments')
    if any(comment.get('user', {}).get('login') == 'github-actions[bot]'
           and marker in comment.get('body', '') for comment in comments):
        return
    # Новая запись сохраняет историю; прошлые комментарии не перезаписываются.
    api.request(f'/issues/{number}/comments', 'POST', {'body': body})


if __name__ == '__main__':
    try:
        event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text(encoding='utf-8'))
        repository = os.environ['GITHUB_REPOSITORY']
        run(event, repository, GitHub(repository, os.environ['GITHUB_TOKEN']))
    except Exception:
        print('Не удалось сформировать журнал PR. Проверьте доступ GitHub API и данные события.', file=sys.stderr)
        sys.exit(1)
