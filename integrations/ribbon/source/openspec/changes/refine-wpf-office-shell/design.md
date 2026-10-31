# Design

OfficeRibbon.xaml 保留原设计资源；StudioTheme.xaml 重写 Button、ToggleButton、TextBox、ComboBox、CheckBox、Tab、TreeView、DataGrid、ContextMenu、ToolTip、Expander、ScrollBar 模板，统一 Office 色板和间距。按钮使用标准 CommonStates，原生鼠标路径与渲染测试共用同一状态定义。

Chrome.Install 将现有内容包入窗口外壳，主窗使用已有蓝色标题栏；其他自有窗口新增同款标题栏。WindowStyle=None，AllowsTransparency=false，WindowChrome 提供 6px 调整边界、38px 标题区和原生非客户区行为。Caption 控件显式启用 chrome 区命中。操作繁忙时只禁用业务内容，窗口按钮保留响应；原部署的 Closing 防护继续生效。

菜单按右键命中的对象选择，树和预览同步选中但不重建菜单所属视觉对象。按钮副本重新分配 ID、清空 sourceId 与命令 Key；Group/Tab 副本重新创建节点和按钮映射，避免回写覆盖原按钮。只读加载条目不提供可执行编辑/移除；已有模块仍禁止在加入页撤销，支持复制信息。

输入框使用各自独立的原生 ApplicationCommands 菜单，遵循只读与选区状态。确认、错误和输入弹窗使用同款无边框 WPF 窗口，保留 Enter/Esc；系统文件选择器不被替换。

ThemeVerification 在隔离回归中检查模板状态实际渲染值、下拉弹层、窗口配置、右键复制命令与大小切换，输出原生渲染图。此验证不等于外部鼠标键盘、系统 Snap 或任意 DPI 的人工验收。
