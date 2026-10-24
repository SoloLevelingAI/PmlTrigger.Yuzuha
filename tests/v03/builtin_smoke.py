"""Verify native CLI embedded guides and isolated reference stores; no MCP or AVEVA."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
from datetime import datetime, timezone

runtime = Path(sys.argv[1]).resolve()
records = []
with tempfile.TemporaryDirectory(prefix='yuzuha-builtin-') as temporary:
    base = Path(temporary)
    env = dict(os.environ, YUZUHA_KNOWLEDGE_DIR=str(base / "knowledge"), YUZUHA_STATE_DIR=str(base / "state"))
    def call(operation, expected=0, write=False, **arguments):
        command = [str(runtime / "PmlTwin.Phayton.Cli.exe"), "op", "invoke", operation, "--input", json.dumps(arguments, ensure_ascii=False)]
        if write:
            command.append("--allow-write")
        result = subprocess.run(command, env=env, text=True, encoding="utf-8", capture_output=True, timeout=45)
        data = json.loads(result.stdout)
        records.append(dict(operationId=operation, expectedExitCode=expected, exitCode=result.returncode, result=data))
        assert result.returncode == expected and data["success"] == (expected == 0), (data, result.stderr)
        return data["data"]

    try:
        guides = call("builtin.usage", query="尝试在PDMS中查询当前元素")
        assert guides[0]["Id"] == "read-current-element"
        assert "YuzuhaReadCurrentElement" in guides[0]["Content"]
        assert len(call("builtin.usage")) == 7
        setup = call("builtin.usage", query="EVAR")[0]
        assert setup["Id"] == "environment-setup"
        assert "runtime/cli/PmlTwin.Phayton.Cli.exe" in setup["Content"]
        assert "aveva.session.status" in setup["Content"]
        assert not (base / "knowledge").exists()
        rows = call("knowledge.layers.search", query="当前元素")
        assert len(rows) == 1 and rows[0]["Role"] == "builtin"
        assert rows[0]["Guides"][0]["Id"] == "read-current-element"
        assert call("knowledge.layers.search", query="unmatched-synthetic-term") == []
        source = base / "source"
        source.mkdir()
        (source / "replacement.pmlfnc").write_text("define function !!YuzuhaReadCurrentElement()\nreturn\nendfunction\n", encoding="utf-8")
        custom = call("knowledge.source.register", write=True, role="project", name="user-input", pmlLibRoot=str(source))
        assert custom["ok"] and Path(custom["database"]).name == "custom-user-input.sqlite3"
        call("knowledge.database.build", expected=2, write=True, pmlLibRoot=str(source), dbName="project", rebuild=True)
        official = call("knowledge.source.register", write=True, role="official", name="PDMS-test", pmlLibRoot=str(source))
        assert official["ok"]
        rows = call("knowledge.layers.search", query="YuzuhaReadCurrentElement")
        assert len(rows) == 1 and rows[0]["Role"] == "builtin"
        rows = call("knowledge.layers.search", query="YuzuhaReadCurrentElement", includeSupplemental=True, topK=2)
        assert rows[0]["Role"] == "builtin" and any(row["Role"] == "official" for row in rows)
        assert sum(len(row["Result"]["hits"]) for row in rows if row["Result"]) <= 2
        assert not (base / "knowledge/project.sqlite3").exists()
        passed = True
    except Exception:
        passed = False
        raise
    finally:
        report = dict(schemaVersion=1, passed=locals().get("passed", False), executed=len(records),
                      scope="native-cli-isolated-synthetic-reference-data-no-live-AVEVA",
                      finishedUtc=datetime.now(timezone.utc).isoformat(), cases=records)
        (runtime.parent / "native-builtin.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print("PASS native CLI embedded guides, local source isolation and existing retrieval priority.")
