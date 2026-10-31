## Boundaries
apps/desktop 是 WPF HTTP 客户端，apps/web 是网页客户端，apps/backend 是独立 C# HTTP 服务。packages/contracts-csharp/RibbonContracts.cs 是两端共同编译的传输模型，无 WPF 或服务依赖；版本显示、模块勾选能力等显示属性位于桌面 DisplayModels.cs。

## Feature descriptions
| Feature | 描述（每项不超过八句） |
| --- | --- |
| Projects | 验证项目形状、ID、命令键与引用，规范化缺失值。前端只提供操作入口。 |
| Images | 验证 PNG/JPEG 后保持比例输出 32/16 图标。前端只选取和显示。 |
| Uic | 安全解析与合并 XML，保留额外属性、隐藏节点和未编辑文本。前端通过 HTTP 加载或下载。 |
| Workspace | 兼容现有文档与版本 JSON，保留五次草稿及长期快照。只在服务中落盘。 |
| Deployment | 预览摘要、复核路径及变更、备份和写入、登记模块。前端穿梭框和确认不直接写 AVEVA。 |
| Management | 区分托管、自定义、保护与未知项，显式接管并管理加载。原生/未知保持只读。 |
| Layout/Editing | 结构与 Ribbon 是同一 DTO 的可视化编辑，撤销只记录局部对象及前后位置/值。没有后端 UI。 |

## Implementation policy
简单流程用直接方法，构建/启动/发现/边界检查用脚本。共用 XML、编码、原子文件写入、增量操作集中为小组件，不建立没有第二实现的接口或工厂。委托主要用于事件、路由和操作连接，不引入反射扫描、动态代理或复杂继承。基础设施选择 .NET Framework 4.8 的现成 HttpListener/HttpClient、XML、JSON 和 GDI+；Windows 原子重命名集中在基础设施中。保持已有稳定运行时，SDK 仅用于格式化，不让已安装的预览 SDK 成为应用依赖。

## Incremental history
EditOperation 保存 Undo/Redo 动作，事务将一次用户操作合并为一步。位置操作保留列表与索引，增删保留受影响对象，属性保留前后值。复制只复制所选子树及依赖按钮，不复制原 UIC。保存规范化只更新现有对象字段，因此历史中的引用持续有效；异步保存只更新状态，不重新创建树和预览。文字局部刷新，图像冻结缓存，按 ID 建立渲染查找索引。50 步限制继续有效；不再为每一步保存整个项目或原 UIC XML。

## Navigation
项目 Tab 条移至设计页上方，使用现有 Office Tab 状态，提供＋入口。树与 Tab 点击选择同步；实际 Ribbon 区只展示当前 Tab 的 Group。工具组的大按钮与小按钮列之间增加分隔。

## Compatibility and validation
保留 JSON、路径、版本序号、原文件覆盖、共享 UIC 引用及模块登记契约。C# API 契约测试和隔离 WPF 回归验证迁移，旧 TS 核心作为参考验证。性能测试将一万按钮装入模型，连续记录 200 次属性变更；只统计增量记账，不包含重绘或网络。真实输入、任意 DPI 与 AVEVA 实机加载仍需人工验收。
