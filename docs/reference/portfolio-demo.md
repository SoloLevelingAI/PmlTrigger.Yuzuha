# Phayton：AI 开发与直接协作

Phayton 把 AI 接入 AVEVA 的 PML 能力，并提供启动工程、观察连接、管理资料和核查执行结果的工作空间。用户可以把一个想法变成可反复使用的小工具，也可以与 AI 一起阅读对象、观察视图、定位问题和核查不易察觉的关联。

## 观看演示

<video controls preload="metadata" poster="media/poster.jpg" style="width:100%;max-width:1200px" src="../media/Phayton-Workflow-zh-CN.mp4">请下载下方 MP4 观看。</video>

[下载视频](../media/Phayton-Workflow-zh-CN.mp4) · [中文字幕](../media/Phayton-Workflow-zh-CN.srt) · [分镜与旁白](../media/storyboard.json)

这支约 2 分 23 秒的视频使用正式 WPF 程序截图、已存档的 E3D 2.1 实测结果和能力示意。脚本加载与调试片段明确标为 **2026-10-23 历史实测记录重放**；它不是本次重新录制的 AVEVA 连续操作录像。

| 时间 | 内容 |
| --- | --- |
| 00:00 | 连接 AI 与 AVEVA，介绍两条路线 |
| 00:29 | 编写一个结果可核对的小工具 |
| 00:45 | 部署、加载与重新加载 |
| 00:59 | 类型错误、修正与独立重跑 |
| 01:12 | 核对结果、版本与按钮回读 |
| 01:27 | 数据与视图中的直接协作 |
| 01:42 | 工作台与资料管理 |
| 01:56 | 真实用户与社群 |

## 两条工作路线

| 路线 | 用户希望完成的事 | Phayton 提供的支持 |
| --- | --- | --- |
| 开发工具 | 将重复操作做成脚本、原生窗体或插件 | 查阅资料、编写文件、部署到已确认的 PMLLIB、加载、读取错误、修正并核对结果 |
| 直接协作 | 让 AI 结合对象与视图查清问题，找到值得核查的关联 | 读取对象与属性、获取视图、查询知识，通过 PML 核查判断并返回结果 |

连接层负责入口、上下文、执行与回传。具体任务的效果取决于 AI 的判断、可访问的数据和 AVEVA 的底层能力。直接协作片段是工作方式示意，没有宣称本次发现了某个实际工程缺陷。

## 脚本演示的证据

视频选用仓库自有的小型 PML 开发夹具：按钮调用函数与对象、显示结果与版本，再修改版本、重新加载和核对。

1. **编写**：自有函数、对象、窗体及验证宏，任务规模小，结果可检查。
2. **加载**：确认会话与 PMLLIB，预览复制计划、备份、部署，再 rehash/reload。
3. **调试**：首次验证遇到 `GADGET=MENU` 类型错误，流程停止并保留失败记录；修正访问方式后重新验证。
4. **核查**：历史成功运行完成 10 步，8 个预期值全部匹配；版本从 v1 更新为 v2，原生按钮回调计数与回读一致。

[实测记录](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-23-pml-development.md) · [机器可读摘要](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-23-pml-development.summary.json) · [现场验证说明](../../artifacts/docfx/navigation/repository/tests/live/pml-development/README.md) · [PML 开发流程](pml-development-flow.md)

## 已有人使用，也有清楚的验证范围

维护者于 2026-10-23 提供：至少 **5 位已知线上用户**，另有一个 **数十人的交流社群**。线下熟人不计入；群成员数不等于活跃用户数；开源项目的实际总用户数未知。这里没有添加用户身份、未获许可的引语或未经测量的效率指标。

本次重新运行的是桌面 0.3.4.1：Windows 11 上的原生圆角、外部阴影、最大化与恢复，以及两套主题、三个窗口尺寸下的页面回归。完整范围见[窗口与作品集验证](../../artifacts/docfx/navigation/repository/docs/validation/2026-10-24-window-portfolio.md)。旧版 AVEVA 的兼容配置与已验证宿主版本分别列于仓库 README，不能把一个 E3D 版本的结果扩展成全版本保证。

## 继续开发

- 从 [PML 开发与加载链路](pml-development-flow.md) 开始复现小工具流程。
- 从 [原生 Form 与 Gadget 查询](form-gadget-query.md) 和 [对象遍历](phayton-object-walker.md) 了解可核查的数据入口。
- 从 [桌面工作台](workspace.md) 了解工程启动、连接观察和资料管理。
- 视频的源分镜、字幕、截图和渲染脚本随仓库保存，后续可替换为经脱敏的真实工程案例。
