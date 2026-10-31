# Design

## Context

见 proposal.md 的动机和 docs/uic-format-evidence.md 的原始文件核对。用户明确要求先做网页，后续迁移桌面，后端不能与前端粘连。首版已实现，网页和独立 API 验证见 docs/verification.md；AVEVA 实机验收仍待完成。

## Goals / Non-Goals

**Goals:** 通过稳定数据契约隔离界面与 UIC 生成；利用本地样本建立最小可验证格式；保证同一已保存项目的导出稳定。

**Non-Goals:** 首版不解析任意 UIC、不复刻全部 AVEVA Ribbon 控件、不执行 PML、不改系统 design.uic。布局预览不保证与 AVEVA 像素一致。不提供公网多用户托管。

## Decisions

### 1. 三层结构

采用 TypeScript 的独立生成核心、Node.js HTTP 服务和 Vite + TypeScript 网页客户端，运行于 Node.js 24。核心不依赖浏览器 DOM 或 HTTP 请求对象，前端只消费契约和后端结果，不生成最终 XML。首版采用原生 DOM 交互；XML 使用 xmlbuilder2，图片解码使用 sharp，版本由 package-lock.json 锁定。

计划目录：`packages/contracts` 定义版本化数据契约，`packages/uic-core` 做校验和序列化，`apps/api` 提供 HTTP 服务，`apps/web` 负责交互。后续桌面可启动同一后端或调用同一核心，无须重写 UIC 规则。

将所有逻辑放浏览器会使桌面复用、统一校验和导出测试耦合 UI，因此不采用。采用 AVEVA 内插件将限制网页开发和独立运行，首版亦不采用。

### 2. 编辑模型与 UIC 分开

项目模型包含 `schemaVersion`、业务 `namespace`、`buttons[]` 和 `tabs[]`。按钮包含 `id`、`caption`、`macro`、稳定 `commandKey`、单份 `image`、`tooltipText` 和 `tooltipTitle`；Tab 内含 Group；Group 内保存有序 `items[{buttonId, size}]`，其中 size 为 large 或 small。按钮引用与定义分开，同一按钮可复用，显示大小属于引用。

图片以媒体类型和 Base64 字节进入项目，避免导出依赖用户机器的绝对路径。首版接受 PNG/JPEG，原图上限 12 MiB、解码尺寸上限 8192×8192，总请求上限 20 MiB；这些是本工具的处理上限，不是 AVEVA 官方限制。UI 可建议使用方形小图，但不宣称 16/32 等尺寸是已验证限制。

项目 JSON 负责再次编辑；UIC 仅负责交付 AVEVA。首版不承诺 UIC → 项目的完整反向转换。用户所说的 Large/Small 当前按显示大小解释；分别选择两份图片需补原生样本后另立变更。

### 3. API 和前端责任

- `POST /api/v1/projects/validate`：接受完整项目，返回 `valid` 和 `issues[{code,path,message}]`，校验失败仍返回完整问题列表。
- `POST /api/v1/projects/normalize`：验证项目并分配缺失的工具生成标识和 `Macro:<GUID>` Key，返回规范化项目；已存在的值保持稳定。版本不支持或项目非法返回 422。
- `POST /api/v1/uic/export`：接受规范化项目，成功返回文件内容与 `.uic` 下载名；项目错误为 422，超限 413，不支持的请求媒体类型 415。

首次创建按钮后由前端请求 normalize，并将返回项目保存在编辑状态和项目文件中。export 不临时生成随机 ID、时间戳或 Key；同一规范化项目得到相同字节。空白显示名和仅空白 Macro 无效，但合法 Macro 的前后空格与换行不裁剪。

前端负责树形或列表编辑、参数表单、布局示意、项目文件选择/下载和导出按钮。所有导出调用后端，项目重开也经过后端校验。核心可在无网页进程时独立测试和运行。

### 4. XML 输出策略

采用 XML 构建器正确写出文本和属性，不拼接用户输入。保持已观察的根级结构与命名空间，未使用的根级容器写空；按自定义样本的元素顺序输出。业务 Namespace 另行写出。普通 Tab 写 `ContextualTabGroup/Item=None`。

按钮 `ButtonTool` 写 Name、Image、Command、Caption、HelpContext、Tooltip 字段和 DisplayStyle。Command 写 Type=Macro、原始 Macro、稳定 Key 和空 Arguments。TooltipFormattedText 将纯文本转换为转义后的段落内容，再作为 XML 文本序列化，避免将用户文本当作富文本执行或错误转义。

Group 引用写 `Tool Name=buttonId` 和显式 LargeImage=True/False。各列表保留项目顺序，UTF-8、固定换行和缩进。图标由后端处理为 PNG，按 Large/Small 分别以 32×32 / 16×16 嵌入 Image；PNG 与 JPEG 均需在目标 AVEVA 中验收，若特定版本不支持则建立版本配置，不能将 XML 可解析当作兼容通过。

### 5. 运行与保存

首版服务仅绑定本机回环地址；开发前端通过代理访问，发行版本可由同一个服务托管静态网页，但生成核心和 API 仍保持独立。无需数据库，项目由用户下载 JSON 后保存。导出返回字节，不接受任意后端写文件路径。

此方案将文件写入交给浏览器下载，可用于未来桌面保存对话框；相比直接写 AVEVA 目录，减少环境依赖，也便于比较和回滚生成结果。

## Risks / Trade-offs

- [只有样本，没有完整厂商 Schema] → 样本结构测试与实机加载分别验收；不报告未经验证的兼容性。
- [业务 Namespace 或 Name 与已有 UIC 冲突] → 新项目使用独立前缀，记录加载合并行为；导出校验项目内重复，不能假装知道整个 AVEVA 会话的名称集合。
- [图标在大小布局下模糊或格式不兼容] → 验收包含 PNG/JPEG 和 Large/Small；发现差异后调整版本输出策略。
- [Macro 中的命令在点击后失败] → 生成器只保证文本保真；在用户指定目标环境后逐项点击验收并记录错误。
- [未来桌面需携带服务运行时] → 独立核心和版本化 HTTP 契约均保留，不提前绑定桌面壳技术。

## Migration Plan

先完成核心和无前端 API 验证，再接入网页。导出到用户选定下载位置，按目标 AVEVA 支持的入口手动加载自定义 UIC并记录产品、版本及结果。回滚为移除该自定义 UIC或恢复其旧副本。现有安装文件不参与迁移。
