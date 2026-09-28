import copy
import json
import os
import unittest
from unittest.mock import patch

from discord_changelog import EMBED_COLOR, payloads, plain_text, text_length
from discord_pr import main, payload


class DiscordPrTest(unittest.TestCase):
    def setUp(self):
        self.repository = 'Endennsss/orbitra-station'
        self.event = {'action': 'opened', 'pull_request': {
            'number': 12, 'title': 'fix: тест — цвет', 'draft': False,
            'user': {'login': 'author'}, 'head': {'ref': 'feature', 'label': 'fork:feature'},
            'base': {'ref': 'master', 'repo': {'full_name': self.repository}},
        }}

    def test_new_pr_is_linked_and_uses_shared_gray_palette(self):
        message = payload(self.event, self.repository)
        embed = message['embeds'][0]
        self.assertEqual(embed['color'], EMBED_COLOR)
        self.assertEqual(embed['url'], f'https://github.com/{self.repository}/pull/12')
        self.assertIn('Готов к ревью', embed['description'])
        self.assertNotIn('—', embed['title'])
        self.assertEqual(message['allowed_mentions'], {'parse': []})

    def test_draft_status(self):
        self.event['pull_request']['draft'] = True
        self.assertIn('Черновик', payload(self.event, self.repository)['embeds'][0]['description'])

    def test_other_events_do_not_notify(self):
        for action in ['synchronize', 'closed', 'edited', 'reopened', 'ready_for_review']:
            self.event['action'] = action
            self.assertIsNone(payload(self.event, self.repository))

    def test_external_text_cannot_ping_or_exceed_limits(self):
        self.event['pull_request']['title'] = '@everyone **test** 😀' * 500
        embed = payload(self.event, self.repository)['embeds'][0]
        self.assertLessEqual(text_length(embed['title']), 256)
        self.assertNotIn('@everyone', embed['title'])
        self.assertNotIn('**test**', embed['title'])

    def test_repository_mismatch_is_rejected(self):
        with self.assertRaises(ValueError):
            payload(self.event, 'another/repository')

    def test_invalid_number_is_rejected(self):
        for number in [-1, True, '12']:
            event = copy.deepcopy(self.event)
            event['pull_request']['number'] = number
            with self.assertRaises(ValueError):
                payload(event, self.repository)

    def test_changelog_palette_and_dash_normalization(self):
        entry = {'id': 1, 'author': 'author', 'changes': [{'type': 'Fix', 'message': 'тест — да – да'}]}
        embed = payloads([entry])[0]['embeds'][0]
        self.assertEqual(embed['color'], EMBED_COLOR)
        self.assertEqual(plain_text('a — b – c'), 'a - b - c')
        self.assertEqual(embed['fields'][0]['name'], '🔧 Исправлено')

    def test_dry_run_never_sends(self):
        env = {'GITHUB_EVENT_PATH': 'event.json', 'GITHUB_REPOSITORY': self.repository, 'PR_NOTIFY_DRY_RUN': 'true'}
        with patch.dict(os.environ, env, clear=True), patch('discord_pr.Path.read_text', return_value=json.dumps(self.event)), \
                patch('discord_pr.send_payload') as send, patch('builtins.print'):
            main()
            send.assert_not_called()

    def test_opened_event_sends_one_message(self):
        env = {'GITHUB_EVENT_PATH': 'event.json', 'GITHUB_REPOSITORY': self.repository,
               'DISCORD_WEBHOOK_URL': 'https://discord.com/api/webhooks/123/test-token'}
        with patch.dict(os.environ, env, clear=True), patch('discord_pr.Path.read_text', return_value=json.dumps(self.event)), \
                patch('discord_pr.send_payload') as send, patch('builtins.print'):
            main()
            send.assert_called_once()
            self.assertIn('wait=true', send.call_args.args[0])
            self.assertEqual(send.call_args.args[1]['allowed_mentions'], {'parse': []})

    def test_missing_secret_reports_setup_error(self):
        env = {'GITHUB_EVENT_PATH': 'event.json', 'GITHUB_REPOSITORY': self.repository}
        with patch.dict(os.environ, env, clear=True), patch('discord_pr.Path.read_text', return_value=json.dumps(self.event)), \
                patch('discord_pr.send_payload') as send:
            with self.assertRaisesRegex(ValueError, 'ORBITRA_CHANGELOG_WEBHOOK'):
                main()
            send.assert_not_called()
