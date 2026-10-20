"""Verify published CLI/MCP executables without selecting or executing in AVEVA."""
import json
import os
from pathlib import Path
import queue
import subprocess
import sys
import tempfile
import threading


def check(runtime):
    with tempfile.TemporaryDirectory(prefix='yuzuha-cli-native-') as temporary:
        base = Path(temporary)
        env = dict(os.environ, YUZUHA_KNOWLEDGE_DIR=str(base / 'knowledge'),
                   YUZUHA_TRUST_FILE=str(base / 'trust.json'), YUZUHA_STATE_DIR=str(base / 'state'))

        def cli(executable, *args, expected=0):
            result = subprocess.run([str(executable), '--cli', *args], env=env,
                                    capture_output=True, encoding='utf-8', timeout=45)
            assert result.returncode == expected, (result.returncode, result.stdout, result.stderr)
            # json.loads rejects logging or protocol fragments mixed into stdout.
            return json.loads(result.stdout)

        def mcp_catalog(executable, arguments):
            process = subprocess.Popen([str(executable), *arguments], env=env,
                stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                text=True, encoding='utf-8')
            responses = queue.Queue()
            def read():
                for line in process.stdout:
                    try:
                        responses.put(json.loads(line))
                    except ValueError:
                        responses.put({'invalid_stdout': line})
            threading.Thread(target=read, daemon=True).start()
            def send(value):
                process.stdin.write(json.dumps(value) + '\n')
                process.stdin.flush()
            try:
                send(dict(jsonrpc='2.0', id=1, method='initialize', params=dict(
                    protocolVersion='2024-11-05', capabilities={},
                    clientInfo=dict(name='native-cli-smoke', version='1'))))
                assert 'result' in responses.get(timeout=20)
                send(dict(jsonrpc='2.0', method='notifications/initialized'))
                send(dict(jsonrpc='2.0', id=2, method='tools/list', params={}))
                while True:
                    result = responses.get(timeout=20)
                    assert 'invalid_stdout' not in result, result
                    if result.get('id') == 2:
                        return result['result']['tools']
            finally:
                process.stdin.close()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()

        catalogs = {}
        for name in ('YuzuhaToolkit.Mcp', 'YuzuhaToolkit.Knowledge'):
            executable = runtime / (name + '.exe')
            tools = cli(executable, 'list')['tools']
            implicit = mcp_catalog(executable, [])
            explicit = mcp_catalog(executable, ['--mcp'])
            assert tools == implicit == explicit, name
            catalogs[name] = len(tools)

        executable = runtime / 'YuzuhaToolkit.Mcp.exe'
        assert all(name in [tool['name'] for tool in mcp_catalog(executable, ['--mcp'])]
                   for name in ('markdb', 'endmarkdb', 'inspect_registered_pmllib', 'get_ai_runtime_status', 'get_recent_tool_calls'))
        result = cli(executable, 'call', 'markdb', '--arguments', '{}', expected=1)
        assert 'TARGET_NOT_SELECTED' in result['content'][0]['text'], result
        result = cli(executable, 'call', 'get_recent_tool_calls', '--arguments', '{"limit":50}')
        entries = json.loads(result['content'][0]['text'])
        assert any(entry['tool'] == 'markdb' and entry['phase'] == 'failed' for entry in entries), entries
        argument_file = base / 'arguments.json'
        argument_file.write_text(json.dumps(dict(methodName='Test', parameters=[])), encoding='utf-8')
        result = cli(executable, 'call', 'generate_pml_call', '--file', str(argument_file))
        assert 'Test' in result['content'][0]['text'], result
        batch = base / 'requests.json'
        batch.write_text(json.dumps([
            dict(name='generate_pml_call', arguments=dict(methodName='Test', parameters=[])),
            dict(name='nonexistent_smoke_tool', arguments={}),
            dict(name='get_builtin_usage', arguments={})]), encoding='utf-8')
        result = cli(executable, 'batch', '--file', str(batch), expected=1)
        assert result['stoppedOnError'] and result['completed'] == 2, result
        assert result['results'][1]['result']['isError'], result
        result = cli(executable, 'call', 'get_builtin_usage', '--arguments', '[]', expected=2)
        assert result['code'] == 'invalid_arguments', result
        argument_file.write_text(json.dumps(dict(query='当前元素')), encoding='utf-8')
        result = cli(runtime / 'YuzuhaToolkit.Knowledge.exe', 'call', 'search_knowledge_layers', '--file', str(argument_file))
        assert 'read-current-element' in result['content'][0]['text'], result
        assert not (base / 'knowledge').exists(), 'Built-in lookup created a database'
        print(json.dumps(dict(passed=True, catalogs=catalogs, nativeCli=True,
            defaultAndExplicitMcp=True, identicalSchemas=True, batchStopsOnError=True,
            cleanStdout=True, builtinsWithoutDatabase=True), ensure_ascii=False))


if __name__ == '__main__':
    check(Path(sys.argv[1]).resolve())
