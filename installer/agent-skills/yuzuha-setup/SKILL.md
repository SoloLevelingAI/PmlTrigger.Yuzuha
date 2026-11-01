---
name: yuzuha-setup
description: Install or update the extracted Yuzuha Windows Inno Setup distribution, or prepare optional CLI usage Skill integration, using a reviewed installation plan. Use for this archive's lifecycle, not live AVEVA operations.
---

# Yuzuha Windows Setup

Read [references/INSTALL.md](references/INSTALL.md) completely before tool use.
The build places the matching installation guide there. Resolve the bundle
root as two levels above this skill directory; it contains `setup`, `skill`,
`.setup-payload.json`, `BUNDLE-SHA256SUMS.txt`, and one Setup EXE.

1. Verify all paths and SHA256 entries in `BUNDLE-SHA256SUMS.txt` before
   executing the helper or EXE. Reject absolute/traversal paths. A bundled
   hash detects corruption, not publisher authenticity; use the user's trusted
   download and separately supplied archive hash when available.
2. Read `VALIDATION.md`. Application-only Manager handoff uses `plan` as below.
   Legacy direct AVEVA integration runs the helper on the Windows machine that
   has AVEVA (inside the VM if AVEVA is there); use `plan-auto` to let the program
   discover environments and construct its proposal, not model-guessed paths.
   `discover <new-json>` is available for diagnosis; read the output JSON after
   execution, because discovery writes a file rather than printing candidates.
   `locate <new-json>` locates existing Yuzuha installations, not AVEVA.
3. Confirm the install directory and selected environment files/profiles.
   Keep `PmlTrigger` in the install folder name. Registry discovery is a
   recommendation, not authorization. Before generating a plan, resolve the
   client's runtime Skill choice. If it is not already explicit, ask once whether
   to install the CLI usage Skill, then wait for the answer. Respect earlier choices.
   Confirm the client, Windows account and exact supported runtime Skill directory.
   CLI is installed as runtime/cli/PmlTwin.Phayton.Cli.exe and needs no MCP service.
   This CLI-only distribution rejects any nonempty mcpJson request and does not
   modify existing MCP entries. Never re-enable a disabled MCP as an installation
   side effect. Include selected AVEVA and Skill changes in the same preview.
4. Create a new request using `request.example.json` as a schema example,
   not as real paths. New installations default to explicit `integrationMode:
   "manager"` and empty environments; invoke `plan` to install the application
   and defer EVARS/deployment to the Manager preparation task. This is distinct
   from none and reports `manager-preparation-required`, not AVEVA connected.
   Only when the user explicitly chooses legacy direct AVEVA integration, use
   `integrationMode: "aveva"` and invoke `plan-auto` as documented, using the bundle's
   `.setup-payload.json` and **runtime `skill` directory**, not this setup skill.
   Present the generated Markdown preview, changed paths and before/after
   lines. Wait for user approval before running the EXE. Automatic candidates
   do not authorize execution. If plan-auto refuses, read `<plan>.discovery.json`
   and its reported reasons; ask the user to resolve ambiguous/missing targets,
   then generate an explicit selected-environment request with `plan`.
   Never silently drop a product or change to `integrationMode: "none"` merely
   to get a successful exit. None mode requires the user's files/client-only
   intent. An empty default AVEVA request is rejected by the program.
5. Execute the same EXE with the approved `/PLAN`, `/PLANHASH`, and matching
   `/DIR`. Use the documented silent flags only after approval. UAC cannot be
   bypassed: an unelevated agent must request elevation through its supported
   process launcher or ask the user to launch it. Wait for exit and inspect
   the matching `result.json`/`result.md`; an exit code alone does not prove
   AVEVA or the AI client loaded successfully. Check `avevaIntegration`,
   `environmentCount` and each environment path/hash, not just `status=success`.

On failure, record the error and available reports; do not blindly retry,
delete ownership markers, overwrite conflicts, or run vendor EVARS scripts.
Do not launch AVEVA, rehash PML, rebuild runtimes, or index knowledge as an
installation side effect. Preserve user data and D-drive development files.

Reading this skill guides the current task; it does not register a skill in
the client. Permanent runtime Skill copying occurs only through the approved
`skillTarget`. Report separately: CLI/files installed, AVEVA configuration written,
Skill files copied, and actual client loading tested or not tested.
