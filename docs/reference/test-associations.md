# 测试关联与证据

本阶段复用 [LaunchDesk main 的测试关联机制](https://github.com/SoloLevelingAI/LaunchDesk/tree/main/tools/documentation)，保留 `xunit:<全限定方法名>` ID、SHA256 短文件名、Roslyn 源码范围、显式 test-map 关联、详情页、复制命令与 API 反向入口。适配文件：`tools/Documentation/TestInventory.cs`、`scripts/documentation-tests.mjs`、`scripts/run-documented-test.ps1`、`docs/reference/template/public/tests.js`；不引入独立测试服务。

参考实现读取时的 Git blob：TestInventory `9e1b24a039311fe0c85c533ee112525072e8032f`，documentation-tests `fa0731c86630318675c4aaffb4d442899f1bc414`，runner `639c234ec482343191a4ecd07e839ad7d0502393`，tests.js `1bbec05f6005854aa702430464e29e877538d1c5`，test-map `23401a2770084fa120223f5bc8c1994e018c35c6`。

## 独立复刻

在 Windows / PowerShell 7 的仓库根目录，安装 .NET 10 SDK：

```powershell
./scripts/run-documented-test.ps1 -TestId 'xunit:YuzuhaToolkit.Tests.BackendBehaviorTests.PmlArgumentsKeepOrderAndEscapeQuotes'
./scripts/run-documented-test.ps1 -TestId 'smoke:native-cli'
./scripts/run-documented-test.ps1 -TestId 'manual:formuse-get-gadget'
```

`-Describe` 仅核验身份与源码。xUnit 执行器重新解析当前源码，使用精确方法过滤并核对 TRX 的每个 testId、类名和方法名；Theory 覆盖全部实际数据行。不存在、重名、零匹配、跳过、失败或错配均不能报通过。人工案例默认返回 NotRun，退出码 2；不会操作活动 AVEVA。

Native AOT / RPC Smoke 还需要 MSVC x64 工具链、Windows SDK 和 Python 3。若 `python` 指向 Windows Store 占位入口，可用 `YUZUHA_TEST_PYTHON` 指定真实 python.exe；执行器先验证该前提，再开始发布。

所有本次输出在 `artifacts/dt/<测试ID哈希>/<运行编号>/`。终端打印完整路径，`documented-run.json` 记录提交、工作区修改状态、源码 SHA256、OS/架构、SDK、PowerShell、UTC 时间、执行数量、CI Run URL 和证据相对路径。TRX 与 console.log 是实际运行证据；来源文件保留在 Git。未提交的运行标记 dirty，不能声称已验证某个干净提交。

## 维护边界

新增 xUnit 方法自动进入目录，不按类名猜测功能关联。尚未维护的验证目的明确显示“未维护”；在 `docs/reference/test-map.json` 的 bindings 中补充 purpose、environment、expected 和 targets。C# 目标使用 `项目::类型UID`，方法名必须唯一（重载可填写完整 DocFX UID）。原生 PML 目标同时校验文件和声明符号。失效关联使构建失败。

cases 登记 CLI/MCP Native AOT、NET35/NET48 独立 Host 兼容夹具、文档工具与 AVEVA 人工案例。框架夹具不加载 AVEVA，不证明相应产品已经实测。FormRecord 使用合成数据与测试窗口；需要交互桌面，不能把无桌面失败改为跳过后通过。

静态站点不自动执行测试，默认状态为未附加本次证据。历史报告有自己的时间、版本和边界，不能覆盖当前状态。CI 工件名包含提交和 OS；只上传明确的离线 TRX/元数据和静态文档，不上传整个 artifacts、用户工程、真实记录、截图、知识数据库或本机注册文件。

后续 v0.4 的 Form/Gadget 查询、WPF 调试页与本地插件目录在函数注册/权限和有界数组读取验收后继续；本阶段只登记原生窗体能力与限制。
