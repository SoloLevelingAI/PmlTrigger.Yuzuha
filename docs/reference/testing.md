# 测试与验证

```powershell
npm test
```

也可直接运行 `scripts/Test-Development.ps1`。统一入口先验证 LikeC4 生成源与白名单一致，再执行 `dotnet test YuzuhaToolkit.Development.slnx -c Release`。TRX 输出到 `artifacts/tests`。

`YuzuhaToolkit.Tests` 验证 PML 参数顺序、单引号转义、区域无关的数字、非法类型与参数、内置指南别名及单词边界、中文检索规划、FTS 引号和切分行号；进程内 HTTP 测试覆盖真实 OpenAPI、Scalar、完整指南、预览结果、400 错误及执行接口缺席。

CLI 集成测试启动实际服务进程，比较默认 MCP、显式 `--mcp` 与 `--cli` 的工具和结果；覆盖共用进程的有序 batch、协议与业务错误停止、无效输入不启动服务、退出码和无数据库内置查询。

`YuzuhaToolkit.Architecture.Tests` 用 ArchUnitNET 检查编译依赖，用 xUnit 检查源项目白名单、禁止的引用与循环，同时验证一个真实 IL 违规反例会被拒绝。

GitHub Actions 的 `development.yml` 在 Windows 上执行测试与完整文档构建，上传 TRX 和静态文档工件。`build.yml` 执行 Native AOT 发布、内置指南与 CLI/MCP 一致性 smoke；现有 Python / PowerShell / Host smoke 测试保留，可按原有说明单独运行。

这些自动化检查不代表 AM、PDMS 或各 E3D 版本实机验证通过，也不覆盖真实模型执行、安装升级和混合 DPI 截图。此类验证继续按仓库现有联测流程执行。
