# Host/PML 严格验收 · 2026-10-31

本次在 E3D 2.1 APS 会话完成严格 Host 身份核验及两次内置有界 PML 读取。此前 Host=Unknown 的失败历史见 [启动工作流验收](launch-workflow-2026-10-31.md)及 [静态部署调查](../../openspec/changes/integrate-pmltwin-product-platform/features/launch-workflow-host-followup.md)，保留原记录，不将新验收追写为旧启动回执的成功。

## 部署范围与回退

实装根为 `C:\Users\26648\AppData\Local\YuzuhaToolkit\PmlTrigger.Yuzuha`。备份根为 `C:\Users\26648\AppData\Local\YuzuhaToolkit\PmlTrigger.Yuzuha\backups\host-bootstrap-20261031T052842-f7febc7e`。只更新下表前两个 PML 文件，目标安装后摘要已核对；索引保留原内容。没有修改 Host DLL、EVARS 或工程模型。

| 相对路径 | 更新前 SHA256 | 更新后 SHA256 |
|---|---|---|
| PMLLIB/Bootstrap/YuzuhaResolveRuntimePath.pmlfnc | E134D87C9DDDB7864D56AEB617CEE3239D64162FFD0B0827B5AD55ABDE873950 | D38A4FDE4BFD8CE723E49EFDEF4856ACBA1138AC85950783541F020131F78BF2 |
| PMLLIB/Addins/YuzuhaRpc.pmlobj | 4D65EBBFF161EB71C9EFC49B7E2EE3C2A3F9F4F0AA3A88B83746CE859C153677 | 8E4486A10BEE89545783F85F90B6B29BCEB447DEDD1360A41881E0452785F560 |
| PMLLIB/pml.index（未更新） | E222A8CDB1764FA697CE3D1CC8BB1119142ADE733CEFF0A5C79A4CF92390BF29 | E222A8CDB1764FA697CE3D1CC8BB1119142ADE733CEFF0A5C79A4CF92390BF29 |

回滚步骤：先正常退出使用该部署的产品会话；按 [部署清单](host-pml-verification-2026-10-31.evidence/deployment-manifest.json) 先校验两个备份文件 SHA256 等于各自 beforeSha256，且当前目标 SHA256 等于各自 afterSha256，任一不一致立即停止，避免覆盖后续用户改动。全部匹配后才将备份中的 resolver、Rpc 两文件复制回各自 target，核对恢复后的 SHA256 等于 beforeSha256；不覆盖 DLL、EVARS 或未修改的索引。重新启动产品并用新发现的 PID、进程创建时刻、pipe 与 expectedModel 严格重验。恢复旧启动链可能再次出现 Unknown，不能沿用本次成功证据。本次未执行回滚演练。

## 实机证据与限制

2026-10-31 05:31 UTC，严格选择 PID **8408**、`expectedModel=DESIGN`，工具返回 `SessionSelected=true`、`Connected=true`、`TargetVerified=true`；Host Model=DESIGN，pipe=`yuzuha.pml.command.v1.pid-8408`，framework=net48，HostVersion=0.3.2.0。没有放宽 expectedModel。[MCP 原始选择回执](host-pml-verification-2026-10-31.evidence/strict-selection.raw.json)未经过 JSON.parse，精确创建 ticks 为 `639290214396762401`，SelectedSession、Expected、Host 三处一致。汇总 live-evidence.json 曾经 JS Number 转换，ticks 有精度损失，不能当完整 Int64 原始值；身份以原始回执及实际工具的 TargetVerified 结论为证，新调用必须重新发现身份。

内置读取 `!!YuzuhaReadCurrentElement(30,2,'')` 返回 Success=true、Code=OK、Count=30，当前元素为 WORL；`!!YuzuhaReadGlobal(5,1,'','YuzuhaRuntimePath')` 同样返回 OK、Count=1，路径指向实装 E3D2.1/net48 Host。均为有界内置读取，无模型写入。原始回执见 [当前元素](host-pml-verification-2026-10-31.evidence/current-element.raw.json)、[运行路径](host-pml-verification-2026-10-31.evidence/runtime-read.raw.json)，另有 [汇总副本](host-pml-verification-2026-10-31.evidence/live-evidence.json)。证据从 artifacts 原样复制，未通过重新序列化修复或伪造 ticks。[启动回执](host-pml-verification-2026-10-31.evidence/aps-launch.json)与[核验时截图](host-pml-verification-2026-10-31.evidence/aps-verified.png)一并留存。

Host 的 FormQueryVersion 与 BoundedArrayReadVersion **仍为 0**。本次证明严格身份与上述内置读取可用，不证明查询协议 v1 已支持，不能把 Manager 的协议灯宣称为绿色；也不推广到 E3D 3.1、AM、PDMS 或其他会话。

YuzuhaRuntimePath 全局值只证明内置读取返回了配置路径，不证明进程实际加载的程序集文件身份；本次没有读取已加载程序集路径或运行中 DLL 内容摘要。

## 仓库回归与待修项

部署指纹已覆盖 Host DLL、resolver、Rpc、Addin、pml.index 及可选环境文件。固定 `C:/Users/26648/.dotnet/dotnet.exe` 编译 0 警告、0 错误；ConnectionEvidenceTests 18 项、ManagerConnectionTestBuildsEvidence 3 项，共 **21 项通过**。覆盖逐文件字节变化清除全部旧绿灯、必需文件缺失失败、Unknown 模型拒绝；合成 fixture 测试与本次实机证据分开记录。

验证命令为 `dotnet build tests/YuzuhaToolkit.Manager.Tests/YuzuhaToolkit.Manager.Tests.csproj --no-restore -m:1 -v:minimal`，随后运行 `dotnet test tests/YuzuhaToolkit.Manager.Tests/YuzuhaToolkit.Manager.Tests.csproj --no-build --no-restore -m:1 --filter FullyQualifiedName~ConnectionEvidenceTests --verbosity normal` 与同命令的 `--filter FullyQualifiedName~ManagerConnectionTestBuildsEvidence --verbosity minimal`。其中 dotnet 均为上述固定绝对路径。结果保留在本次终端工具回执，未生成持久 TRX；受限沙箱内首次测试宿主等待后终止，提升后两组全部通过，没有把等待的运行当作通过。

根代理另用 PowerShell Int64 解析原始 strict-selection.raw.json，确认 TargetVerified=true、Model=DESIGN，SelectedSession、Expected、Host 三处 ticks 完全相等 `639290214396762401`；该精确原文补足汇总 JSON 的数值损失。

本次 APS 通过原生退出确认正常关闭，无保存提示；PID 8408 与脚本 PID 10788 均已不存在，TTY 会话自然 exit 0，finally 执行完成，未修改模型。启动回执仍保留 entry-process-created 与 hostVerification=unverified，外部严格核验不追写该回执。

已知待修：管理 CLI 经 PowerShell `Out-String` 管道时观察到 EOF/继承句柄疑似导致等待的问题；APS 退出后 Out-String 立即完成，进一步支持后代持有输出句柄的解释。根因尚未确认，也未在本次修复，不应将工具直连读取成功描述为 CLI 等待问题已经解决。

本轮后续：现代独立 CLI / Host 兼容修复及 APS 严格实机验收已通过，详见 [2026-10-31 CLI / Host 验证报告](cli-host-compatibility-2026-10-31.md)。历史验证范围仍按本文原文保留。
