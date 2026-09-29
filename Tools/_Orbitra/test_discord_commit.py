import json
import os
import unittest
from unittest.mock import patch

from discord_commit import main, payload


class DiscordCommitTest(unittest.TestCase):
    repository = 'Endennsss/orbitra-station'

    def commit(self, sha='a' * 40):
        return {
            'id': sha,
            'message': 'fix: тест — @everyone **сообщение**',
            'author': {'name': 'Автор', 'email': 'author@example.com'},
            'committer': {'name': 'Коммиттер', 'email': 'committer@example.com'},
            'added': ['new.yml'], 'modified': ['file.cs'], 'removed': ['old.ftl'],
        }

    def test_payload_has_commit_link_authors_and_files(self):
        message = payload(self.commit(), self.repository, 'pusher')
        embed = message['embeds'][0]
        self.assertEqual(embed['color'], 0x353535)
        self.assertIn('/commit/' + 'a' * 40, embed['url'])
        self.assertIn('Автор', embed['fields'][0]['value'])
        self.assertIn('pusher', str(embed['fields']))
        self.assertIn('new.yml', str(embed['fields']))
        self.assertEqual(message['allowed_mentions'], {'parse': []})

    def test_multiple_commits_are_published_separately(self):
        event = {'commits': [self.commit(), self.commit('b' * 40)], 'sender': {'login': 'pusher'}}
        with patch.dict(os.environ, {'GITHUB_EVENT_PATH': 'event.json', 'GITHUB_REPOSITORY': self.repository,
                                     'COMMIT_NOTIFY_DRY_RUN': 'true'}, clear=True), \
                patch('discord_commit.Path.read_text', return_value=json.dumps(event)), \
                patch('builtins.print') as output:
            main()
            data = json.loads(output.call_args.args[0])
            self.assertEqual(2, len(data))

    def test_text_is_sanitized_and_limited(self):
        commit = self.commit()
        commit['message'] = '@everyone **danger** ' + '😀' * 5000
        commit['modified'] = [f'file-{i}.cs' for i in range(100)]
        embed = payload(commit, self.repository)['embeds'][0]
        self.assertNotIn('@everyone', embed['description'])
        self.assertNotIn('**danger**', embed['description'])
        self.assertLessEqual(len(embed['description'].encode('utf-16-le')) // 2, 900)
        self.assertIn('ещё', str(embed['fields']))
        self.assertTrue(all(len(field['value'].encode('utf-16-le')) // 2 <= 1024 for field in embed['fields']))

    def test_invalid_sha_and_repository_are_rejected(self):
        with self.assertRaises(ValueError):
            payload(self.commit('bad'), self.repository)
        with self.assertRaises(ValueError):
            payload(self.commit(), 'invalid repository')

    def test_dry_run_does_not_send(self):
        event = {'commits': [self.commit()], 'sender': {'login': 'pusher'}}
        with patch.dict(os.environ, {'GITHUB_EVENT_PATH': 'event.json', 'GITHUB_REPOSITORY': self.repository,
                                     'COMMIT_NOTIFY_DRY_RUN': 'true'}, clear=True), \
                patch('discord_commit.Path.read_text', return_value=json.dumps(event)), \
                patch('discord_commit.send_payload') as send, patch('builtins.print'):
            main()
            send.assert_not_called()

    def test_missing_secret_reports_setup_error(self):
        event = {'commits': [self.commit()], 'sender': {'login': 'pusher'}}
        with patch.dict(os.environ, {'GITHUB_EVENT_PATH': 'event.json', 'GITHUB_REPOSITORY': self.repository}, clear=True), \
                patch('discord_commit.Path.read_text', return_value=json.dumps(event)):
            with self.assertRaisesRegex(ValueError, 'ORBITRA_CHANGELOG_WEBHOOK'):
                main()


if __name__ == '__main__':
    unittest.main()
