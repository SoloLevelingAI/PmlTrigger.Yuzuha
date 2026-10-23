# Phayton：预览、演示与名称过渡

对应 TRACE35–TRACE37。新版逐步使用 **Phayton** 作为展示名称，过渡期写 **Phayton · Yuzuha**。本批交付设计和演示，正式桌面与正式 DocFX 主题保持现有界面。检查范围见[本批验证](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-23-phayton-preview-demo.md)。

## 宽屏与设计方向

[打开可操作预览](../../artifacts/docfx/navigation/repository/docs/design/phayton-review/prototype.html) · [设计画板](../../artifacts/docfx/navigation/repository/docs/design/phayton-review/README.md)

原预览在侧栏中被压成窄屏，隐藏了最近工程和完整知识库布局。本次增加固定 1440×1024 内页的评审器，支持适应窗口、100%、全屏、独立打开，以及启动工程、知识库、设置、DocFX 首页/文章和应用页直达。独立浏览器已打开完整布局。用户目录、凭据、IPC 和真实 AVEVA 都不会被预览操作修改。

R1.1 保留浅色绿系方向和可操作原型，统一展示名称。另存 R2 方向 B：80px 主导航、独立最近工程区、模块卡片、紧凑登录字段和明确启动动作。R2 的开发入口与日常启动分区，蓝色重点只用于当前模块和主要动作；这是静态 FigCraft 概念稿，尚未替换生产 UI。R2 原生文件保留 R1 七画板、变量、组件与原型链接，并新增一个方向画板。

## 名称如何逐步使用

新设计、介绍视频和演示使用 Phayton。原仓库名、可执行文件、CLI 契约、MCP 注册名、环境变量、用户目录、PML 函数和对象标识暂不整体改名。迁移这些标识会影响兼容性，需在实施批次提供别名、配置迁移及回退验证。`PhaytonObjectWalker` 的名称与行为不因品牌过渡改变。

## 介绍视频与现场演示

已制作约 2 分 22 秒的 1920×1080 / 24fps 中文介绍初剪，包含本机合成旁白、烧录中文字幕、独立 SRT 和十幕可切换演示页。交付包保存在本机；本页保留可复用的演示内容和证据入口。视频采用设计画板、合成表格和日期明确的原生 Form 历史截图，未伪装成新录制的 AVEVA 全流程。

主线：描述需求 → AI 编写 PML → 复制到 PMLLIB → REHASH → Object/Form 重载 → IPC 回读 → 核对结果 → 改进工具。DocFX、知识库和用户目录用于让这条开发链路可理解、可复刻、可维护。

| 时间段 | 观众看到什么 | 说明 |
| --- | --- | --- |
| 0:00–0:27 | 新名称、AI / CLI / MCP / IPC / PML 架构 | 展示现有架构；不把流程动画当执行回执 |
| 0:27–0:43 | 复制、REHASH、重载、值核验，原生按钮 | E3D 2.1 历史实测：10 步、8 项值及 v1→v2 |
| 0:43–1:00 | 原生 Form 与 HTML 表格各自职责 | 2410 项为历史实测统计，画面为合成表格 |
| 1:00–1:56 | 启动工程、知识库、用户目录、DocFX | 全部明确标为设计预览，R2 为静态方向稿 |
| 1:56–2:22 | 已有证据、待解决项和收束 | 大数组最新卡顿与跨产品复验仍未关闭 |

### 五分钟可复刻演示脚本

1. **0:00–0:40：描述一个具体任务。**“做一个小工具，从自己的数组读取数据，提供原生按钮，并在 HTML 中筛选与导出。”只用合成值，不展示真实工程凭据或模型数据。
2. **0:40–1:30：展示生成的源文件。**独立 PMLFNC 计算加法，PMLOBJ 返回版本与 alpha/beta/gamma，PMLFRM 按钮调用对象。对照 XML 注释解释参数和返回值，定位[已验证夹具](../../artifacts/docfx/navigation/repository/tests/live/pml-development/README.md)。
3. **1:30–2:30：展示部署与加载。**按[开发与加载链路](pml-development-flow.md)，选择已发现的固定会话、核验实际 PMLLIB，再复制、REHASH、重载和执行验证。现场运行需要准备好的测试会话；本批只准备演示，没有新执行。
4. **2:30–3:15：验证效果。**点击自有 Form 的 Run probe，比较对象版本、sum=5 与计数；v1→v2 的版本变化证明新代码被加载。失败或未知即展示真实状态，不自动重复请求。
5. **3:15–4:15：展示 HTML。**解释按钮、方法、参数绑定；对合成结果筛选、分页、导出。表格由 HTML 呈现，程序负责桥接，PML 负责取值。真实大 ELDATA 暂不作为现场成功必选步骤。
6. **4:15–5:00：把成果留下。**在知识库记录验证和排查经验，展示用户目录保护以及 DocFX 中的源码/验证入口。最后回到“从想法到可核对的工具”。

### 证据与边界

- [PML 部署与原生按钮实测](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-23-pml-development.md)：10 步、8 项结果、FunctionSum=5、Object/Form v1→v2、真实按钮计数。
- [HTML 应用管理器实测](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-23-html-app-manager.md)：ELDATA 2410 项、首中尾比对、分页导出与函数调用。该成功不能覆盖后续卡顿。
- [当前能力核对](runtime-capability-audit.md)：最新大数组回读无响应、AM 发现缺口、任意数据库写入、自动 XML 登记等仍按原状态处理。

介绍可以突出“AI 能协助编写并验证 AVEVA 工具”，不使用“任意底层操作都已打通”“所有版本无人值守可用”一类超出证据的表述。EffectCraft 本轮没有可用工具入口；当前动效由 FigCraft/CSS 与本地视频合成完成。
