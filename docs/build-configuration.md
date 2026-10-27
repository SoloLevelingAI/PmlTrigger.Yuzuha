# Build entry points

The production entry points are the C# WPF workbench and native CLI. Business workflows stay in C#; AVEVA-side PML and existing UI technologies remain separate platform concerns.

Build the native CLI with scripts/Build-NativeCli.ps1 and the desktop with scripts/Build-Desktop.ps1. These are Windows build glue. The old Build-NativeServers alias and MCP registration/install chain have been removed; no standalone MCP executable is produced.

Use docs/reference/installation.md for the formal installer and docs/reference/cli.md for current invocation contracts. Do not introduce a new language or runtime for a business feature.
