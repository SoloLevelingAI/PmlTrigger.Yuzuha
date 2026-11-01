# 独立视觉候选：本地阅读与浏览器验证

在仓库根运行：

```powershell
npm ci
npm run contracts:browsers:install
npm run contracts:styles:serve
```

打开终端显示的本机19440地址。ION、PRISM和BENCH共用经过校验的公共内容，页面不执行工程动作。内部方法需要先显式运行 `npm run contracts:methods:capture` 并通过图谱共同生成服务发布；没有捕获或发布时显示不可用，不把历史证据当当前源码。

浏览器测试使用 `package-lock.json` 锁定的根 Playwright 包，安装命令下载相匹配的受管 Chromium；不会使用个人路径、固定缓存 revision 或被忽略的 `artifacts/**/node_modules`。使用已安装浏览器时可设置 `CONTRACT_ATLAS_CHROMIUM_EXE`；使用已安装 Edge 可显式设置 `CONTRACT_ATLAS_BROWSER_CHANNEL=msedge`。缺少浏览器时给出安装命令并失败，测试本身不自动下载。

服务运行后，可执行：

```powershell
npm run contracts:styles:browser
npm run contracts:styles:inline
npm run contracts:styles:breadcrumbs
node tests/contract-style-methods-browser.cjs
```

后三种真实页面回归仍需相应的有效共同方法修订；隔离方法组件测试使用合成夹具，不替代Roslyn/DocFX或产品实机证据。FigCraft独立构建需要其README声明的真实CLI；浏览器依赖准备不会安装或启动工程宿主。
