---
name: yuzuha-setup
description: Install or update the extracted Yuzuha Windows Inno Setup distribution, or prepare optional MCP and runtime Skill integration, using a reviewed installation plan. Use for this archive's lifecycle, not live AVEVA operations.
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
2. Read `VALIDATION.md`. Run the helper on the Windows machine that has AVEVA
   (inside the VM if AVEVA is there). Use `plan-auto` below to let the program
   discover environments and construct the proposed plan, not model-guessed paths.
   `discover <new-json>` is available for diagnosis; read the output JSON after
   execution, because discovery writes a file rather than printing candidates.
   `locate <new-json>` locates existing Yuzuha installations, not AVEVA.
3. Confirm the install directory and selected environment files/profiles.
   Keep `PmlTrigger` in the install folder name. Registry discovery is a
   recommendation, not authorization. Before generating a plan, resolve the
   client's MCP/Skill choice. If the user has not specified it, ask once:
   "是否同时为目标 AI 客户端注册 MCP、安装 Skill？可以都安装、仅选其中一项，或暂不接入。"
   ("Register MCP and install the runtime Skill for your AI client as part of
   this installation? Choose both, either one, or neither.") Wait for an answer;
   silence is not a decline. Respect an already explicit choice without asking
   again, including files-only/no-client requests.
   For selected integrations, confirm the client, Windows account, exact MCP
   configuration file and/or runtime Skill directory. Being invoked from
   Workbuddy does not prove its configuration paths or supported format.
   Verify existing configuration or client documentation; ask if unresolved.
   Never treat TOML as JSON. Leave only declined/not-applicable targets blank.
   Include AVEVA and selected client changes in the same preview. Do not defer
   this question until installation finishes or register anything automatically.
4. Create a new request using `request.example.json` as a schema example,
   not as real paths. Keep `integrationMode: "aveva"` and initially empty
   environments; invoke `plan-auto` as documented, using the bundle's
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
`skillTarget`. Report separately: files installed, client configuration written,
Skill files copied, and actual client loading tested or not tested.
