# Installer source review — 2026-09-20

Author: OpenAI Codex.

The Inno Setup project is `installer/Yuzuha.iss`; `installer/build.py` stages
the payload and invokes ISCC. Preview8 adds native Inno pages in
`installer/NativePages.iss` and ships a separate installer source supplement.
The previous preview7 delivery did not include installer sources.

Setup now requires administrator privileges before the wizard starts. There
is no non-administrator override. Existing installation defaults remain
unchanged. When using credentials for a different administrator account,
LocalAppData and HKCU refer to that account; explicitly review target paths.
MCP/Skill integration remains optional. Elevation does not bypass file locks,
explicit deny ACLs, or the existing preview and preflight checks.

Resolved by maintainer approval: synchronize PMLLIB from
`D:\PmlTrigger.Yuzuha\PMLLIB`, EXCEPT
`Bootstrap/YuzuhaResolveRuntimePath.pmlfnc`. Keep the repository's portable
bootstrap unchanged; the D-drive version uses local development paths and
is deliberately excluded. D-drive originals are never modified. Keep
repository-only files rather than inferring permission to delete them.
The per-file UTC/SHA256 inventory is in `docs/semantic/pml-sync.json`.
Do not claim that the updated PML has been tested inside AVEVA.

GitHub publication is separate from local packaging. No push is performed by
this change.
