# Built-in-first Native AOT candidate — validation

Local candidate dated 2026-09-06. Not installed into the current user environment and not published remotely.

- Both NET10 servers were published as Native AOT using Build-NativeServers.ps1. The manifest binds each EXE's SHA-256 to this publish. No installed .NET 10 runtime is required on the target; NET35/NET48 AVEVA Host prerequisites remain.
- Published MCP stdio initialize/tools-list/tool-call tests passed. A fresh empty knowledge directory still returns the complete current-element guide for “尝试在PDMS中查询当前元素”. Registry/INIT guidance is available through get_builtin_usage too.
- Supplemental custom and official sources containing the same built-in symbol did not replace the built-in result. Optional reference inclusion and bounded supplemental result count passed. User source registration cannot overwrite the reserved project database.
- Existing knowledge tests passed: source indexing, full chunk retrieval, failed rebuild retention, append-only experience/idempotency and explicit project-only refresh.
- Lifecycle tests passed with a mocked Codex CLI and isolated fixture directories: base install creates no SQLite index; update preserves optional databases/trust/custom Hosts; conflict handling and rollback retain recoverable state; success prints restart reminders.
- The AOT RPC adapter passed synthetic NET35 and NET48 Host tests for identity, command request/response, Unicode, booleans, arrays, date values and heartbeat. Tests never execute actual PML or attach to AVEVA.
- Batch-style INIT is accepted by the writer after the target format is verified. PowerShell syntax validation passed. Registry queries and actual INIT editing were not performed on this machine.

PMLLIB/PMLUI and prebuilt NET35/NET48 Hosts are retained; live AVEVA acceptance remains necessary to verify the target installation, startup chain, window discovery and model behavior. The registry/INIT-first guide accepts batch-style INIT but does not claim a parser for arbitrary configuration languages: an Agent must inspect the actual file syntax and active launcher before editing it.

Reproduce:

```powershell
.\scripts\Build-NativeServers.ps1
python -X utf8 tests/v03/builtin_smoke.py runtime/net10
python -X utf8 tests/v03/knowledge_smoke.py
.\tests\v03\lifecycle_smoke.ps1
```

Build tests/AotRpcSmoke/Client with Native AOT and Server/Server35 into a test directory containing test-client, test-server and test-server35; run tests/AotRpcSmoke/run.py with that directory and the desired server subdirectory. These are protocol tests, not real model tests.
