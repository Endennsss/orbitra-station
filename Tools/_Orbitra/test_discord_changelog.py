import copy
import os
import unittest
from unittest.mock import Mock, patch

import requests
import yaml

import discord_changelog as publisher


class DiscordChangelogTest(unittest.TestCase):
    def setUp(self):
        self.entry = {'id': 56, 'author': 'Endennsss', 'changes': [
            {'type': 'Add', 'message': 'Латунная стена.'},
            {'type': 'Fix', 'message': 'Исправлено обращение.'},
        ]}

    def document(self, entries, **extra):
        return yaml.safe_dump({'Name': 'OrbitraUpdates', 'AdminOnly': False, 'Entries': entries, **extra})

    def test_only_new_ids_oldest_first(self):
        old = dict(self.entry, id=54)
        new = dict(self.entry, id=55)
        self.assertEqual([55, 56], [entry['id'] for entry in publisher.select_entries([self.entry, new, old], [old])])

    def test_edit_does_not_resend(self):
        changed = dict(self.entry, author='Other')
        self.assertEqual([], publisher.select_entries([changed], [self.entry]))

    def test_reject_wrong_or_private_changelog(self):
        for extra in ({'Name': 'Changelog'}, {'AdminOnly': True}):
            with self.subTest(extra=extra), self.assertRaises(ValueError):
                publisher.load_entries(self.document([self.entry], **extra))

    def test_reject_duplicate_ids(self):
        with self.assertRaises(ValueError):
            publisher.load_entries(self.document([self.entry, self.entry]))

    def test_reject_empty_or_unknown_changes(self):
        for changes in ([], [{'type': 'Unknown', 'message': 'text'}], [{'type': 'Fix', 'message': ''}]):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                publisher.load_entries(self.document([dict(self.entry, changes=changes)]))

    def test_russian_categories_and_author(self):
        messages = publisher.payloads(publisher.load_entries(self.document([self.entry])))
        embed = messages[0]['embeds'][0]
        self.assertIn('**Добавлено**', embed['description'])
        self.assertIn('**Исправлено**', embed['description'])
        self.assertEqual('Автор: Endennsss', embed['footer']['text'])
        self.assertEqual({'parse': []}, messages[0]['allowed_mentions'])

    def test_long_unicode_preserved_and_within_limits(self):
        entry = copy.deepcopy(self.entry)
        entry['changes'] = [{'type': 'Add', 'message': '😀' * 9000}]
        messages = publisher.payloads([entry])
        self.assertEqual(9000, sum(message['embeds'][0]['description'].count('😀') for message in messages))
        for message in messages:
            self.assertLessEqual(len(message['embeds'][0]['description'].encode('utf-16-le')) // 2, 4096)

    def test_no_mentions_or_markdown_injection(self):
        result = publisher.plain_text('@everyone **test** <@123>')
        self.assertNotIn('@everyone', result)
        self.assertIn(r'\*\*test\*\*', result)

    def test_webhook_url(self):
        url = publisher.webhook_url('https://discord.com/api/webhooks/123/token?thread_id=456')
        self.assertIn('wait=true', url)
        self.assertIn('thread_id=456', url)
        for invalid in ('https://example.com/api/webhooks/123/token', 'https://discord.com/api/webhooks/123/token/github', ''):
            with self.subTest(url=invalid), self.assertRaises(ValueError):
                publisher.webhook_url(invalid)

    @patch.object(publisher.time, 'sleep')
    @patch.object(publisher.requests, 'post')
    def test_rate_limit_then_success(self, post, sleep):
        post.side_effect = [Mock(status_code=429, json=lambda: {'retry_after': 0.5}), Mock(status_code=200)]
        publisher.send_payload('secret', {})
        self.assertEqual(2, post.call_count)
        sleep.assert_called_once_with(0.75)

    @patch.object(publisher.requests, 'post')
    def test_network_error_does_not_leak_secret_or_retry(self, post):
        post.side_effect = requests.ConnectionError('secret-webhook-token')
        with self.assertRaises(RuntimeError) as failure:
            publisher.send_payload('secret', {})
        self.assertNotIn('secret', str(failure.exception))
        self.assertEqual(1, post.call_count)

    @patch.object(publisher.requests, 'post', return_value=Mock(status_code=400))
    def test_bad_request_fails_without_retry(self, post):
        with self.assertRaisesRegex(RuntimeError, 'HTTP 400'):
            publisher.send_payload('secret', {})
        self.assertEqual(1, post.call_count)

    @patch.dict(os.environ, {'CHANGELOG_ENTRY_ID': '56', 'CHANGELOG_DRY_RUN': 'true'}, clear=True)
    @patch.object(publisher, 'send_payload')
    @patch.object(publisher.Path, 'read_text')
    @patch('builtins.print')
    def test_manual_preview_never_sends(self, output, read, send):
        read.return_value = self.document([self.entry])
        publisher.main()
        send.assert_not_called()
        output.assert_called_once()

    @patch.dict(os.environ, {'CHANGELOG_BEFORE': 'a' * 40, 'DISCORD_WEBHOOK_URL': 'https://discord.com/api/webhooks/123/token'}, clear=True)
    @patch.object(publisher, 'send_payload')
    @patch.object(publisher.subprocess, 'run')
    @patch.object(publisher.Path, 'read_text')
    def test_push_reads_previous_revision(self, read, git, send):
        read.return_value = self.document([self.entry, dict(self.entry, id=55)])
        git.return_value = Mock(stdout=self.document([dict(self.entry, id=55)]))
        publisher.main()
        self.assertEqual(1, send.call_count)
        self.assertIn('#56', send.call_args.args[1]['embeds'][0]['title'])

    @patch.dict(os.environ, {'CHANGELOG_BEFORE': '0' * 40}, clear=True)
    @patch.object(publisher.Path, 'read_text')
    def test_first_push_cannot_publish_archive(self, read):
        read.return_value = self.document([self.entry])
        with self.assertRaises(ValueError):
            publisher.main()


if __name__ == '__main__':
    unittest.main()
