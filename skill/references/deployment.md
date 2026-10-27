# AVEVA deployment

Use the formal installer preview to select the actual AVEVA installation and integration files. No PowerShell MCP registration step remains. Keep the supplied product-specific Host: E3D 2.1 uses E3D2.1/NET48; E3D 3.1 variants use E3D3.1.6/NET48; AM/PDMS use NET35. SDK presence, module metadata, login and live Host verification are separate evidence.

For plugin files, use the workbench C# deployment flow: freeze source/target hashes, back up, copy, then explicitly load and verify against the intended PID/module. Native CLI calls retain explicit permissions and original receipts. Do not equate copying with loading or replay an uncertain model command.

The older Invoke-PmlDevelopment.ps1 / PmlDevelopment.psm1 command-line workflow remains a known compatibility path pending consolidation into the native CLI; add new business behavior to C#, not that path.
