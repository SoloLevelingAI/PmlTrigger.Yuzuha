"""Test authoring validation and truthful failed-run reporting, without AVEVA."""
import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import subprocess

ROOT = Path(__file__).resolve().parents[1]
TEMP_ROOT = ROOT / 'artifacts/scenario-parser-tests'
TEMP_ROOT.mkdir(parents=True, exist_ok=True)
tempfile.tempdir = str(TEMP_ROOT)
SPEC = importlib.util.spec_from_file_location('scenario', ROOT / 'scripts/run-workbench-scenario.py')
runner = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(runner)


class ScenarioAuthoringTests(unittest.TestCase):
    def test_json_and_toml_define_exactly_the_same_workflow(self):
        left, _ = runner.read_scenario(ROOT / 'tests/scenarios/launch-lifecycle.toml')
        right, _ = runner.read_scenario(ROOT / 'tests/scenarios/launch-lifecycle.json')
        self.assertEqual(left, right)
        self.assertEqual(15, len(left['steps']))

    def test_duplicate_json_key_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / 'case.json'
            p.write_text('{"schemaVersion":1,"schemaVersion":1}', encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'Duplicate JSON key'):
                runner.read_scenario(p)

    def test_toml_datetime_cannot_silently_become_a_string(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / 'case.toml'
            p.write_text('date = 2026-10-24T00:00:00Z', encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'Not a JSON-compatible value'):
                runner.read_scenario(p)

    def test_forward_binding_and_implicit_permission_are_rejected(self):
        baseline, _ = runner.read_scenario(ROOT / 'tests/scenarios/launch-lifecycle.json')
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / 'case.json'
            for change in ('forward', 'permission', 'empty', 'unknown'):
                scenario = copy.deepcopy(baseline)
                if change == 'forward':
                    scenario['steps'][0]['bindings'] = {'revision': {'step': 'save', 'pointer': '/data/revision'}}
                elif change == 'permission':
                    scenario['steps'][0]['permissions'] = ['execution']
                elif change == 'empty':
                    scenario['steps'] = []
                else:
                    scenario['typoField'] = True
                p.write_text(json.dumps(scenario), encoding='utf-8')
                with self.subTest(change=change), self.assertRaises(ValueError):
                    runner.read_scenario(p)

    def test_missing_pointer_and_boolean_integer_mismatch_do_not_pass(self):
        with self.assertRaises(KeyError):
            runner.select({'data': {}}, '/data/missing')
        self.assertFalse(runner.check_assertion({'ok': True}, {'pointer': '/ok', 'equals': 1})['passed'])
        self.assertEqual('value', runner.select({'a/b': {'c~d': 'value'}}, '/a~1b/c~0d'))

    def test_bad_scenario_still_saves_failed_receipt_and_html(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp)
            source = p / 'bad.json'
            source.write_text('{"bad":true}', encoding='utf-8')
            report = runner.run_scenario(source, p / 'nonexistent-cli.exe', p / 'report')
            self.assertEqual('Failed', report['status'])
            self.assertEqual(0, report['executed'])
            self.assertFalse(report['avevaExecuted'])
            self.assertEqual('Failed', json.loads((p / 'report/run.json').read_text(encoding='utf-8'))['status'])
            self.assertIn('Failed', (p / 'report/report.html').read_text(encoding='utf-8'))

    def test_interrupt_marks_active_step_and_stops_before_next_process(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            executable = root / 'fixture.exe'
            executable.write_bytes(b'not executed: subprocess is mocked')
            with patch.object(runner, 'source_identity', return_value={'fixture': True}), \
                    patch.object(runner.subprocess, 'run', side_effect=KeyboardInterrupt) as process:
                report = runner.run_scenario(ROOT / 'tests/scenarios/launch-lifecycle.json', executable, root / 'report')
            self.assertEqual(1, process.call_count)
            self.assertEqual('Interrupted', report['status'])
            self.assertEqual('Interrupted', report['steps'][0]['status'])
            self.assertEqual(1, report['executed'])
            self.assertEqual('KeyboardInterrupt', report['steps'][0]['error'])
            saved = json.loads((root / 'report/run.json').read_text(encoding='utf-8'))
            self.assertEqual(report, saved)
            self.assertIn('Interrupted', (root / 'report/report.html').read_text(encoding='utf-8'))

    def test_timeout_retains_partial_output_and_stops_before_next_process(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            executable = root / 'fixture.exe'
            executable.write_bytes(b'not executed: subprocess is mocked')
            timeout = subprocess.TimeoutExpired(['fixture.exe'], 0.01, output=b'{"partial":', stderr=b'pending write\n')
            with patch.object(runner, 'source_identity', return_value={'fixture': True}), \
                    patch.object(runner.subprocess, 'run', side_effect=timeout) as process:
                report = runner.run_scenario(ROOT / 'tests/scenarios/launch-lifecycle.json', executable, root / 'report', timeout=0.01)
            self.assertEqual(1, process.call_count)
            self.assertEqual('Failed', report['status'])
            step = report['steps'][0]
            self.assertEqual('Failed', step['status'])
            self.assertTrue(step['timedOut'])
            self.assertEqual(0.01, step['timeoutSeconds'])
            self.assertEqual(b'{"partial":', (root / ('report/' + step['id'] + '.stdout.json')).read_bytes())
            self.assertEqual(b'pending write\n', (root / ('report/' + step['id'] + '.stderr.log')).read_bytes())
            self.assertEqual(report, json.loads((root / 'report/run.json').read_text(encoding='utf-8')))
            self.assertTrue((root / 'report/report.html').is_file())


if __name__ == '__main__':
    unittest.main()
