# REVIEW——开源目录多人审查

[English](REVIEW.md) | 中文

该目录是私有源码的清理导出镜像，仅用于审查。审查者不应直接修改镜像，而应在
规范源码中修复并重新导出。

## 审查方式

1. 克隆或打开目录，并检查 Git 历史。
2. 将发现写入 `reviews/reviewer-<name>.md`，每位审查者独立记录：
   - 结论：通过、条件通过或拒绝；
   - 阻断项：发布前必须修复；
   - 建议项：建议修复或考虑；
   - 问题：需要作者确认。
3. 问题解决后在 README 状态清单中签字确认。

## 检查清单

### R1 敏感信息

- [ ] 配置、文档和注释中没有个人账号或个人路径。
- [ ] 没有凭证、API Key 或会话数据。
- [ ] 已提交文件中没有本机绝对路径。

### R2 AVEVA 再分发

- [ ] 没有提交 `PMLNet.dll`、`Aveva.*`、`Infragistics.*`、
      `ForeignLanguage.dll`。
- [ ] README 明确 AVEVA Runtime 是前置条件，不随项目提供。

### R3 第三方许可证

- [ ] `NOTICE.md` 清单完整准确。
- [ ] MIT/BSD 等依赖的许可证材料已提供或正确引用。

### R4 PlantHost.Rpc 所有权

- [x] 项目自有；`THIRD-PARTY.md` 和许可证副本已记录。

### R5 BfsCache / SQLite

- [x] SQLite 实现已排除，只保留可选 `IBfsCache` 接口。

### R6 AVEVA HintPath 参数化

- [x] Host 项目通过 `src/build/AvevaSdk.props` 使用
      `$(AvevaInstallDir)`；见构建文档。

### R7 示例匿名化

- [ ] 文档和 Skill 中的真实 DBREF、元素名称应替换为通用占位符。

### R8 可复现构建

- [ ] `NuGet.config` 能使用离线源或网络源恢复。
- [ ] README 提供构建命令。

### R9 PML 发布准备

- [ ] 动态 DBREF、全局变量和属性输入得到严格验证。
- [ ] 示例不会在 Addin 启动时自动执行。
- [ ] 空盘管示例已实现，或明确标为站点私有扩展。
- [ ] 对象图遍历能披露重复和截断。
- [ ] Runtime 发现支持可移植 Release 布局。
- [ ] Net48/Net35 支持声明有真实 AVEVA 集成证据。

详细双语发现见 `docs/pml-open-source-review.zh-CN.md` 和
`docs/pml-open-source-review.en.md`。
