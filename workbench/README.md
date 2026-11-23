# Phayton Workspace 0.2.6

This directory contains the WPF workbench, the UI library, and the backend
sources used by Phayton 0.3.5.5. The trial uses saved login records for user
and MDB suggestions. New entries can be typed directly in the form.

## Build the trial workbench

From the repository root on Windows with the .NET 10 SDK or later:

```powershell
dotnet publish workbench/PmlTwin.Workbench/PmlTwin.Workbench.csproj -c Release -r win-x64 --self-contained true -p:BundlePhayton=false -p:BundleProjectReader=false -o artifacts/workbench-0.2.6
```

Pass the publish directory to [the installer builder](../installer/README.md)
as `--desktop` and set `--version 0.3.5.5`. The Windows EXE uses the Native
AOT CLI, product-specific Hosts, and documentation host verified for
0.3.5.3. Those runtime files have the same hashes as in that release.

## Verify the trial

The installer SHA256 at publication is
`26693ff745b4805839845af7eee27fe1adae5ff36e742f76ab97d9fb689498f8`.
The build inventory covers 921 installed files including 37 raw PML
sources. The installer was unsigned at publication.

An isolated upgrade from 0.3.5.4 retained 876 files with identical bytes
and completed while one of them was held open. The old bytes of a changed
program file were present in its backup. The installed workbench opened,
reused saved user and MDB choices, and saved a profile without AVEVA
license settings.

A Windows AI client can run the [native CLI](../docs/ai-client-setup.md)
locally. Install the package, configure AVEVA integration in Workspace,
and check the target session with the CLI. The trial distribution omits
the standalone MCP executables and keeps client configuration under the
client's control.
