"""Run JSON/TOML workbench scenarios through the real CLI in isolated files.

Python 3.11+ is a development dependency only. Every step uses a new CLI process;
there is no alternate implementation of application behavior in this runner.
"""
from __future__ import annotations

import argparse
import copy
import datetime as dt
import hashlib
import html
import json
import math
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import tomllib


def utc():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def digest(data):
    return hashlib.sha256(data).hexdigest()


def strict_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON key: {key}")
        result[key] = value
    return result


def json_value(value):
    if value is None or type(value) in (str, bool, int):
        return
    if type(value) is float and math.isfinite(value):
        return
    if isinstance(value, list):
        for item in value:
            json_value(item)
        return
    if isinstance(value, dict) and all(isinstance(key, str) for key in value):
        for item in value.values():
            json_value(item)
        return
    raise ValueError(f"Not a JSON-compatible value: {type(value).__name__}")


def fields(value, allowed, required):
    if not isinstance(value, dict) or not required <= value.keys() or value.keys() - allowed:
        raise ValueError(f"Expected fields {sorted(required)}, allowed {sorted(allowed)}")


def read_scenario(path):
    raw = path.read_bytes()
    if len(raw) > 1_048_576:
        raise ValueError("Scenario exceeds 1 MiB")
    if path.suffix.lower() == '.toml':
        value = tomllib.loads(raw.decode('utf-8-sig'))
    elif path.suffix.lower() == '.json':
        value = json.loads(raw.decode('utf-8-sig'), object_pairs_hook=strict_object)
    else:
        raise ValueError("Use .json or .toml")
    json_value(value)
    fields(value, {'schemaVersion', 'caseId', 'description', 'environment', 'fixture', 'steps'},
           {'schemaVersion', 'caseId', 'environment', 'fixture', 'steps'})
    if type(value['schemaVersion']) is not int or value['schemaVersion'] != 1:
        raise ValueError("Unsupported scenario version")
    if not isinstance(value['caseId'], str) or not re.fullmatch(r'[a-z][a-z0-9.-]+', value['caseId']):
        raise ValueError("Invalid caseId")
    if value['environment'] != 'isolated-files' or value['fixture'] != 'workspace.launch-pair':
        raise ValueError("This runner supports isolated-files / workspace.launch-pair only")
    if not isinstance(value['steps'], list) or not 1 <= len(value['steps']) <= 100:
        raise ValueError("Require 1 to 100 steps")
    seen = set()
    for step in value['steps']:
        fields(step, {'id', 'operation', 'input', 'bindings', 'assertions', 'expectExitCode', 'permissions', 'transport'},
               {'id', 'operation', 'input', 'assertions'})
        if not isinstance(step['id'], str) or not re.fullmatch(r'[a-z][a-z0-9-]*', step['id']) or step['id'] in seen:
            raise ValueError("Invalid or duplicate step ID")
        if not isinstance(step['operation'], str) or not re.fullmatch(r'launch\.(profile\.(read|list|validate|preview|save|update|history|restore|restore\.preview|delete|duplicate|favorite|default)|modules\.list)', step['operation']):
            raise ValueError("Isolated fixture only allows launch configuration operations")
        if not isinstance(step['input'], dict):
            raise ValueError("Input must be an object")
        if step.get('transport', 'file') not in ('file', 'stdin', 'inline'):
            raise ValueError("Unknown input transport")
        if type(step.get('expectExitCode', 0)) is not int:
            raise ValueError("expectExitCode must be an integer")
        if step.get('permissions', []) not in ([], ['write']):
            raise ValueError("Only isolated configuration write permission is allowed")
        for key, binding in step.get('bindings', {}).items():
            fields(binding, {'step', 'pointer'}, {'step', 'pointer'})
            if binding['step'] not in seen or key in step['input']:
                raise ValueError("Binding must reference an earlier step and cannot replace an input")
            validate_pointer(binding['pointer'])
        if not isinstance(step['assertions'], list) or not step['assertions']:
            raise ValueError("Each step needs at least one assertion")
        for assertion in step['assertions']:
            fields(assertion, {'pointer', 'equals', 'notEquals', 'length', 'contains'}, {'pointer'})
            if len(assertion) != 2:
                raise ValueError("An assertion needs one comparator")
            validate_pointer(assertion['pointer'])
        seen.add(step['id'])
    return value, raw


def validate_pointer(pointer):
    if not isinstance(pointer, str) or (pointer and not pointer.startswith('/')) or re.search(r'~(?![01])', pointer):
        raise ValueError("Invalid JSON Pointer")


def select(value, pointer):
    validate_pointer(pointer)
    if not pointer:
        return value
    for part in pointer[1:].split('/'):
        key = part.replace('~1', '/').replace('~0', '~')
        if isinstance(value, list):
            if not re.fullmatch(r'0|[1-9][0-9]*', key):
                raise ValueError("Invalid array index")
            value = value[int(key)]
        elif isinstance(value, dict):
            value = value[key]
        else:
            raise ValueError("Pointer traverses a scalar")
    return value


def save_json(path, value):
    pending = path.with_suffix(path.suffix + '.tmp')
    pending.write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False), encoding='utf-8')
    pending.replace(path)


def create_fixture(root):
    state = root / 'state'
    state.mkdir(parents=True)
    profiles = []
    for suffix, product, version, module, kind, executable in (
        ('e3d', 'E3D', '2.1', 'Design', 'Monitor', 'mon.exe'),
        ('am', 'AM', '12.1', 'Outfitting', 'MarineBatch', 'marine.bat')):
        install = root / suffix
        install.mkdir()
        (install / executable).write_text('', encoding='utf-8')
        (install / 'launch.init').write_text('', encoding='utf-8')
        profiles.append({'id': 'sample-' + suffix, 'name': product + ' scenario', 'product': product,
                         'version': version, 'kind': kind, 'executable': str(install / executable),
                         'workingDirectory': str(install), 'initialization': str(install / 'launch.init'),
                         'project': 'TST', 'user': 'SYSTEM', 'mdb': '/ALL', 'module': module,
                         'rememberPassword': False, 'credentialKey': '', 'environment': {},
                         'vendorField': {'retained': 'profile-' + suffix}})
    settings = {'pluginName': 'KeepPlugin', 'warnFiles': 3000, 'warnBytes': 209715200,
                'profiles': profiles, 'vendorExtension': {'keep': [1, 2, 3]}}
    save_json(state / 'workspace.json', settings)
    return state


def check_assertion(value, spec):
    actual = select(value, spec['pointer'])
    if 'equals' in spec:
        # JSON true and 1 are distinct even though Python considers them equal.
        passed = json.dumps(actual, sort_keys=True, ensure_ascii=False) == json.dumps(spec['equals'], sort_keys=True, ensure_ascii=False)
    elif 'notEquals' in spec:
        passed = json.dumps(actual, sort_keys=True, ensure_ascii=False) != json.dumps(spec['notEquals'], sort_keys=True, ensure_ascii=False)
    elif 'length' in spec:
        passed = isinstance(actual, (list, dict, str)) and len(actual) == spec['length']
    else:
        passed = isinstance(actual, (list, dict, str)) and spec['contains'] in actual
    return {'assertion': spec, 'actual': actual, 'passed': passed}


def source_identity(root):
    def git(*args):
        result = subprocess.run(['git', *args], cwd=root, capture_output=True, check=True)
        return result.stdout
    return {'commit': git('rev-parse', 'HEAD').decode().strip(),
            'dirty': bool(git('status', '--porcelain')),
            'trackedDiffSha256': digest(git('diff', 'HEAD', '--', 'src', 'scripts', 'tests'))}


def html_report(path, report):
    e = lambda value: html.escape(str(value))
    rows = ''.join('<tr><td>' + e(step['id']) + '</td><td><code>' + e(step['operation']) +
                   '</code></td><td>' + e(step['status']) + '</td><td>' +
                   e(step.get('error', '')) + '</td></tr>' for step in report['steps'])
    page = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>Phayton 场景测试报告</title>
<style>body{max-width:1100px;margin:40px auto;padding:0 24px;font:16px/1.65 system-ui;background:#f6f7fa;color:#18212f}h1{font-size:28px}table{width:100%;border-collapse:collapse;background:white}th,td{text-align:left;padding:12px;border-bottom:1px solid #ddd}pre{overflow:auto;background:#fff;padding:20px}a{color:#245ab0}</style>'''
    page += '<h1>' + e(report.get('caseId', '场景解析失败')) + '</h1><p><b>' + e(report['status']) + '</b> · 隔离文件 / 每步独立 CLI 进程</p>'
    page += '<p>这份报告验证真实应用服务和文件存储，不代表 AVEVA 实机启动通过。</p>'
    page += '<table><thead><tr><th>步骤</th><th>操作</th><th>状态</th><th>错误</th></tr></thead><tbody>' + rows + '</tbody></table>'
    page += '<p><a href="run.json">完整回执 JSON</a> · <a href="normalized.json">规范场景 JSON</a></p>'
    page += '<pre>' + e(json.dumps(report, ensure_ascii=False, indent=2)) + '</pre></html>'
    path.write_text(page, encoding='utf-8')


def run_scenario(source, executable, destination, timeout=30):
    destination.mkdir(parents=True, exist_ok=False)
    report = {'schemaVersion': 1, 'runnerVersion': 1, 'status': 'Running', 'startedUtc': utc(),
              'environment': 'isolated-files', 'avevaExecuted': False, 'steps': []}
    save_json(destination / 'run.json', report)
    try:
        scenario, raw = read_scenario(source)
        report['caseId'] = scenario['caseId']
        report['sourceSha256'] = digest(raw)
        report['source'] = source.name
        report['cliSha256'] = digest(executable.read_bytes())
        report['code'] = source_identity(Path(__file__).resolve().parents[1])
        (destination / ('source' + source.suffix)).write_bytes(raw)
        save_json(destination / 'normalized.json', scenario)
        state = create_fixture(destination / 'fixture')
        environment = dict(os.environ)
        environment.pop('YUZUHA_TOOLKIT_ROOT', None)
        environment.update(YUZUHA_STATE_DIR=str(state), YUZUHA_KNOWLEDGE_DIR=str(destination / 'fixture/knowledge'))
        outputs = {}
        prefix = ['dotnet', str(executable)] if executable.suffix.lower() == '.dll' else [str(executable)]
        for step in scenario['steps']:
            item = {'id': step['id'], 'operation': step['operation'], 'status': 'Running', 'startedUtc': utc()}
            report['steps'].append(item)
            save_json(destination / 'run.json', report)
            started = time.monotonic()
            try:
                arguments = copy.deepcopy(step['input'])
                for key, binding in step.get('bindings', {}).items():
                    arguments[key] = copy.deepcopy(select(outputs[binding['step']], binding['pointer']))
                item['input'] = arguments
                input_path = destination / (step['id'] + '.input.json')
                save_json(input_path, arguments)
                payload = json.dumps(arguments, ensure_ascii=False, allow_nan=False)
                command = prefix + ['op', 'invoke', step['operation']]
                transport = step.get('transport', 'file')
                if transport == 'file':
                    command += ['--input-file', str(input_path)]
                elif transport == 'stdin':
                    command += ['--input-stdin']
                else:
                    command += ['--input', payload]
                if step.get('permissions') == ['write']:
                    command += ['--allow-write']
                item['transport'] = transport
                process = subprocess.run(command, input=payload.encode('utf-8') if transport == 'stdin' else None,
                                         capture_output=True, env=environment, timeout=timeout)
                (destination / (step['id'] + '.stdout.json')).write_bytes(process.stdout)
                (destination / (step['id'] + '.stderr.log')).write_bytes(process.stderr)
                result = json.loads(process.stdout.decode('utf-8-sig'), object_pairs_hook=strict_object)
                item.update(exitCode=process.returncode, output=result)
                outputs[step['id']] = result
                if process.returncode != step.get('expectExitCode', 0) or process.returncode != result.get('exitCode'):
                    raise AssertionError(f"Unexpected exit code: process={process.returncode}, envelope={result.get('exitCode')}")
                if result.get('operationId') != step['operation']:
                    raise AssertionError('Operation identity mismatch')
                item['assertions'] = [check_assertion(result, check) for check in step['assertions']]
                if not all(check['passed'] for check in item['assertions']):
                    raise AssertionError('One or more assertions failed')
                item['status'] = 'Passed'
            except BaseException as error:
                item['status'] = 'Interrupted' if isinstance(error, KeyboardInterrupt) else 'Failed'
                item['error'] = str(error) or type(error).__name__
                if isinstance(error, subprocess.TimeoutExpired):
                    # subprocess.run has terminated and waited for the child here.
                    # Keep its partial output even when it is not valid JSON yet.
                    (destination / (step['id'] + '.stdout.json')).write_bytes(error.stdout or b'')
                    (destination / (step['id'] + '.stderr.log')).write_bytes(error.stderr or b'')
                    item['timedOut'] = True
                    item['timeoutSeconds'] = timeout
                raise
            finally:
                item['durationMs'] = round((time.monotonic() - started) * 1000)
                item['finishedUtc'] = utc()
                save_json(destination / 'run.json', report)
        settings = json.loads((state / 'workspace.json').read_text(encoding='utf-8-sig'))
        if settings.get('vendorExtension') != {'keep': [1, 2, 3]} or settings.get('pluginName') != 'KeepPlugin':
            raise AssertionError('Unrelated settings were not preserved')
        for profile in settings['profiles']:
            if profile['id'] in ('sample-e3d', 'sample-am'):
                if profile.get('vendorField') != {'retained': 'profile-' + profile['id'].split('-')[1]}:
                    raise AssertionError('Profile extension field was not preserved')
        if digest(executable.read_bytes()) != report['cliSha256']:
            raise AssertionError('CLI changed during scenario execution')
        report.update(status='Passed', unrelatedSettingsPreserved=True)
    except BaseException as error:
        report['status'] = 'Interrupted' if isinstance(error, KeyboardInterrupt) else 'Failed'
        report['error'] = str(error) or type(error).__name__
    finally:
        report['finishedUtc'] = utc()
        report['executed'] = len(report['steps'])
        save_json(destination / 'run.json', report)
        html_report(destination / 'report.html', report)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--scenario', required=True, type=Path)
    parser.add_argument('--cli', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    result = run_scenario(args.scenario.resolve(), args.cli.resolve(), args.output.resolve())
    print(json.dumps({'caseId': result.get('caseId'), 'status': result['status'], 'executed': result['executed'],
                      'error': result.get('error'), 'report': str(args.output.resolve() / 'run.json')}, ensure_ascii=False))
    return 0 if result['status'] == 'Passed' and result['executed'] > 0 else 1


if __name__ == '__main__':
    raise SystemExit(main())
