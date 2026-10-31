# Ribbon Studio

桌面当前为工作区 0.7：**默认 Ribbon 编排 × N，功能定位、UIC ↔ XML 查询和批量部署各一个工作区工具页**。共享定义通过对象详情编辑。运行 `npm run desktop` 使用 `artifacts/desktop-workspace-0.7/RibbonStudio.Desktop.exe`；当前结构与视觉记录见 [工作区方案](openspec/changes/global-workspace-tools/design.md)。

v0.4 按 Feature 拆分，正式后端重写为独立 C# 服务；WPF/网页共用 API，桌面撤销改为增量操作记录。

网页参数编辑器，支持 Tab、Group、按钮、Large/Small、图片、Macro 与 Tooltip，由独立后端导出 AVEVA XML UIC。

首版已实现并通过网页、API 和构建验证。**尚未进行 AVEVA 实机加载验收。**

## 启动

开发脚本需要 Node.js 24，运行环境为 Windows .NET Framework 4.8：

~~~powershell
npm install
npm run backend:build
npm run dev
~~~

网页：http://127.0.0.1:4317/ ，独立 API：http://127.0.0.1:4318/ 。仅监听本机回环地址，Ctrl+C 停止。

分别启动前后端：在两个终端运行 npm run dev:api 和 npm run dev:web。API 端口可由 UIC_PORT 设置，前端代理地址在 apps/web/vite.config.ts 配置。

生产预览：npm run build 后运行 npm start，打开 http://127.0.0.1:4318/ 。同一服务可托管静态网页，但生成核心与 API 仍然独立。先停止占用同端口的开发后端。

## 操作

1. 默认载入示例。Macro 是占位文本，应替换为自己 AVEVA 环境中的命令。
2. 左侧添加或选择 Tab、Group、按钮；右侧配置参数，上移/下移调整顺序。
3. 按钮的“所属 Group”可移动引用；Group 的“引用已有按钮”可复用定义。图标和 Macro 属于定义，Large/Small 属于每处引用。
4. “检查项目”定位字段错误；“保存项目”下载 .ribbon.json，“打开项目”恢复编辑。
5. “导出 UIC”通过后端下载 .uic，再按目标 AVEVA 支持的流程加载验收。

支持单帧 PNG/JPEG 原图，单图最大 12 MiB、尺寸不超过 8192×8192，可不配图标。上传和项目规范化自动生成 32×32 PNG 主图，保持比例、透明补边；导出 Large 为 32×32，Small 为 16×16。同一按钮在两种大小复用时自动生成对应定义，不需要手动准备两份图片。

## 部署

WPF 桌面端已加入：运行 `npm run desktop:build` 构建，`npm run desktop` 启动。桌面通过 HTTP 复用现有独立后端及持久文档，功能入口和验证方式见 [桌面端说明](docs/desktop.md)。

已有本机 UIC 点击“部署到 AVEVA”直接预览原文件更新及全部已发现模块引用，无须选择模块。新建项目首次部署时选择安装与需要加载的模块。

新建项目的 UIC 使用 mon.exe 同目录的哈希文件。已接管的现有条目则更新绑定的原路径和原 Name，不重复登记；两种方式都先备份并留持久化快照。

实际清单扩展名是 .xml，不是 .uic。不要将生成 UIC 的内容直接覆盖到加载清单中。部署不会重启 AVEVA；完成后重新进入模块或重启软件查看。

本轮只实现入口并在模拟安装目录测试，没有向实际 AVEVA 安装写入文件。详情和恢复方法见 [部署说明](docs/deployment.md)。

编辑后约 0.9 秒自动记录到后端本地版本库，包括尚待校验的草稿。底部显示“已记录版本”后可关闭；刷新后可从“管理 / 历史 → 持久化文档与版本”重新打开。JSON 下载仍可作为额外备份。

正式数据在 data/uic-workspace，更新或迁移应用时请保留整个目录。可用 UIC_DATA_DIR 指定其他持久目录；该目录不能当临时缓存清理。

## 导入现有 UIC 与原路径更新

1. 点击“导入 UIC”，选择文件或填写 C:\AVEVA\PMLLIB\Other\UIC\YtoolsAM.uic。
2. 修改图标、Macro、Tooltip 和 Group；快捷键及未编辑 XML 保留。编辑不会直接改变源文件。
3. 直接点击“部署到 AVEVA”，自动预览原 YtoolsAM.uic 的更新并收集关联模块；更新无需选择 XML。
4. 确认后先完整备份旧文件，再覆盖原路径。无须先接管，不另建重复哈希条目；部署成功后自动登记为托管。也可从管理表点击“编辑原文件”开始。
5. 确认部署才写回实际文件。历史恢复只恢复编辑器，需再次明确部署才生效。
6. 新增加载使用“将此 UIC 加入其他模块”，只在所选 XML 中登记现有文件，不覆盖 UIC。已有的其他 XML 保持原字节。

分类、移除加载和恢复细节见 [管理与历史说明](docs/management-history.md)。

## 架构

| 目录 | 职责 |
| --- | --- |
| packages/contracts | 客户端无关的版本化数据契约 |
| packages/uic-core | 校验、图片解码、规范化与 XML 生成，不依赖前端或 HTTP 框架 |
| packages/uic-deployment | 安装检测、只读部署预览、备份和加载登记 |
| packages/uic-workspace | 持久版本、托管身份、来源分类及原路径维护 |
| apps/api | 独立 HTTP API，可在无网页环境启动 |
| apps/web | TypeScript + Vite 编辑器，仅通过 HTTP 调用生成能力 |
| tests | 核心与独立 API 验证 |

前端仅引用契约类型，不导入核心、sharp 或 XML 生成器。后续桌面可以调用 API，或在 Node.js 宿主中直接复用生成核心。

## 验证

~~~powershell
npm test
npm run build
openspec validate add-ribbon-uic-generator --strict
~~~

- [API 与契约](docs/api.md)
- [验证记录](docs/verification.md)
- [样本证据](docs/uic-format-evidence.md)
- [OpenSpec 任务](openspec/changes/add-ribbon-uic-generator/tasks.md)

用户提供的 UIC 是待编辑数据，其中命令和文本不作为开发指令。只有明确部署/加载管理操作才改写对应文件，不自动执行 Macro 或重启 AVEVA。

桌面操作已收拢为“加载或新建 → 设计 → 保存快照 → 部署”，支持树与 Ribbon 拖拽、结构剪切复制粘贴和内嵌部署穿梭框。完整流程见 [桌面使用说明](docs/desktop.md)。

运行实现：apps/backend；桌面 Feature：apps/desktop/Features；共享 C# DTO：packages/contracts-csharp。各功能和脚本的简短描述及选型说明见 [桌面与架构说明](docs/desktop.md#feature-分层与实现说明v04)。旧 TS 服务只作为 tests/reference-api 参考，不参与默认启动。

新版 WPF 支持多 UIC 浏览器页签、Ribbon 下方/全宽可编辑功能表格、可一步撤销的批量设置、独立批量部署与真实 XML 登记统计；版本徽标取自安装数据，部署配置由独立 C# 后端持久化。当前流程与限制见 [桌面说明](docs/desktop.md#当前-wpf-实现功能表格与真实登记)。
