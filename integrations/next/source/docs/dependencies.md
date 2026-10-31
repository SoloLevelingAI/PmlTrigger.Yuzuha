---
document_kind: reference
implementation_status: mixed
reviewed_on: 2026-10-29
uid: dependencies
---
# 实际解析依赖与许可

**已批准但未安装**：[TOML库选型](decisions/toml-selection.md)确定Tomlyn 2.10.1；用户要求暂不安装，不计入下方实际依赖。ArchUnitNET等已批准依赖保持原版本。

直接精确版本由 Directory.Packages.props 唯一维护；以下从实际 packages.lock.json 和包内 nuspec 生成。测试传递依赖不进入产品运行层。

| 包 | 实际版本 | 许可声明 |
|---|---|---|
| CommunityToolkit.Mvvm | 8.4.2 | MIT |
| NLog | 6.2.2 | BSD-3-Clause |
| coverlet.collector | 10.1.0 | MIT |
| Microsoft.NET.Test.Sdk | 18.11.0 | MIT |
| NUnit | 5.0.0 | MIT |
| NUnit3TestAdapter | 6.3.1 | MIT |
| TngTech.ArchUnitNET | 0.13.4 | Apache-2.0 |
| TngTech.ArchUnitNET.NUnit | 0.13.4 | Apache-2.0 |
| CycleDetection | 2.0.0 | MIT |
| JetBrains.Annotations | 2026.2.0 | MIT |
| Microsoft.ApplicationInsights | 2.23.0 | MIT |
| Microsoft.CodeCoverage | 18.11.0 | MIT |
| Microsoft.Extensions.DependencyModel | 8.0.2 | MIT |
| Microsoft.Testing.Extensions.Telemetry | 2.4.1 | MIT |
| Microsoft.Testing.Extensions.TrxReport.Abstractions | 2.4.1 | MIT |
| Microsoft.Testing.Extensions.VSTestBridge | 2.4.1 | MIT |
| Microsoft.Testing.Platform | 2.4.1 | MIT |
| Microsoft.Testing.Platform.MSBuild | 2.4.1 | MIT |
| Microsoft.TestPlatform.AdapterUtilities | 18.10.1 | MIT |
| Microsoft.TestPlatform.ObjectModel | 18.11.0 | MIT |
| Microsoft.TestPlatform.TestHost | 18.11.0 | MIT |
| Mono.Cecil | 0.11.6 | MIT |
| Newtonsoft.Json | 13.0.4 | MIT |
| System.ValueTuple | 4.6.2 | MIT |
| FlaUI.UIA3 | 5.0.0 | LICENSE.txt |
| FlaUI.Core | 5.0.0 | LICENSE.txt |
| Interop.UIAutomationClient | 10.19041.0 | LICENSE.txt |
| Microsoft.Win32.SystemEvents | 8.0.0 | MIT |
| System.CodeDom | 8.0.0 | MIT |
| System.Configuration.ConfigurationManager | 8.0.1 | MIT |
| System.Diagnostics.EventLog | 8.0.1 | MIT |
| System.Diagnostics.PerformanceCounter | 8.0.1 | MIT |
| System.Drawing.Common | 8.0.10 | MIT |
| System.Management | 8.0.0 | MIT |
| System.Security.Cryptography.ProtectedData | 8.0.0 | MIT |
| System.Security.Permissions | 8.0.0 | MIT |
| System.Windows.Extensions | 8.0.0 | MIT |
