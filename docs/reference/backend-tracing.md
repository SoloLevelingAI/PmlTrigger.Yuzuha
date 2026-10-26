# 后端溯源

[界面入口](ui-tracing.md) → [静态调用图](../../artifacts/docfx/navigation/ui-trace/index.md) → [源码绑定](../../artifacts/docfx/navigation/specifications/bindings.md) → [测试](testing.md)。

Roslyn 使用真实编译输入；接口边为候选，非运行选择。Host 缺 SDK 时仅声明；PML/平台内部单列边界。

| 现象 | 核查 |
| --- | --- |
| 分派前拒绝 | PID/启动时间/模块、协议、权限、预算 |
| 空页/缺属性 | Success、Scanned、NextIndex、原生原因；空≠false/0/失败 |
| 超时/断线 | mayHaveExecuted；回执与模型，禁止自动重发 |
| 日志故障 | 记录结果与业务结果分开，不把已执行改失败 |

传输 RequestId、Host 回执和 RuntimeJournal 编号未全程统一；保留各编号，不编造完整运行轨迹。数组/字符预算限制扫描与返回体积，不限制原生枚举瞬时分配；首项超限需调预算，不能无限重试空页。
