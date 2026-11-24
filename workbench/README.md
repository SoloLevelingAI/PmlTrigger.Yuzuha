# PmlTwin Workspace 0.2.7

This directory contains the standalone WPF desktop, the UI library and the Core30 sources used by the desktop. Phayton Setup 0.3.5.6 installs this desktop and the native CLI. Use the [configuration guide](../docs/reference/pmltwin-first-install.md) to install with or without an AI client.

## Build the desktop

From the repository root on Windows with the .NET 10 SDK:

```powershell
dotnet publish workbench/PmlTwin.Workbench/PmlTwin.Workbench.csproj -c Release -r win-x64 --self-contained true -p:BundlePhayton=false -p:BundleProjectReader=false -o artifacts/workbench-0.2.7
```

Pass that publish directory to the [Windows installer builder](../installer/README.md) as `--desktop` and use `--version 0.3.5.6`. The test release reuses the verified 0.3.5.5 native CLI, product-specific Hosts and documentation runtime. The installer assembles them beside the desktop.

The pre-release includes the native CLI and optional AVEVA integration sources. SAG and the project Reader are separate. No AVEVA SDK installation is required to build the WPF project. Target-side AVEVA use requires a supported installation, an appropriate license, and authorization for the selected project.
