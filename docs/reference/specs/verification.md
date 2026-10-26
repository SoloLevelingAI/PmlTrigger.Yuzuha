# 验证

本轮对象：正式 WPF 保留页面、真实日志筛选、代码回归、DocFX 页面布局。AVEVA 执行及首次用户验收另记。

[本轮结果](../../../artifacts/docfx/navigation/specifications/data/verification.json) · [压缩数据](../../../artifacts/docfx/navigation/specifications/data/metrics.json)

```powershell
pwsh -NoProfile -File scripts/Build-Documentation.ps1
python scripts/test-handoff-browser.py
```

浏览器检查覆盖各页面类型、用户所示源码页及全部生成源码页；不同宽度分别留图。WPF 用隔离目录和实际控件验证，合成数据不算平台实测。

长路径知识建库失败保留原记录，转[待定](../features/planned-tools.md)。旧浏览器演示的50项检查不计入本轮。
