# Continuing PMLTWIN development

- Preserve and update `docs/reference/pml-development-plan.md` for requirements DEV01–DEV09, implementation status, PRs and validation evidence. Do not leave new decisions only in chat.
- Execution authorization has Conservative (registration only), Standard (per-function permission) and Development (exact session identity, source directory, reason and expiry) modes. Only explicit project rules may be bypassed. Identity, source hashes, audit and automatic MARKDB remain required.
- Every non-read-only execution must use the shared bridge and updated Host's single-call MARKDB/body/ENDMARKDB path. Begin failure prevents body dispatch. Unknown transport outcomes are never retried automatically. Do not auto SAVEWORK.
- Use CLI `--cli dev` / MCP development tools for scoped writes and hash-verified macro snapshots. Retain test source and evidence. Dispatched, assertion Passed, Unknown and Pending human review are distinct states. Do not claim human review or AVEVA live acceptance from synthetic tests.
- Named Pipe execution is local to the Windows machine running AVEVA. Cloud work can edit code, run offline tests and open PRs; never guess a live PID or claim that cloud access reaches the engineering machine.
- Build: `dotnet build YuzuhaToolkit.Development.slnx -c Release`. Tests: `scripts/Test-Development.ps1`, `dotnet run --project tests/Phayton -c Release`, `scripts/Test-Desktop.ps1`, `scripts/Build-Documentation.ps1`. Windows CI also publishes Native AOT frontends and runs NET35/48 production-service fixtures with synthetic AVEVA dependencies.
- Push completed changes and development documentation to a GitHub PR for continued cloud review. Keep current checks and actual live-test limitations in the PR description.
