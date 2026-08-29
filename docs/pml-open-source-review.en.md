# YuzuhaToolkit PML Open-Source Release Review

Review date: 2026-08-29  
Scope: `PMLLIB`, `PMLUI`, the .NET 10 Native AOT MCP, the Net48 PMLNet host,
the agent Skill, and release scripts.

## Verdict

**Suitable for a Preview/Experimental open-source release; not yet suitable
for a stable v1 tag.**

The public tree contains no credentials, personal paths, business-model data,
or proprietary AVEVA assemblies. The Net10 MCP now uses typed
`PlantHost.Rpc.Net10` calls and source-generated JSON instead of
`DispatchProxy`; its Native AOT executable passed a startup smoke test. The
Net48 host still requires a licensed user-installed AVEVA/.NET Framework 4.8
environment.

## Resolved in this preview

- `YuzuhaAddin` registers `!!YuzuhaRpcCommand` and
  `!!YuzuhaTriggerCommand`; duplicate RPC-host startup is deduplicated by the
  host.
- A two-second heartbeat and side-effect-free `get_connection_status` tool
  report state, latency, consecutive failures, and the last error.
- CE, DBREF, and global-object reads have depth and item limits; DBREF reads
  restore the original CE.
- BOX is a bundled example. Spiral demonstrations use site-provided
  `!!NewSpiral(...)` and `!!RotateMyspiral(...)` functions. The public docs
  and Skill teach this workflow without inventing or distributing the private
  site implementation.
- Execution tools require explicit user authorization and must not be retried
  automatically after a timeout.
- Releases exclude `Aveva.Core.Utilities.dll`, `PMLNet.dll`, and
  `ForeignLanguage.dll`.

## Remaining work before stable v1

### P0: Dynamic PML input boundary

DBREFs, global names, attribute paths, and macro paths eventually reach dynamic
PML evaluation. The Preview relies on an explicit trusted-local-user boundary.
A stable release should enforce character allowlists, maximum lengths, and
path scopes, and clearly label advanced dynamic entry points as unsafe.

### P1: Traversal and error model

Object-graph BFS still relies mainly on depth and item limits. Add stable object
identity, a visited set, explicit truncation reasons, and structured errors in
place of string sentinels.

### P1: Real AVEVA integration evidence

Record the target E3D version and bitness, Addin loading, heartbeat reconnect,
command execution, exception recovery, and shutdown cleanup. Validate the
spiral demonstration separately in an environment that provides the site
functions.

### P1: Third-party release process

Retain the PlantHost.Rpc and Newtonsoft.Json licenses. Every release should
scan for proprietary DLLs, run the release validator, verify SHA-256 hashes,
and distribute binaries through GitHub Releases instead of source history.

## Release gate

- [x] Net10 Native AOT startup smoke test.
- [x] Heartbeat and connection-status tool.
- [x] Correct `YuzuhaTriggerCommand` name and spiral guidance.
- [x] Skill, PMLUI, PMLLIB, MCP, and installer delivered together.
- [x] Proprietary AVEVA assemblies excluded.
- [ ] Input allowlists and structured traversal errors.
- [ ] Integration verification in a target AVEVA E3D environment.
- [ ] Independent review and sign-off before a stable tag.
