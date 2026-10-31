# Project and Module discovery

## Why
The user intentionally shares E3D2.1 projects with E3D3.1. Discovery must follow
actual configuration, expose source evidence and preserve editable choices.

## What Changes
- Add one concrete C# Project/Module discovery script and bounded text reading.
- Inspect initialization references and project children without executing scripts.
- Keep multiple candidates and provenance; prefill only an unambiguous declared value.
- Expose results through existing records and editable fields without redesigning UI.
- State that installation discovery currently uses configured directories, not Registry.

## Impact
No original/Rewrite writes, dependency changes, vendor launches, remote connection
changes or credential writes. Remote configuration and portable credentials are separate proposals.
