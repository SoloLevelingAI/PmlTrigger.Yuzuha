# 本地 UIC 格式核对

核对日期：2026-10-28。读取 XML，未修改原文件，未执行其中 Macro。

| 来源 | SHA-256 | 根级 Tools 子项 | 普通 Tab | 普通 Tab 内 Group |
| --- | --- | ---: | ---: | ---: |
| `C:/AVEVA/PMLLIB/Other/UIC/YtoolsAM.uic` | `5EAB874FC16788B3814543F734B82ACE2AC9A2A7539499CD544F2798F193FC86` | 10 | 1 | 2 |
| `C:/Program Files (x86)/AVEVA/Everything3D3.1/design.uic` | `4A7801C9E45779F30A54333D558470A7A354E179FCBEB38766D9606E94C1A4C3` | 542 | 14 | 117 |

## 已观察到的结构

- 两者均可由 XML 解析器读取；根元素 `UserInterfaceCustomization`，XML 命名空间为 `www.aveva.com`，`Version` 为 `1.0`。
- 根级 `Namespace` 为业务值，示例分别为 `HuangYuanTai` 和 `Aveva`；与 XML 命名空间不同。
- 按钮定义位于根级 `Tools/ButtonTool`，通过 `Name` 标识。
- 自定义文件在按钮的 `Image` 中嵌入 Base64 图片；首个图片以 JPEG 签名开头。系统文件也大量使用 `ImageResourceId`，不能据此假定这些系统资源可在独立工具中解析。
- Macro 位于 `Command`，包含 `Type=Macro`、`Macro`、`Key`、`Arguments`。样本中 Key 既有宏文本，也有 `Macro:<GUID>`，首版统一选用后者并保存稳定值，仍需实机验证。
- 按钮提示字段为 `Tooltip`、`TooltipFormattedText`、`TooltipTitle`。系统其他类型的元素还出现 `ToolTip`，不能混用大小写。
- 布局位于 `Tabs/Tab/Groups/Group/Tools/Tool`。Tool 的 Name 引用按钮定义，`LargeImage="True"` 或 `"False"` 控制该处布局。系统文件也有省略此属性的引用；生成器应显式写出。
- `Tab` 和 `Group` 均有 `Caption`。普通 Tab 样本使用 `ContextualTabGroup/Item=None`。

## 证据边界

上述证据支持“可以按参数生成同结构 XML”的可行性判断，不能代替完整厂商 Schema 或 AVEVA 加载验收。文件名 YtoolsAM 不证明其已经在所有 AM/PDMS/E3D 版本通过验证。

当前证据仅显示一个按钮 `Image` 字段和布局中的 LargeImage 选择，尚未确认独立 large/small 两份图片的序列化字段。首版按“一份图标 + Large/Small 布局”实现；若需要两个独立图片槽，先取得对应原生导出样本再扩展。

后续验收需确认：独立 UIC 的加载入口、Namespace 与 Name 的合并行为、图片格式及尺寸、大小图标效果、Tooltip 显示、Macro 点击行为。首版输出不包含系统 design.uic 内容，不修改 AVEVA 安装文件。

## v0.2 实现补充

编辑项目保存一份 32×32 PNG 主图，导出时按 Large/Small 引用生成 32×32 或 16×16。混合大小复用通过生成额外 ButtonTool 处理，不假定存在未验证的双 Image 字段。

已读取本机 E3D 3.1 与 Marine 12.1 SP4 的 DesignCustomization.xml，均通过 UICustomizationSet/UICustomizationFiles/CustomizationFile 登记 UIC。部署入口按此结构最小插入，并保留其他条目。真实安装本轮仅只读预览，写入测试限模拟目录。详见 deployment.md。
