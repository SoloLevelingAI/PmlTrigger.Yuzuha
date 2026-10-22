# PMLTWIN v04：对齐用户提供的蓝色 v03

当前文件为 `PMLTWIN-v04.figcraft`。输入是用户指定的 `yuzuha-v03/PMLTWIN-v03.figcraft`，SHA256 为 `18112044246baf9790d0ee3c2d61fe0b3b7767fc4cb8ce6ea1bff3594d5da981`；原件未修改。可复刻的原始副本在 `baselines/PMLTWIN-v03-blue.figcraft`。本目录此前的 v03 为上一轮基于 v02 的历史修订，不再作为当前视觉基线。

保留用户原有 12 页、蓝色系、四组导航、字号和构图。新增两页：明暗模式与实现约定、设计疑问与圈注。审阅页内有 7 张带圈注的原稿副本，原有工作页不删减疑问内容。

## 明暗模式

- 沿用原有 60 个 Theme 变量和 Light / Dark 模式；侧栏底部新增可点击切换入口。
- 原型采用 setVariableMode，工作页继承 Auto，使切换后跨页导航保持主题。
- `v04-previews/Light` 和 `v04-previews/Dark` 分别包含全部 12 页与 7 张圈注副本；`themes.png` 为变量和实现约定。
- 原有 High Contrast 变量保留；本批 WPF 交付 Light / Dark。
- WPF 两份资源字典由 `theme-tokens.json` 和 `brand-fixed.json` 生成，蓝色用于操作，绿/琥珀/红保留状态语义。

## 圈注内容

| 编号 | 位置 | 处理 |
| --- | --- | --- |
| Q01 | 总览、AI 趋势 | 保留健康度与图表区域；文件预算、当前保留范围和完整长期统计分开。 |
| Q02 | 启动命令 | 继续使用现有 LaunchService.Preview，不按示意命令重新拼接口。 |
| Q03 | 会话中的当前元素、对象查询、原始控制台 | 保留卡片与字段；尚未接入的桌面动作禁用并标注。 |
| Q04 | 函数名、签名、源码差异 | 当前全局契约用合法名称与 STRING/REAL/BOOLEAN；旧正文不能由哈希还原。调用参数可展开。 |
| Q05 | 测试详情与当前结果 | 完整身份与同次证据可定位；证据失效不能同时显示本次通过；实测项保持 NotRun。 |
| Q06 | 评审与迁移 | 同一人工审核；必需 NotRun 阻止批准，源码/契约/证据快照及迁移阶段分开。 |
| Q07 | Web 桥接与权限 | 打开、桥接和调用许可分开；页面不自行登记或授予许可，本阶段不开放任意 PML。 |

圈注保留 v04 审阅时的状态。后续功能纠正已将当前对象、指定元素（名称 / 路径 / DBREF）、全局变量和成员遍历，以及本窗口知识检索接入 WPF。知识索引构建 / 经验写入、客户端配置、日志筛选 / 导出仍待交付。当前实现范围见 `docs/reference/desktop-m1.md`，不以历史圈注替代当前功能说明。

## WPF 对照

主区恢复原稿的总览横幅、四指标、分组图与会话表；会话页保留当前元素、对象查询、最近查询和控制台；能力页把有界数组放回下方。AI 运行与工具日志分成不同页面。评审、测试、Web、知识库与设置使用原稿的卡片/表格结构，待接入项显示空状态和明确标记，未使用截图作为运行界面。

使用正文 12–13px、辅助文字 11–12px；小于这些尺寸的设计文字不直接照搬。窄窗口改为单列并可滚动。对照材料位于本机 `artifacts/ui-v04-comparison/`，实际合成截图在 `artifacts/ui-v04-final/`。截图是离线合成验证，不是 AVEVA 实测或人工视觉验收。

## 继续编辑与复刻

```powershell
python design/figcraft/build_v04.py --input design/figcraft/baselines/PMLTWIN-v03-blue.figcraft --cli '<本机 figcraft-cli.exe>' --output-dir '<新目录>'
python scripts/sync-desktop-themes.py
```

第一条通过 FigCraft MCP 串行另存和导出，不改输入文件。第二条只生成 WPF 的主题资源字典。手工编辑后的设计不要原地用生成脚本覆盖；重新生成时使用另一个输出目录。
