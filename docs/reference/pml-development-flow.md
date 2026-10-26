# PML 开发与加载

任务入口见[工作台](workspace.md)；AI 草稿放 `tests/ai-pml/<任务>/`，契约用 XML 注释，接口签名由 DocFX 提取。

| 顺序 | 操作与核验 |
| --- | --- |
| 1 | 发现并核验会话，确认实际 PMLLIB，不以目录选择代替搜索路径验证 |
| 2 | 预览源/目标哈希、文件差异和备份；任一端变化拒绝旧计划 |
| 3 | 备份复制，按清单 REHASH ALL，再按类型重载 Object / Form |
| 4 | 新实例/构造版本及验证宏核对实际值；加载回执不等于行为正确 |
| 5 | 保存输入、预期、回读、错误和清理记录；失败/未知停止，不自动重发 |

桌面“部署”提供复制并验证；命令行复用 `scripts/Invoke-PmlDevelopment.ps1` / `PmlDevelopment.psm1`。样例：`tests/ai-pml/development-probe` 与 `tests/live/pml-development/manifest.json`。不要求 PMLCMD，不改 Host 协议；备份和原始模型证据留本机。历史 E3D 夹具结果不替代当前环境重测。

Form 重载引用带 `!!`，Object 用类型名；不必每次全量重载。普通复制按钮仍只复制。

简单输入/原生拾取用 PMLFRM；复杂表格/筛选/导出可用[本地 HTML 管理器](html-app-manager.md)，经 C# 桥复用现有 IPC。浏览器不能直接连命名管道；当前无 WebView2 依赖。

当前[Form/Gadget 查询](form-gadget-query.md)仅列已设置 GADGET，固定读取 shown/active/visible/tooltip/callback；callback 只读，不模拟点击。MENU、FMPROP、普通成员、全部属性与嵌套展开尚待扩展。逐项区分成功、unset、不支持、失败；数组保留真实下标、预算、续页与截断，原生未暴露内容不假造为空。
