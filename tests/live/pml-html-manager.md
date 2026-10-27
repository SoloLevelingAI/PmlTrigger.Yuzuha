# HTML 应用管理器实机复刻

## manual:pml-html-manager

验证 HTML 分类、绑定和参数进入现有 IPC，读取大 ELDATA，展示、筛选和导出结果。独立夹具不改现有调用，不注册 PMLCMD，不保存模型。

1. 按前批开发链部署 `tests/ai-pml/development-probe`，复用其清单。
2. 确认新版 `PhaytonObjectWalker` 已部署、重载。
3. 使用 `scripts/Invoke-PmlDevelopment.ps1` 部署 `tests/ai-pml/html-manager`，清单为 `tests/live/pml-html-manager-manifest.json`。准确 PID 和 PMLLIB 从现场发现及查询取得。
4. 构建后从工作台“Web 应用”打开管理器（C# HtmlAppLauncher），地址为 `http://127.0.0.1:5288`，明确选择会话。
5. 网页新增分类，修改按钮及默认值，登记原生 Form；重启核对保存。属性示例填写自有 DBREF、ELDATA、下标 0。
6. 核对来源 VAR SPLIT、总数/输出数及截断状态。搜索末项，导出 CSV；首、中、尾定向结果与完整读取逐项比较。
7. 函数参数验证 `12.5+7.5=20`；Form 按钮打开 `YuzuhaDevProbeForm`，通过原生窗口确认可见。

API/IPC 复刻脚本执行真实 PML，仅用于已授权会话。仓库不预置现场 DBREF：

```powershell
pwsh -NoProfile -File tests/live/pml-html-manager.ps1 `
  -ProcessId $avevaProcessId -ElementRef $fixtureRef `
  -OutputFile artifacts/manual/html-manager/run.json
```

预期：完整有序下标；首中尾单项数量 1 且值一致；预算 5 时总提取量不变并提示截断；函数返回 20，Form 返回派发回执。重复请求保留原 IPC RequestId；无令牌/跨来源为 403，旧绑定为 409，畸形 DBREF 为 400。失败或未知不重发。

E3D 2.1 / NET48 结果见[实测报告](../../docs/validation/2026-10-23-html-app-manager.md)。其他产品版本未实测。DBREF、模型值、原始记录和截图只存本机。
