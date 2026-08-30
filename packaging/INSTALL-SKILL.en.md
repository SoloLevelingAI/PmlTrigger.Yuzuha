# Install the YuzuhaToolkit Skill for an Agent

[中文](INSTALL-SKILL.zh-CN.md) | English

This document is written for an agent that receives the Release folder. The
Skill is already included and does not need to be downloaded.

## Source directory

Resolve this path relative to the current Release root:

```text
payload\PmlTrigger.Yuzuha\skill
```

The source must contain both:

```text
skill\
|-- SKILL.md
`-- references\
```

Copy the whole directory contents, not only `SKILL.md`, or the agent will miss
the MCP, deployment, and mutation workflow references.

## DeepSeek Harness

Prefer this user-level discovery location:

```text
%USERPROFILE%\.dsh\skills\yuzuha-toolkit\
|-- SKILL.md
`-- references\
```

For a specific Harness project, this project-level location is also valid:

```text
<project-root>\.dsh\skills\yuzuha-toolkit\
```

Do not create `yuzuha-toolkit\skill\SKILL.md` or add another nesting level.
`SKILL.md` must be directly under `yuzuha-toolkit`.

## Codex

The installer supports:

```powershell
.\Install-Yuzuha.ps1 -InstallCodexSkill
```

Its equivalent user-level layout is:

```text
%USERPROFILE%\.codex\skills\yuzuha-toolkit\
|-- SKILL.md
`-- references\
```

## Update policy

1. Check for an existing version or user edits before installation.
2. A fresh install may copy the entire directory. Do not silently overwrite an
   existing copy; back it up or compare changes first.
3. MCP registration and Skill discovery are independent. A connected MCP does
   not prove that the agent loaded this Skill.
4. Restart Harness or start a new session after copying so the Skill catalog is
   refreshed.
5. Confirm that `yuzuha-toolkit` appears in the available Skill list before
   testing the E3D connection.

## Minimal instruction for another agent

> Read `INSTALL-SKILL.en.md` in the current Release root. Install the complete
> `payload\PmlTrigger.Yuzuha\skill` into your user-level Skill discovery
> directory. Do not overwrite existing edits. Verify that
> `yuzuha-toolkit\SKILL.md` and `references` are siblings, then tell me whether
> Harness must be restarted or a new session started.
