# CI 视口失败与契约演示交接

2026-10-31，承接 PR #13 / 1ac975f。上轮只验证了本机成功后推送，没有拿到云端完成证据；本轮先确认具体失败，再修复和补足证据契约。

## 已确认的失败

[development-quality 41202203985](https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/actions/runs/41202203985) 的 xUnit、DocFX 全部成功，WPF isolated flows 失败；同期 build 和 Phayton command regression 成功。下载 artifact 13042548945，smoke-error 为“1280×720 首页摘要不应要求纵向滚动”。其 overview-1280.png 实际1044×720。测试只设置 Window.Width，云端桌面限制了 HWND；文件名/静态receipt仍声称1280。无滚动断言没有错，尺寸前提未验证。

本机按CI的 run-documented-test 入口复验又发现嵌套报告路径加32位随机状态目录导致SQLite WAL打开失败；测试专用状态路径现缩为 s/12位随机值，保持隔离，不改变生产数据目录。

## 修复契约

- 显式设置实际工作区画布的 DIP 尺寸；响应布局依据 WindowSurface 实际尺寸。
- smoke 故意将宿主限制1024×768；所有118截图覆盖主功能、设计图、展开态、窄窗、错误态，必须在该宿主下完成。
- WorkspaceSurface 正常运行仍使用默认Grid布局裁剪；只有隔离smoke取消宿主的layout clip，子控件的裁剪、滚动和权限保持。局部VisualBrush渲染避免父窗口偏移。
- 每张PNG验证请求尺寸、actual尺寸和IHDR像素相等；截图四角必须不透明，拒绝“正确尺寸的裁剪空图”。最初仅设置尺寸的版本被目检发现裁剪，随后新断言也真实拒绝过中间实现。
- 保留1280×720摘要无滚动、五主入口、全部12功能视图、草稿保存、双主题和4错误状态断言。118张非空图片不能替代各项业务断言。
- receipt区分renderDpi=96与hostDpi，不再把853×480逻辑画布描述为真实150%缩放验收。

## HTML 演示

入口 docs/presentation/contracts/index.html。21页离线演示，围绕整体职责、Feature输入/状态/失败、Script算法和副作用、回执证据展开；附24条需求原文、Feature/Script索引与源码链接。每页一个主张，细节按需展开。原始状态保持，不把planned/partial升级为完成。

独立事实审阅纠正了两项：C# Preview冻结/重验ManifestHash，PowerShell只读清单并冻结文件计划，保证不能混同；DBREF部分错误返回空数组且可能临时切换CE，不能概括成所有查询都无副作用且错误完全可辨。

## 验证与接手

本机严格documented desktop runner在1024×768宿主下通过118截图尺寸/透明边缘检查，原12视图断言全部保留。原始证据 artifacts/dt/33e198fcbab08788/ccc7d6a19fae4416aa1b9acdc56fab42；截图已目检1280摘要完整。

开发/文档最终回归、HTML多尺寸检查和推送CI证据另存本Feature的验收摘要。未执行APS/AVEVA实机，当前修复不涉及Host/PML业务协议。原先本机通过记录保持历史范围，不能作为上轮云端通过证据。

源码：MainWindow.SmokeViewport.xaml.cs设置画布；WorkspaceSurface.cs仅为隔离渲染处理根裁剪；MainWindow.xaml.cs采样/回执和布局；Test-Desktop.ps1逐图核验；App.xaml.cs测试隔离路径。均在src/YuzuhaToolkit.Desktop或scripts。后续新增截图须同时更新明确覆盖数量与尺寸契约，不应删除断言让CI变绿。
