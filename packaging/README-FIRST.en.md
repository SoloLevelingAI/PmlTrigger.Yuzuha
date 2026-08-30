# PmlTrigger.Yuzuha Native AOT copy-mode package

[中文](README-FIRST.zh-CN.md) | English

This is the uncompressed `v0.1.0-preview.6` Release directory. It contains:

- a single-file .NET 10 Native AOT MCP executable; no installed .NET 10 is required;
- the Net48 AVEVA host and redistributable dependencies;
- PMLUI, PMLLIB, the agent Skill, and bilingual documentation;
- copy installation and E3D `evars.init` configuration scripts.

If an agent or DeepSeek Harness will install the package, first have it read
[`INSTALL-SKILL.en.md`](INSTALL-SKILL.en.md). That document identifies the
complete Skill source and the supported discovery destinations.

It does not contain AVEVA, PMLNet, or any proprietary AVEVA assembly. The user
must have a licensed AVEVA E3D 2.1/.NET Framework 4.8 environment.

## Near one-command installation

Close E3D, then run in PowerShell:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Install-Yuzuha.ps1 -RegisterCodex -InstallCodexSkill
```

The default destination is:

```text
%LOCALAPPDATA%\YuzuhaToolkit\PmlTrigger.Yuzuha
```

The installer discovers E3D's `evars.init`, creates a timestamped backup, and
appends the installed PMLUI and PMLLIB paths. For a non-standard installation:

```powershell
.\Install-Yuzuha.ps1 `
  -EvarsInitPath 'D:\AVEVA\Everything3D2.10\evars.init' `
  -RegisterCodex -InstallCodexSkill
```

To copy files without configuring E3D:

```powershell
.\Install-Yuzuha.ps1 -SkipE3DConfiguration
```

Existing installations are not overwritten by default. With `-Force`, the
installer moves the complete previous installation into the sibling `backup`
directory before copying a clean payload. An existing Codex Skill is also
backed up before replacement, avoiding an invalid nested
`yuzuha-toolkit\skill\SKILL.md` layout.

## Verify after installation

1. Restart E3D.
2. Confirm that `!!YuzuhaRpcHost.GetRpcServerStatus()` returns `RUNNING`.
3. Call the side-effect-free `get_connection_status` and expect `Connected`.
4. Start with a read-only query; do not create a BOX or spiral merely to test connectivity.
5. In a new session, confirm that the agent can see the `yuzuha-toolkit` Skill
   before asking it to inspect connection status.

See `docs\Agent.PmlTrigger.en.md` for agent behavior and
`docs\Agent.PmlTrigger.zh-CN.md` for the Chinese version.

## Pre-release validation

```powershell
.\Test-YuzuhaRelease.ps1
```

Checksums are in `SHA256SUMS.txt`. PDB files are intentionally excluded.
