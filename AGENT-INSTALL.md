# Yuzuha Agent lifecycle

This archive is intended to be extracted to a temporary directory and operated
by a trusted local Agent on Windows. Run the scripts from the extracted package
root. Do not run update or uninstall from the managed installation directory.

## Install

```powershell
.\scripts\Install-YuzuhaAgent.ps1
```

Defaults:

- Package: `%LOCALAPPDATA%\YuzuhaToolkit\PmlTrigger.Yuzuha`
- Skill: `%CODEX_HOME%\skills\yuzuha-toolkit`, or
  `%USERPROFILE%\.codex\skills\yuzuha-toolkit`
- MCP name: `YuzuhaToolkit`

For PDMS or AM, optionally configure the explicit EVAR file during install:

```powershell
.\scripts\Install-YuzuhaAgent.ps1 `
  -InstallRoot 'D:\YuzuhaToolkit' `
  -AvevaProfile PDMS `
  -EvarBat 'D:\AVEVA\Plant\PDMS12.1.SP4\evars.bat'
```

The installer refuses to overwrite an existing installation or Skill. It
registers the MCP only when no matching entry exists and stops on conflicts or
possible duplicates.

## Update

Extract the new archive somewhere other than the managed installation, then:

```powershell
.\scripts\Update-YuzuhaAgent.ps1
```

To migrate a verified older Yuzuha installation containing `install-info.json`
and an existing `yuzuha-toolkit` Skill, run once with
`-AdoptLegacyInstallation`. This flag never adopts unrelated directories.

Use the same `-InstallRoot` and `-CodexRoot` values chosen during installation.
Update requires matching management markers in both locations. It stages the
new package and Skill before swapping them and rolls back if the swap or MCP
validation fails. Close AVEVA before updating because a loaded NET35/NET48 host
may lock old files.

## Uninstall

Run from an extracted setup archive, not from the installed directory:

```powershell
.\scripts\Uninstall-YuzuhaAgent.ps1
```

If install used custom roots, pass them again. For AM/PDMS, pass `-EvarBat` to
remove only the marked Yuzuha block; a backup is created first:

```powershell
.\scripts\Uninstall-YuzuhaAgent.ps1 `
  -InstallRoot 'D:\YuzuhaToolkit' `
  -EvarBat 'D:\AVEVA\Plant\PDMS12.1.SP4\evars.bat'
```

Uninstall removes the MCP only when it points to the managed executable. A
conflicting MCP is preserved. Files and Skill directories are deleted only
when their Yuzuha management markers match.

Restart Codex after install, update, or uninstall. Fully restart AVEVA after
changing EVAR or host files.
