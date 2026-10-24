"""Native CLI process smoke. Uses isolated state and never executes PML in AVEVA."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
from datetime import datetime, timezone


def check(executable, report=None, legacy=None):
    executable = Path(executable).resolve()
    records = []
    command = ["dotnet", str(executable)] if executable.suffix == ".dll" else [str(executable)]
    with tempfile.TemporaryDirectory(prefix="phayton-native-cli-") as temporary:
        root = Path(temporary)
        env = dict(os.environ, YUZUHA_STATE_DIR=str(root / "state"),
                   YUZUHA_KNOWLEDGE_DIR=str(root / "knowledge"),
                   YUZUHA_TRUST_FILE=str(root / "trust.json"),
                   YUZUHA_FUNCTION_REGISTRY=str(root / "functions.json"))

        def invoke(arguments, expected=0, stdin=None):
            result = subprocess.run(command + arguments, input=stdin, env=env, capture_output=True,
                                    text=True, encoding="utf-8", timeout=45)
            data = json.loads(result.stdout)
            records.append(dict(arguments=arguments, expectedExitCode=expected,
                                exitCode=result.returncode, result=data))
            assert result.returncode == expected, (arguments, result.returncode, result.stdout, result.stderr)
            assert data["exitCode"] == expected and data["success"] == (expected == 0), data
            assert "Application started." not in result.stderr
            return data

        try:
            operations = invoke(["op", "list"])["data"]
            assert len(operations) >= 46
            aliases = {name for op in operations for name in op.get("legacyTools") or []}
            assert len(aliases) == 30, aliases
            for operation in operations:
                described = invoke(["op", "describe", operation["id"]])["data"]
                assert described == operation
            execute = next(op for op in operations if op["id"] == "pml.command.execute")
            assert execute["requiresPid"] and execute["requiresExecution"]
            invoke(["op", "describe", "not.found"], 3)
            payload = json.dumps({"methodName": "Example", "parameters": [{"type": "str", "value": "O'Brien 中文"}]}, ensure_ascii=False)
            input_file = root / "input.json"
            input_file.write_text(payload, encoding="utf-8")
            first = invoke(["op", "invoke", "pml.call.generate", "--input", payload])
            second = invoke(["op", "invoke", "pml.call.generate", "--input-file", str(input_file)])
            third = invoke(["op", "invoke", "pml.call.generate", "--input-stdin"], stdin=payload)
            assert first == second == third
            assert first["data"] == "!!Example('O''Brien 中文')"
            invoke(["op", "invoke", "pml.command.execute", "--input", '{"pmlCommand":"!!Example()"}'], 5)
            invoke(["op", "invoke", "pml.command.execute", "--pid", "2147483647", "--input", '{"pmlCommand":"!!Example()"}'], 4)
            invoke(["op", "invoke", "pml.command.execute", "--pid", "2147483647", "--allow-execution", "--input", '{"pmlCommand":"!!Example()"}'], 5)
            invoke(["op", "invoke", "aveva.session.status"], 5)
            invoke(["op", "invoke", "pml.call.generate", "--input", "{bad}"], 2)
            invoke(["op", "invoke", "pml.call.generate", "--pid", "0"], 2)
            invoke(["op", "invoke", "builtin.usage", "--timeout-seconds", "0"], 2)
            assert invoke(["op", "invoke", "builtin.usage", "--input", '{"query":"current element"}'])["data"]
            assert invoke(["op", "invoke", "knowledge.layers.search", "--input", '{"query":"当前元素"}'])["data"][0]["Role"] == "builtin"
            assert not (root / "knowledge").exists()
            invoke(["op", "invoke", "pml.trust.set", "--input", '{"functionName":"Example","state":"untrusted"}'], 4)
            assert not (root / "trust.json").exists()
            batch = [
                {"operationId": "pml.call.generate", "input": {"methodName": "Example"}},
                {"operationId": "does.not.exist", "input": {}},
                {"operationId": "pml.trust.set", "input": {"functionName": "Example", "state": "untrusted"}}
            ]
            outcome = invoke(["op", "batch", "--input", json.dumps(batch), "--allow-write"], 3)
            assert outcome["data"]["completed"] == 2 and outcome["data"]["stoppedOnError"]
            assert outcome["data"]["results"][0]["data"] == "!!Example()"
            assert not (root / "trust.json").exists()
            assert invoke(["--mcp"], 3)["error"]["code"] == "MCP_DISABLED"
            if legacy:
                for arguments in ([], ["--mcp"]):
                    old = subprocess.run([str(Path(legacy).resolve()), *arguments], env=env, capture_output=True,
                                         text=True, encoding="utf-8", timeout=20)
                    assert old.returncode == 3 and json.loads(old.stdout)["error"]["code"] == "MCP_DISABLED"
            passed = True
        except Exception:
            passed = False
            raise
        finally:
            receipt = dict(schemaVersion=1, passed=locals().get("passed", False),
                           executable=str(executable), sha256=hashlib.sha256(executable.read_bytes()).hexdigest(),
                           finishedUtc=datetime.now(timezone.utc).isoformat(),
                           scope="native-cli-real-processes-isolated-state-no-live-AVEVA",
                           executed=len(records), operations=locals().get("operations", []), cases=records)
            if report:
                target = Path(report).resolve()
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text(json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"PASS {len(records)} native CLI process checks; no MCP transport; no live AVEVA execution.")
    return receipt


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--cli", required=True, type=Path)
    parser.add_argument("--mcp", type=Path, help="Optional legacy filename; verify disabled transport only.")
    parser.add_argument("--report", type=Path)
    options = parser.parse_args()
    check(options.cli, options.report, options.mcp)
