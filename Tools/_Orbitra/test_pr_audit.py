import unittest
from unittest.mock import Mock

import pr_audit


BASE = 'a' * 40
HEAD = 'b' * 40
REPOSITORY = 'Endennsss/orbitra-station'


def review(state='APPROVED', sha=BASE, review_id=1):
    return {'id': review_id, 'state': state, 'commit_id': sha, 'submitted_at': '2026-09-28T10:00:00Z',
            'user': {'login': 'reviewer'}}


def commit():
    return {'sha': HEAD, 'author': {'login': 'contributor'},
            'commit': {'author': {'name': 'Local Name'}, 'message': 'fix: исправлен блум\n\nDetails'}}


class PrAuditTest(unittest.TestCase):
    def test_dismissed_approval_is_recovered_from_timeline(self):
        events = [{'event': 'review_dismissed', 'dismissed_review': {'review_id': 1, 'state': 'approved'}}]
        self.assertEqual(1, pr_audit.latest_approval([review('DISMISSED')], events)['id'])

    def test_dismissed_changes_requested_is_not_approval(self):
        events = [{'event': 'review_dismissed', 'dismissed_review': {'review_id': 1, 'state': 'changes_requested'}}]
        self.assertIsNone(pr_audit.latest_approval([review('DISMISSED')], events))

    def test_latest_approval_uses_submission_order_not_api_order(self):
        newer = dict(review(review_id=2), submitted_at='2026-09-28T11:00:00Z')
        self.assertEqual(2, pr_audit.latest_approval([newer, review()], [])['id'])

    def test_audit_identifies_pusher_and_actual_commit_author_separately(self):
        comparison = {'status': 'ahead', 'commits': [commit()], 'total_commits': 1,
                      'files': [{'filename': 'file.cs', 'additions': 2, 'deletions': 1}]}
        body = pr_audit.audit_body(REPOSITORY, 7, HEAD, 'maintainer', review(), comparison)
        for text in ('maintainer', 'contributor', 'reviewer', BASE + '...' + HEAD, 'file.cs (+2/−1)'):
            self.assertIn(text, body)

    def test_force_push_with_missing_base_still_leaves_audit(self):
        body = pr_audit.audit_body(REPOSITORY, 7, HEAD, 'pusher', review(), None)
        self.assertIn('Сравнение недоступно', body)
        self.assertIn(BASE, body)

    def test_diverged_history_is_explicit(self):
        body = pr_audit.audit_body(REPOSITORY, 7, HEAD, 'pusher', review(), {'status': 'diverged'})
        self.assertIn('История разошлась', body)

    def test_unlinked_git_author_is_preserved(self):
        item = commit()
        item['author'] = None
        self.assertIn('Local Name', pr_audit.commit_lines([item])[0])

    def test_commit_content_cannot_mention_everyone_or_break_lines(self):
        item = commit()
        item['commit']['message'] = '@everyone **title**\nUnexpected body'
        text = pr_audit.commit_lines([item])[0]
        self.assertNotIn('@everyone', text)
        self.assertNotIn('Unexpected body', text)
        self.assertIn(r'\*\*title\*\*', text)

    def setup_run(self, comments=None, reviews=None):
        api = Mock()
        api.request.side_effect = [
            {'state': 'open', 'head': {'sha': HEAD}},
            {'status': 'ahead', 'commits': [commit()], 'total_commits': 1},
            {},
        ]
        api.pages.side_effect = [reviews if reviews is not None else [review()], [], comments or []]
        event = {'action': 'synchronize', 'after': HEAD, 'sender': {'login': 'pusher'}, 'pull_request': {'number': 7}}
        return api, event

    def test_new_audit_is_posted_once(self):
        api, event = self.setup_run()
        pr_audit.run(event, REPOSITORY, api)
        self.assertEqual('POST', api.request.call_args.args[1])
        self.assertIn('orbitra-review-audit:7:' + HEAD, api.request.call_args.args[2]['body'])

    def test_rerun_does_not_duplicate_bot_record(self):
        comments = [{'user': {'login': 'github-actions[bot]'}, 'body': f'<!-- orbitra-review-audit:7:{HEAD} -->'}]
        api, event = self.setup_run(comments)
        pr_audit.run(event, REPOSITORY, api)
        self.assertEqual(2, api.request.call_count)

    def test_user_cannot_suppress_audit_with_copied_marker(self):
        comments = [{'user': {'login': 'contributor'}, 'body': f'<!-- orbitra-review-audit:7:{HEAD} -->'}]
        api, event = self.setup_run(comments)
        pr_audit.run(event, REPOSITORY, api)
        self.assertEqual('POST', api.request.call_args.args[1])

    def test_push_without_approval_or_already_approved_head_is_quiet(self):
        for reviews in ([], [review(sha=HEAD)]):
            api, event = self.setup_run(reviews=reviews)
            pr_audit.run(event, REPOSITORY, api)
            self.assertEqual(1, api.request.call_count)

    def test_old_queued_event_does_not_post_as_current(self):
        api, event = self.setup_run()
        api.request.side_effect = [{'state': 'open', 'head': {'sha': 'c' * 40}}]
        pr_audit.run(event, REPOSITORY, api)
        api.pages.assert_not_called()

    def test_closed_unmerged_pr_is_quiet(self):
        api = Mock()
        pr_audit.run({'action': 'closed', 'pull_request': {'number': 7, 'merged': False}}, REPOSITORY, api)
        api.request.assert_not_called()

    def test_merge_summary_distinguishes_author_reviewer_and_merger(self):
        pr = {'number': 7, 'merge_commit_sha': HEAD, 'head': {'sha': BASE}, 'title': 'fix: исправлен блум',
              'user': {'login': 'author'}, 'merged_by': {'login': 'maintainer'}}
        body = pr_audit.merged_body(REPOSITORY, pr, [commit()], [review()])
        for name in ('author', 'maintainer', 'reviewer', 'contributor'):
            self.assertIn(name, body)

    def test_later_changes_requested_cancels_earlier_approval_in_summary(self):
        pr = {'number': 7, 'merge_commit_sha': HEAD, 'head': {'sha': BASE}, 'title': 'fix: исправлен блум',
              'user': {'login': 'author'}, 'merged_by': {'login': 'maintainer'}}
        body = pr_audit.merged_body(REPOSITORY, pr, [], [review(), review('CHANGES_REQUESTED', review_id=2)])
        self.assertIn('Нет approval на финальный SHA', body)

    def test_merged_event_posts_summary(self):
        pr = {'number': 7, 'merged': True, 'merge_commit_sha': HEAD, 'head': {'sha': BASE},
              'title': 'fix: исправлен блум', 'user': {'login': 'author'}, 'merged_by': {'login': 'maintainer'}}
        api = Mock()
        api.request.side_effect = [pr, {}]
        api.pages.side_effect = [[commit()], [review()], []]
        pr_audit.run({'action': 'closed', 'pull_request': pr}, REPOSITORY, api)
        self.assertIn('orbitra-merge-summary:7:', api.request.call_args.args[2]['body'])

    def test_pagination_reads_reviews_beyond_first_page(self):
        api = pr_audit.GitHub(REPOSITORY, 'unused-token')
        api.request = Mock(side_effect=[[review()] * 100, [review(review_id=101)]])
        self.assertEqual(101, len(api.pages('/pulls/7/reviews')))
        self.assertIn('page=2', api.request.call_args.args[0])


if __name__ == '__main__':
    unittest.main()
