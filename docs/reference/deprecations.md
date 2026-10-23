# 弃用与兼容入口登记

登记日期：2026-10-23。依据 `main` 的 `5efe62a1292ca936c76e5c08e230dcca85c74e4e` 源码引用、PML 索引、项目引用、CI 与打包入口审查。

## 当前处理方式

本批只标记、保留并说明迁移方向。没有删除或移动旧文件，没有改名、移出 PML 索引、排除发行载荷、禁止调用或设置移除日期。仓库内没有调用，不代表仓库外的宏或程序集没有使用。

“已弃用”表示停止新增依赖，现有代码保留；“历史配置 / 待评估”表示尚未决定是否正式停止支持。文件在 Obsolete 目录或文档中默认隐藏，不等于已经从运行环境移除。

## 已确认弃用，继续保留

| 入口 | 原因 | 迁移方向与保留边界 |
| --- | --- | --- |
| `src/YuzuhaToolkit.Mcp.Core/BfsCache.cs` / `IBfsCache` | 仓库内没有实现、使用方或注册；SQLite 缓存实现从未交付 | 不新增缓存依赖，继续通过现有查询工具读取；没有对应的替代缓存 API。接口及所有成员保留，XML 文档明确标为 Deprecated；不新增编译警告，避免现有“警告即错误”构建因此失败 |
| `PMLLIB/Obsolete/YuzuhaTriggerCommand.pmlcmd.obsolete` | 源码已声明由 `YuzuhaExcuter.pmlobj` 替代 | 迁移到现有执行器或当前原生命令工具；旧文件及历史扩展名保留，不新增索引项 |
| `PMLLIB/Obsolete/YuzuhaRpcCommand.pmlcmd.obsolote` | 源码已声明由 `YuzuhaRpc.pmlobj` 替代 | 使用现有 `YuzuhaRpc` 启动链；旧文件及 `obsolote` 拼写保留，避免破坏按路径使用者 |
| `PMLLIB/Obsolete/FileBrowseTest.pmlfnc` | 旧手工截图试验，含固定 `D:\` 输出路径及顶层收集语句 | 后续先梳理实际使用者，再迁成专用手工测试；本批不改函数体、顶层语句或索引 |
| `PMLLIB/Obsolete/YuzuCollctTest.pmlfnc` | 旧收集试验，结果仅保留在局部变量，没有返回或断言 | 新工作评估现有 `YuzuhaGetAllFor` / `YuzuhaGetAllWithIn`，不能假定参数与行为完全等价；旧函数保留 |
| `PMLLIB/Obsolete/YuzuhaShortCut.pmlfnc` | 旧截图流程，保留调试输出，当前仓库没有活动调用方 | 评估现有 `YuzuhaSetView`、`Yuzuha3DViewShot`、`YuzuhaSaveClipboard` 组合；跨版本行为仍需实机验证，旧入口继续索引与交付 |
| `tests/RpcCompatibilitySmoke/` | 旧动态代理客户端夹具，当前 CI 不调用它 | 当前验证入口为 `tests/AotRpcSmoke`，运行 `smoke:rpc-net35` 与 `smoke:rpc-net48`；旧客户端、服务端、项目及依赖完整保留 |
| `src/lib/net10/` 的旧 DLL/XML | 当前生产 MCP 使用源码项目 `PlantHost.Rpc.Net10`；旧二进制仅被上述历史测试引用 | 新客户端使用源码项目；历史夹具及其五个 DLL/XML 不拆开删除，许可证继续保留 |
| `scripts/Package-Release.py` | v0.3.2 ZIP 打包入口，输出名与当前 Setup/Agent 分发不同 | 当前 Windows Setup/Agent 使用 `installer/build.py`；旧脚本继续可运行，输出名、参数、校验和及复制范围不变，`--help` 显示弃用说明 |

PML 五个文件只新增 `-- DEPRECATED` 注释，原函数体和命令定义保留。三个 `.pmlfnc` 仍在 `PMLLIB/pml.index` 中；两个旧命令保持原先未索引的状态。两个打包脚本仍复制 PMLLIB 全目录，没有增加 Obsolete 排除规则。

## 历史配置，支持策略待评估

- `win-x64-self-contained.pubxml`：项目默认 `PublishAot=true`，旧配置还请求 `PublishSingleFile=true`、`PublishTrimmed=false`。保留原属性；不能把标记当成这一组合已通过发布验证。是否继续支持非 AOT 单文件发布，另行决定并验证。
- `win-x64-nativeaot.pubxml`：保留原优化和输出设置；新发行构建优先使用 `scripts/Build-NativeServers.ps1`。配置合并另做变更。
- `docs/publishing.zh-CN.md` 与英文版：保留历史验证矩阵，并在顶部明确日期与适用范围。当前 AOT 生产代码和 CI 状态应从现行构建入口及当次运行报告核对。

## 仍在使用的兼容入口

以下内容保持现有状态，不标记为本批弃用：

- `PMLLIB/Examples/YuzuhaExcuter.pmlobj`、`YuzuhaTargetQuery.pmlobj`：Addin 仍创建执行器，执行器仍依赖 TargetQuery，内置指南和真实窗体测试使用它们。
- `PmlCallTools` 的既有 MCP 工具名与 Phayton 操作入口：旧工具转发共享操作，兼容输出继续由命令回归验证。
- NET35 与 NET48 两套 Host、协议契约及 `src/lib/net35` / `src/lib/net48`：分别支持不同 AVEVA 框架和 SDK。
- `scripts/Install-YuzuhaAgent.ps1`、`Update-YuzuhaAgent.ps1`、`Uninstall-YuzuhaAgent.ps1`：仍有独立生命周期用途，不因历史 ZIP 打包脚本弃用而一并弃用。
- 两份解决方案、Shared 链接文件、`AotRpcSmoke`、`FormRecordSmoke` 和桌面测试夹具：继续保留各自构建和验证用途。

## 后续真正移除时

需要独立 PR 说明实际使用者、替代入口、迁移步骤与验证证据，并获得维护者明确决定。同步检查 PML 索引、项目引用、按名称调用、打包载荷、升级迁移、文档目录和 Semantic 元数据；既有 `pml-sync.json` 是历史快照，不回写成新的历史事实。

仓库删除提交合并后，也不会自动清理已有电脑上的旧部署文件。移除前必须明确安装升级如何处理这些文件，避免留下失效入口或误清理用户自有文件。
