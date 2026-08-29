# PmlTrigger.Yuzuha local development layout

[中文](local-development.zh-CN.md) | English

## Single project root

`D:\PmlTrigger.Yuzuha` is the only source, Git, documentation, Skill, and
release project root:

```text
D:\PmlTrigger.Yuzuha\
|-- PMLUI\                         PML UI source
|-- PMLLIB\                        PML source
|-- src\                           .NET source
|-- skill\                         Skill source
|-- docs\                          project documentation
|-- artifacts\                     disposable build output
|-- runtime\                       verified local runtime staging
`-- release\                       user-facing packages
```

Do not use `%LOCALAPPDATA%\YuzuhaToolkit\PmlTrigger.Yuzuha` as the development
root. It is an installed acceptance-test copy and may be replaced by an update.

## PMLUI and PMLLIB

The current E3D `evars.init` points to the C-drive installation. Development
can use either mode:

1. **Copy mode (recommended default):** copy D-drive source into the C-drive
   installation. This stays close to the real user installation and exposes
   packaging omissions.
2. **Directory-junction mode (fast iteration):** make the C-drive `PMLUI` and
   `PMLLIB` junctions target their D-drive source directories. Use this only on
   the development machine, and remove or back up the junctions before running
   the Release installer.

Open `D:\PmlTrigger.Yuzuha` directly in VS Code. Do not maintain another source
history inside the C-drive installation.

## MCP and runtime

The MCP executable location is independent of the PML location loaded by E3D.
A development client may register:

```text
D:\PmlTrigger.Yuzuha\runtime\win-x64-nativeaot\YuzuhaToolkit.Mcp.exe
```

Only then is the client using the D-drive development MCP. Installing a Skill
does not register MCP; inspect the client's MCP list to verify the executable
path.

Do not publish one project directly to both C and D. Use:

```text
src -> artifacts -> runtime -> local C-drive deployment -> release
```

Build into `artifacts`, promote verified outputs into D-drive `runtime`, then
copy them to C in a separate deployment step. Close E3D before replacing the
Net48 host and stop the relevant MCP process before replacing its executable.

## Installer versus daily development

- `Install-Yuzuha.ps1` is for a new machine, Release acceptance, and user
  installation. It handles backup, `evars.init`, and optional MCP/Skill setup.
- Daily development sync should copy only the intended PMLUI, PMLLIB, or runtime
  output instead of rerunning the full installer.
- Before release, run the full installer once to catch packaging omissions.

The Skill source is `D:\PmlTrigger.Yuzuha\skill`. Its installed layout must be
exactly one level:

```text
%USERPROFILE%\.codex\skills\yuzuha-toolkit\
|-- SKILL.md
`-- references\
```

Do not create `yuzuha-toolkit\skill\SKILL.md`, and do not put runtime DLL/EXE
files inside the Skill.
