# 功能与 Script 目录维护

查阅从[按功能阅读](../../artifacts/docfx/navigation/features/index.md)或[Script 目录](../../artifacts/docfx/navigation/scripts/index.md)开始；人工维护从仓库文件开始。自动生成的页面保留源码、用途、归属、调用树和测试入口，源文件与人工说明各有一个权威位置。

## 哪些内容自动寻找

| 输入 | 自动处理 | 需要人工决定 |
| --- | --- | --- |
| PMLLIB、PMLUI、tests/ai-pml | 递归发现 PMLFNC/PMLOBJ/PMLFRM/PMLCMD，读取可识别声明与真实路径；Obsolete 标为历史 | 用途、功能归属、变更说明、是否迁移或执行 |
| 现有 C# 项目与链接源码 | 复用 tools/Documentation 的 Roslyn 声明清单，提供类型、方法、签名与行号 | 哪些类型或唯一方法是 Script，属于什么功能 |
| 人工清单 | 校验重复 ID、未知类型/入口、功能关联、说明文件和路径，再生成聚合页 | 对用途、来源与实现状态的正确性负责 |
| 既有 test-map 与 UI 溯源 | 复用测试关联和可用的 C# 调用树 | 测试目的及关联需要核对，不按可达性推断覆盖 |

C# 不把所有 class 都称为 Script。未认定且包含方法的类型出现在[C# 待分类候选](../../artifacts/docfx/navigation/scripts/candidates.md)中，点源码检查职责后再登记。框架、ViewModel、DTO 和适配器不能只因名称相似就自动变成业务脚本。

PML 只有文本声明识别；有声明不等于语法有效、目标环境支持或本次执行通过。tests/ai-pml 的自动条目显示 draft，用途未知或未归属会明确保留。

## 去哪里编辑

| 要改什么 | 编辑位置 | 不手工维护的内容 |
| --- | --- | --- |
| 函数实现、参数与返回值 | 对应 C#/PML/PowerShell 源文件 | 生成页上的签名副本 |
| 功能名称、目的、状态、类型入口 | `docs/reference/capabilities.json` 的 features | 生成的功能详情、目录和关联数量 |
| Script 归属、用途、短变更摘要 | 同文件的 scripts | 自动源码路径、声明、SHA256、测试结果 |
| 使用例子、错误解释、人工操作 | 普通 Markdown；通过 guide 关联 | artifacts 下生成页 |
| 测试目的、API/PML 关联和人工案例 | 现有 `docs/reference/test-map.json` 与测试源码 | 另一套测试状态或覆盖率 |

浏览器当前负责查找和阅读；增删改通过编辑器和 Git 完成。网页写回、人工批准与迁移属于后续管理功能。

## 先检查再生成

在仓库根目录运行，需要 .NET 10 SDK、Node.js 和 PowerShell 7：

```powershell
# 刷新 Roslyn 源码清单并校验人工目录
npm run docs:catalog:check

# 在终端查询，也可在浏览器筛选
node scripts/documentation-catalog.mjs list --q YuzuhaGetGadget
node scripts/documentation-catalog.mjs list --feature forms --language "C#"
node scripts/documentation-catalog.mjs list --kind candidates --q Array
node scripts/documentation-catalog.mjs list --q FileBrowseTest --history

# 完整生成站点并检查源码、测试和页面链接
npm run docs:build
```

check 和 list 都会刷新声明，不以旧清单冒充当前源码。完整构建额外生成 API、测试和调用树。缺少 AVEVA SDK 时，Host 签名参考仍可读，完整 Host 调用树按既有机制显示未分析边界；可用 `Build-Documentation.ps1` 的两个 SDK 参数补充真实编译输入。

## 增加功能

先在 features 添加一个唯一 id，再建立普通 Markdown 说明。以下是现有 forms 功能的最小结构，已有同 id 时编辑原条目，不重复添加：

```json
{
  "id": "forms",
  "name": "原生 Form 与 Gadget 查询",
  "summary": "复用 FMSYS/PMLFORMS 查询已有窗体和控件。",
  "status": "implemented",
  "origin": "user",
  "guide": "docs/reference/form-gadget-query.md",
  "types": ["YuzuhaToolkit.Mcp.Core::YuzuhaToolkit.Mcp.PmlFormTools"]
}
```

status 支持 implemented、partial、planned，仅表示实现范围。origin 支持 user、existing、ai，记录需求来源。types 是当前源码索引中的 `项目::类型UID`，不是拟议接口名；尚未实现时用空数组，并在说明中列出验收目标。

## 增加 Script

**原生 PML：** 把源文件放入固定扫描目录。重新构建后即使没有人工条目，也会出现在 Script 目录，显示自动发现和未归属。AI 草稿放独立 tests/ai-pml 子目录；此操作不把文件加入生产加载路径。

要补人工解释，在 scripts 添加或编辑文件选择条目。文件必须真实存在：

```json
{
  "file": "PMLLIB/Traversal/YuzuhaReadCurrentElement.pmlfnc",
  "features": ["objects"],
  "purpose": "复用已有遍历器读取当前元素。",
  "guide": "docs/reference/desktop-m1.md"
}
```

未指定 id 时，原生文件 ID 为 `pml:仓库相对路径`。需要跨移动保留身份时，可维护显式 id，并同步源码路径及相关引用；不会按同名文件自动推断迁移。

**C#：** 先从待分类候选选择实际类型，再在 scripts 登记。以下是现有有界数组脚本的选择方式：

```json
{
  "id": "SCR-CS-ARRAY-WINDOW",
  "type": "YuzuhaToolkit.Mcp.Core::YuzuhaToolkit.Pml.PmlArrayWindow",
  "entry": "Read",
  "features": ["arrays"],
  "role": "algorithm",
  "purpose": "限制数组窗口的索引、槽位和返回字符。",
  "guide": "docs/reference/pmltwin-phase1.md"
}
```

type 与 file 二选一。entry 可省略，此时展示该类型的方法；填写时必须命中唯一实际方法，有重载就填写源码清单中的完整 signature。共享源码编译到多个框架时，各 `项目::类型UID` 不混为一个实现；可在功能中聚合。

构建或安装类脚本可使用真实 file、显式 id 和 role=build 登记。登记脚本不会修改业务协议、反射装载、调用许可或执行时机。可使用 algorithm、runtime、adapter、build、native、prototype、draft 等职责描述；职责标签不能替代实际依赖检查。

## 修改已有内容

修改目的或功能归属时编辑原条目，保留稳定 id；源代码签名由自动清单更新。增加一段详细解释时，新建或修改 guide 指向的 Markdown。实质行为变化用 change 写两到四句，再链接功能说明中的版本、差异及验证报告。

修改实现先补测试再实现。更名或删除 C# 入口后，旧 selector 会使检查失败，必须更新人工选择；不会按近似名称补链。该检查定位身份和引用，不评判说明是否符合真实业务，仍需审阅。

## 删除与废弃

| 操作 | 实际结果 |
| --- | --- |
| 删除原生 Script 的人工条目 | 只移除用途与归属补充；真实源文件仍自动出现，不会被隐藏 |
| 删除 C# Script 登记 | 从正式 Script 清单移出；仍存在的类型回到待分类候选 |
| 从功能移除一个 Script | 修改其 features 数组，不删除源文件，也不撤销运行时许可 |
| 删除一个功能 | 先处理引用该功能的 scripts，再删 features 条目；遗留引用检查失败 |
| 保留历史原生实现 | Obsolete 文件仍在目录中，默认筛选隐藏历史，可勾选查看 |
| 真正删除源文件 | 通过 Git 删除并同步人工清单、test-map 和其他引用；失效引用会失败 |

源码删除与功能弃用应说明替代入口及影响。修改 PMLLIB/PMLUI 仍遵守当前原生文件边界，不因为目录维护附带修改。

## 如何查和验收

浏览器可按名称、ID、符号、用途、路径、功能、语言和历史状态筛选；候选类型有独立入口。无结果会显示空状态。每个 Script 详情能回到功能、源码快照与实际行、可用调用树、测试及人工编辑位置。没有维护的用途或测试不自动补成成功。

类型包含多个实际方法时，调用树入口先列出方法供选择；只有一个对应方法时直接打开。对应使用真实类型、文件和源码行，不用隐式构造函数或同名方法替代。未分析或无静态对应时明确说明。

生成内容位于 `artifacts/docfx/navigation/scripts` 和 `features`，页面发布到 `artifacts/docfx/site/reference`。清单 schemaVersion 当前为 1；未知字段、重复 ID/来源选择、失效文件/类型/入口/功能或 guide 使构建失败。不要在清单添加 reviewed=true 或 passed=true，审核与运行证据继续通过真实报告核对。
