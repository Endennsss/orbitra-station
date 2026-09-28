import json
import unittest
from pathlib import Path

import yaml

from pr_policy import REQUIRED, validate


ROOT = Path(__file__).resolve().parents[2]


class PrPolicyTest(unittest.TestCase):
    def valid_pr(self):
        return {'title': 'fix: исправлено перекрытие блума',
                'body': '\n\n'.join('## ' + heading + '\nОписание результата.' for heading in REQUIRED)}

    def test_filled_pr_passes(self):
        self.assertEqual([], validate(self.valid_pr()))

    def test_empty_template_does_not_pass(self):
        pr = self.valid_pr()
        pr['body'] = (ROOT / '.github/PULL_REQUEST_TEMPLATE.md').read_text(encoding='utf-8')
        self.assertTrue(validate(pr))

    def test_comments_do_not_count_as_description(self):
        pr = self.valid_pr()
        pr['body'] = '## Что изменено\n<!-- Filled description -->'
        self.assertEqual(4, len(validate(pr)))

    def test_unknown_type_english_title_and_trailing_dot_fail(self):
        for title in ('Update bloom', 'fix: fix bloom', 'fix: исправлено.', 'misc: исправлено'):
            with self.subTest(title=title):
                self.assertTrue(validate(dict(self.valid_pr(), title=title)))

    def test_ruleset_requires_fresh_review_and_named_checks(self):
        config = json.loads((ROOT / '.github/orbitra/master_ruleset.json').read_text(encoding='utf-8'))
        rules = {rule['type']: rule.get('parameters', {}) for rule in config['rules']}
        self.assertTrue(rules['pull_request']['dismiss_stale_reviews_on_push'])
        self.assertTrue(rules['pull_request']['require_last_push_approval'])
        checks = {check['context'] for check in rules['required_status_checks']['required_status_checks']}
        self.assertTrue({'Orbitra Automation Tests', 'Orbitra PR Policy', 'Build & Test Debug', 'YAML Linter'} <= checks)

    def test_privileged_audit_never_checks_out_pr_head(self):
        workflow = yaml.load((ROOT / '.github/workflows/orbitra_pr_audit.yml').read_text(encoding='utf-8'), Loader=yaml.BaseLoader)
        self.assertEqual(['synchronize', 'closed'], workflow['on']['pull_request_target']['types'])
        checkout = workflow['jobs']['audit']['steps'][0]
        self.assertEqual('${{ github.event.pull_request.base.sha }}', checkout['with']['ref'])
        self.assertEqual('false', checkout['with']['persist-credentials'])
        self.assertEqual('read', workflow['permissions']['contents'])

    def test_required_automation_tests_are_not_path_filtered(self):
        workflow = yaml.load((ROOT / '.github/workflows/orbitra_automation_tests.yml').read_text(encoding='utf-8'), Loader=yaml.BaseLoader)
        self.assertNotIn('paths', workflow['on']['pull_request'])
        self.assertNotIn('if', workflow['jobs']['automation-tests'])
        self.assertEqual('read', workflow['permissions']['contents'])

    def test_required_build_gate_cannot_succeed_when_build_failed(self):
        names = []
        for path in ('build-test-debug.yml', 'build-map-renderer.yml'):
            workflow = yaml.load((ROOT / '.github/workflows' / path).read_text(encoding='utf-8'), Loader=yaml.BaseLoader)
            gate = workflow['jobs']['ci-success']
            self.assertEqual('always()', gate['if'])
            self.assertEqual('${{ needs.build.result }}', gate['steps'][0]['env']['BUILD_RESULT'])
            self.assertEqual('test "$BUILD_RESULT" = success', gate['steps'][0]['run'])
            names.append(gate['name'])
        self.assertEqual(len(names), len(set(names)))


if __name__ == '__main__':
    unittest.main()
