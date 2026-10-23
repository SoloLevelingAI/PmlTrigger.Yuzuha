# PML 部署、重载和验证

## manual:pml-development

2026-10-23 在 E3D 2.1 / NET48 Host 0.3.2.0 实测：独立测试载荷从源码复制到会话实际 PMLLIB，再执行 REHASH ALL、Object/Form 重载、验证宏、IPC 结果读取和精确核对。v1 → v2 覆盖时验证了旧文件备份；真实按钮点击也已核对。完整结果与限制见[本批报告](../../../docs/validation/2026-10-23-pml-development.md)。不修改现有 Walker 调用，也不保存模型。

开发入口为 `scripts/Invoke-PmlDevelopment.ps1`，复用已有 `PluginDeployment` 和独立 `PmlTwin.Phayton.Cli`，没有增加 RPC 协议或替换 Host。需要 Windows、.NET 10 的 Workspace 构建、PowerShell 7.6（本次为 .NET 10.0.11 / PowerShell 7.6.5）和已启动的 AVEVA 会话。

在仓库根目录先构建 `src/PmlTwin.Phayton.Cli` 与 `src/YuzuhaToolkit.Workspace` 的 Release，再使用 CLI 的 `aveva.sessions.list` 查找实际 PID。`RegisteredPmlLib` 必须来自该会话 PMLLIB 查询，支持其返回的短路径；示例中的变量由操作者填写真实发现值：

```powershell
pwsh -NoProfile -File scripts/Invoke-PmlDevelopment.ps1 `
  -CliExecutable src/PmlTwin.Phayton.Cli/bin/Release/net10.0/PmlTwin.Phayton.Cli.exe `
  -SourceDirectory tests/ai-pml/development-probe `
  -RegisteredPmlLib $sessionPmlLib `
  -Manifest tests/live/pml-development/manifest.json `
  -ProcessId $avevaProcessId -ExpectedModel $hostModel `
  -EvidenceDirectory artifacts/manual/pml-development/new-run -AllowExecution
```

每轮使用新的证据目录。流程会先核验发现结果、PID/启动时间和会话实际 PMLLIB，再冻结来源/目标哈希并复制。备份放在证据目录内、PMLLIB 外。复制、重载、派发成功与实际值核对分别记录；任一步失败或结果未知即停止，不自动重发，也不把文件恢复等同于已加载状态恢复。

`manifest.json` 显式声明目标子目录、Object/Form 类型、验证宏、结果全局数组和预期值。载荷必须在源码目录根层包含对应文件。验证宏返回格式化行，示例为 `[0] FunctionSum<REAL> 5`；流程严格检查行数、路径唯一性、值及 UnparsedCount。结果数组只包含本夹具自有数据，读取后删除。

原生测试 Form 展示三项合成数据，并按 `attributes()` / `attribute()` / `objectType()` 区分属性，再读取已设置 GADGET 的 `type()`。不把不同原生类型反复赋给同一个局部变量。它借鉴了 pmlforms 的分类/浏览思路，没有复制原厂源码或宣称实现全控件浏览器。

流程证据保存在本机 `run.json` 和 `rpc.json`，包含每步状态、哈希、备份、Host 原始回执和 RequestId。它们可能包含机器路径或现场信息，不直接提交。公开报告只保留合成值、统计和源码指纹。

离线回归：`pwsh -NoProfile -File tests/pml-development.test.ps1`。HTML 表格回归：`node --test tests/pml-data-table.test.mjs`。表格导出：

```powershell
node scripts/pml-data-table.mjs artifacts/manual/pml-development/new-run/run.json artifacts/manual/pml-development/new-run/results.html
```

HTML 支持本地 JSON 导入、搜索和导出当前显示行的 CSV；也接受既有 `pml.array.read` 的 OperationResult。它展示已读取的快照，不包含可从网页直接执行 PML 的生产桥。PML 不负责写 HTML 或 CSV。测试后可通过原生命令 `hide !!YuzuhaDevProbeForm` 隐藏本夹具窗体；部署文件保留用于复刻。
