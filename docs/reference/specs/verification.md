# 验证

10-27：启动/凭据分离、纯数据与旧PowerShell清理。代码232、文档50、原生CLI JSON/TOML各15步及WPF隔离测试通过。详见latestFollowUp；保留分轮旧证据，无新增AVEVA实测。

[本轮结果](../../../artifacts/docfx/navigation/specifications/data/verification.json) · [压缩数据](../../../artifacts/docfx/navigation/specifications/data/metrics.json)

```powershell
pwsh -NoProfile -File scripts/Build-Documentation.ps1
python scripts/test-handoff-browser.py
```

浏览器检查覆盖各页面类型、用户所示源码页及全部生成源码页；不同宽度分别留图。WPF 用隔离目录和实际控件验证，合成数据不算平台实测。

长路径知识建库失败保留原记录，转[待定](../features/planned-tools.md)。
