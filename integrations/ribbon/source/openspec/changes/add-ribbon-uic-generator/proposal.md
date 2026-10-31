# Proposal

## Why

手工维护 AVEVA Ribbon UIC 时，按钮定义、Group 内引用、图片编码与 Macro 容易不一致。用户希望通过参数编辑自动生成 Ribbon 支持的 UIC，并先使用网页，后续迁移桌面，同时保持后端独立。

## What Changes

- 提供网页编辑器，按普通 Tab → Group → 按钮组织，支持名称、顺序、Large/Small、图标、Macro 与 Tooltip。
- 独立后端承担参数校验、图片编码、引用检查和 XML UIC 序列化；前端通过 HTTP API 调用，不拼装最终 UIC。
- 提供结构预览、项目保存/重开及 `.uic` 下载；项目文件与 AVEVA 输出文件分开。
- 保持生成核心独立于浏览器和网页服务框架，供未来桌面程序复用。
- 首版仅生成新的独立自定义 UIC；不做任意现有 UIC 的无损导入、不修改系统文件、不自动加载或执行 Macro，不实现菜单、状态按钮、上下文 Tab 和快捷键。
- Large/Small 首版解释为按钮布局大小，配合一个图标；独立大小两份图片的需求待原生格式证据支持后扩展。

## Capabilities

### New Capabilities

- `ribbon-uic-authoring`：参数化编辑 Ribbon、校验项目、独立后端生成 UIC 及保存可再次编辑的项目。

### Modified Capabilities

无。新建项目中没有既有规格。

## Impact

新建独立项目 `UicRibbonDesigner`，包含前端、HTTP 后端和生成核心。参考用户提供的两个本地 UIC，证据见 `docs/uic-format-evidence.md`。初期采用本地浏览器访问本机服务，尚不包含公网部署。生成正确性需要 XML 结构检查与目标 AVEVA 实机验收共同确认。
