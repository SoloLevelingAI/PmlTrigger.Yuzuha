"""Real process smoke tests; never send PML to a live AVEVA PID."""
import argparse
import json
import queue
import subprocess
import threading
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--cli", required=True, type=Path)
parser.add_argument("--mcp", required=True, type=Path)
args = parser.parse_args()

def command(path):
    return (["dotnet", str(path.resolve())] if path.suffix == ".dll" else [str(path.resolve())])

def cli(arguments, code):
    result = subprocess.run(command(args.cli) + arguments, capture_output=True, text=True,
                            encoding="utf-8", timeout=20)
    assert result.returncode == code, (arguments, result.returncode, result.stderr)
    data = json.loads(result.stdout)
    assert data["exitCode"] == code and data["success"] == (code == 0), data
    return data

assert len(cli(["op", "list"], 0)["data"]) == 6
description = cli(["op", "describe", "pml.command.execute"], 0)["data"]
assert description["requiresPid"] and description["requiresExecution"]
cli(["op", "describe", "not.found"], 3)
assert cli(["op", "invoke", "pml.call.generate", "--input", json.dumps({
    "methodName": "Example", "parameters": [{"type": "str", "value": "O'Brien 中文"}]}, ensure_ascii=False)], 0)["data"] == "!!Example('O''Brien 中文')"
cli(["op", "invoke", "pml.command.execute", "--input", '{"pmlCommand":"!!Example()"}'], 5)
cli(["op", "invoke", "pml.command.execute", "--pid", "2147483647", "--input", '{"pmlCommand":"!!Example()"}'], 4)
cli(["op", "invoke", "pml.command.execute", "--pid", "2147483647", "--allow-execution",
     "--input", '{"pmlCommand":"!!Example()"}'], 5)
cli(["op", "invoke", "aveva.session.status"], 5)  # Another CLI process cannot inherit selection.
cli(["op", "invoke", "pml.call.generate", "--input", "{bad}"], 2)
cli(["op", "invoke", "pml.call.generate", "--pid", "0"], 2)
print("PASS 10 real CLI process checks")

process = subprocess.Popen(command(args.mcp), stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                           stderr=subprocess.PIPE, text=True, encoding="utf-8", bufsize=1)
messages = queue.Queue()
stderr_lines = []
def drain_stdout():
    for line in process.stdout:
        messages.put(line)
    messages.put(None)
def drain_stderr():
    for line in process.stderr:
        stderr_lines.append(line)
threading.Thread(target=drain_stdout, daemon=True).start()
threading.Thread(target=drain_stderr, daemon=True).start()
request_id = 0
def rpc(method, params):
    global request_id
    request_id += 1
    process.stdin.write(json.dumps({"jsonrpc": "2.0", "id": request_id,
                                   "method": method, "params": params}) + "\n")
    process.stdin.flush()
    while True:
        line = messages.get(timeout=20)
        assert line is not None, "MCP exited: " + "".join(stderr_lines)
        message = json.loads(line)  # Any stray stdout log fails the test.
        if message.get("id") == request_id:
            assert "error" not in message, message
            return message["result"]
def tool(name, arguments):
    result = rpc("tools/call", {"name": name, "arguments": arguments})
    assert not result.get("isError"), result
    return "".join(c["text"] for c in result["content"] if c["type"] == "text")
try:
    rpc("initialize", {"protocolVersion": "2025-03-26", "capabilities": {},
                       "clientInfo": {"name": "phayton-regression", "version": "1"}})
    process.stdin.write('{"jsonrpc":"2.0","method":"notifications/initialized"}\n')
    process.stdin.flush()
    names = {t["name"] for t in rpc("tools/list", {})["tools"]}
    assert {"list_aveva_sessions", "select_aveva_session", "get_connection_status",
            "generate_pml_call", "run_pml_command", "run_pml_command_list",
            "list_pml_function_trust", "set_pml_function_trust", "get_builtin_usage",
            "op_list", "op_describe", "op_invoke"} <= names, names
    assert len(json.loads(tool("op_list", {}))) == 6
    assert tool("generate_pml_call", {"methodName": "Example", "parameters": []}) == "!!Example()"
    assert json.loads(tool("get_builtin_usage", {"query": "read-current-element"}))
    assert tool("run_pml_command", {"pmlCommand": "!!Example()"}).startswith("PML RPC failed:")
    result = json.loads(tool("op_invoke", {"operationId": "pml.command.execute", "processId": 2147483647,
                      "inputJson": '{"pmlCommand":"!!Example()"}'}))
    assert result["exitCode"] == 4, result
    result = json.loads(tool("op_invoke", {"operationId": "pml.call.generate", "inputJson": "{bad}"}))
    assert result["exitCode"] == 2
    print("PASS MCP stdio handshake, legacy tool names/output, shared registration and execution gate")
finally:
    process.stdin.close()
    try:
        process.wait(timeout=10)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=5)
