# Core30 浅色安装器皮肤

Phayton 普通安装使用同屏目录、Apache 2.0 协议和主按钮。自绘标题区使用 Windows 的原生移动循环，窗口圆角由桌面合成器处理。覆盖确认和协议窗口从属于安装主窗口。

`Core30Light.Tokens.iss` 将 Core30 的语义颜色转换为 Inno Setup 使用的颜色格式，同时保留字体、间距和圆角变量。它与 HTML、Penpot 的转换都使用同一份 `design-tokens.json`。在工作区根目录执行以下命令可刷新变量。

```powershell
node .\PmlTrigger.Yuzuha\installer\theme\build-tokens.cjs .\outputs\Core30-Product-UX\design-tokens.json
```

将 `theme` 目录放在对应安装脚本旁，在脚本的 `[Code]` 段之前包含 `theme\Core30Light.iss`。主题通过 `Core30Frame.dll` 绘制真实窗口的边框和按钮。该文件随向导临时加载。可用带 x86 C 编译器的 Visual Studio Build Tools 运行 `theme\build-frame.ps1` 重建。

在 `InitializeWizard` 中先调用 `Core30InitializeLightSkin(产品名称, 标题, 版本, 是否使用单页安装)`，再创建业务页。在已有的 `CurPageChanged` 中调用 `Core30LightPageChanged(CurPageID)` 更新当前页的控件。主题资源目录可通过 `Core30SkinAssetDir` 指定，协议正文读取安装脚本上一级的 `LICENSE`。

`ThemePreview.iss` 可单独用 Inno Setup 6.7 的 ISCC 编译。双击查看单页界面，也能用 `/CONFIGUREAVEVA=1` 查看示例接入选项与计划。正式包从 `Yuzuha.iss` 和已校验的打包目录编译。
