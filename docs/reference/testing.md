# 测试与验证

[测试目录与按 ID 复刻](../../artifacts/docfx/navigation/tests/index.md)自动发现 xUnit 方法，并关联 API 与原生 PML 函数。执行器先核验源码身份，再检查每个实际 TRX 结果；零匹配、跳过或错配不能视为通过。报告默认保留在本机 `artifacts/dt`，静态站点不把历史结果显示为当前通过。

```powershell
./scripts/run-documented-test.ps1 -TestId 'xunit:YuzuhaToolkit.Tests.FunctionRegistryTests.ExplicitPermissionProducesTypedEscapedCallAndCanBeRevoked'
./scripts/run-documented-test.ps1 -TestId 'smoke:rpc-net48'
./scripts/run-documented-test.ps1 -TestId 'manual:formuse-get-gadget'
```

注册权限、有界数组读取和验证限制见 [PMLTWIN 第一阶段](pmltwin-phase1.md)；Gadget/FormUse 的源码问题与人工步骤见 [支持登记](formuse-audit.md)。NET35/NET48 RPC 夹具、Native AOT CLI Smoke 和 AVEVA 实机分别分类。

```powershell
npm test
```

也可直接运行 `scripts/Test-Development.ps1`。统一入口先验证 LikeC4 生成源与白名单一致，再执行 `dotnet test YuzuhaToolkit.Development.slnx -c Release`。TRX 输出到 `artifacts/tests`。

`YuzuhaToolkit.Tests` 验证 PML 参数顺序、单引号转义、区域无关的数字、非法类型与参数、内置指南别名及单词边界、中文检索规划、FTS 引号和切分行号；进程内 HTTP 测试覆盖真实 OpenAPI、Scalar、完整指南、预览结果、400 错误及执行接口缺席。

CLI 集成测试启动实际原生 CLI，验证 inline/file/stdin JSON、46 个操作描述、权限、失败退出码、有序 batch 和无数据库内置查询。旧 MCP 入口必须拒绝启动，不能以协议兼容测试重新启用。启动配置通过同一服务完成持久化、冲突、历史与恢复；JSON/TOML 场景每步启动独立 CLI，并保存 HTML 与逐步 JSON 报告。详见[工作台约定](workbench-lifecycle.md)。

`YuzuhaToolkit.Architecture.Tests` 用 ArchUnitNET 检查编译依赖，用 xUnit 检查源项目白名单、禁止的引用与循环，同时验证一个真实 IL 违规反例会被拒绝。

GitHub Actions 的 `development.yml` 在 Windows 上执行测试与完整文档构建，上传 TRX 和静态文档工件。`build.yml` 执行 Native AOT 发布、内置指南与 原生 CLI 与知识能力 smoke；现有 Python / PowerShell / Host smoke 测试保留，可按原有说明单独运行。

这些自动化检查不代表 AM、PDMS 或各 E3D 版本实机验证通过，也不覆盖真实模型执行、安装升级和混合 DPI 截图。此类验证继续按仓库现有联测流程执行。

启动配置完整场景可按 ID 复刻：

```powershell
./scripts/run-documented-test.ps1 -TestId 'smoke:workbench-launch-lifecycle'
```
