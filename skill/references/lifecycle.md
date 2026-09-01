# Agent-managed lifecycle

Use these scripts only from an extracted setup archive on Windows. Do not run
update or uninstall from the managed installation directory.

## Defaults and invariants

- Package: `%LOCALAPPDATA%\YuzuhaToolkit\PmlTrigger.Yuzuha`
- Skill: `%CODEX_HOME%\skills\yuzuha-toolkit`, or
  `%USERPROFILE%\.codex\skills\yuzuha-toolkit`
- MCP name: `YuzuhaToolkit`
- Install and Skill directories must contain matching
  `.yuzuha-agent-managed.json` markers before update or deletion.
- Never bypass a marker or MCP conflict. Ask the user before explicitly
  removing a conflicting configuration.

## Install

```powershell
.\scripts\Install-YuzuhaAgent.ps1
```

For PDMS or AM, optionally configure the explicit EVAR file during install:

```powershell
.\scripts\Install-YuzuhaAgent.ps1 `
  -InstallRoot 'D:\YuzuhaToolkit' `
  -AvevaProfile PDMS `
  -EvarBat 'D:\AVEVA\Plant\PDMS12.1.SP4\evars.bat'
```

Install refuses to overwrite an existing package or Skill. It registers the
MCP only when no matching entry exists, reuses the exact expected entry, and
stops on conflicts or possible duplicates.

## Update

Extract the new archive somewhere other than the managed installation, then:

```powershell
.\scripts\Update-YuzuhaAgent.ps1
```

For a verified older Yuzuha installation that has `install-info.json` and an
existing `yuzuha-toolkit` Skill but no lifecycle markers, migrate it once with:

```powershell
.\scripts\Update-YuzuhaAgent.ps1 -AdoptLegacyInstallation
```

The legacy install-info root and Skill identity must match before adoption.

Pass the same `-InstallRoot` and `-CodexRoot` values used for installation.
Update requires matching markers, stages the new package and Skill before
swapping them, and rolls back a failed swap or MCP validation. Close AVEVA
before updating because a loaded NET35/NET48 host may lock old files.

## Uninstall

Run from an extracted setup archive:

```powershell
.\scripts\Uninstall-YuzuhaAgent.ps1
```

Pass custom roots again if used during installation. For AM/PDMS, pass
`-EvarBat` to remove only the marked Yuzuha block; the file is backed up first:

```powershell
.\scripts\Uninstall-YuzuhaAgent.ps1 `
  -InstallRoot 'D:\YuzuhaToolkit' `
  -EvarBat 'D:\AVEVA\Plant\PDMS12.1.SP4\evars.bat'
```

Uninstall removes the MCP only when it points to the managed executable. It
preserves a conflicting MCP. It deletes package and Skill directories only
when their management markers match.

Restart Codex after install, update, or uninstall. Fully restart AVEVA after
changing EVAR or host files.
