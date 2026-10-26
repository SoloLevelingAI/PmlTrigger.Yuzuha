# 前端 SDS · 状态

| 状态 | 约束 |
| --- | --- |
| snapshot / revision | 服务返回；保存/恢复携带读取修订 |
| draft / dirty | 与快照隔离；离开确认；冲突保留 |
| pendingRestore | 绑定配置、历史及当前修订 |
| queryKey / continuation | 目标/条件/预算变更即失效 |
| result / runFilter / correlation | 结果与记录独立，可按编号互查 |

成功提交才替换快照；冲突重读后比较。凭据不写历史。分派前拒绝=未执行；分派后超时/断线=可能已执行，不自动重试。易变配置显示作用域、生效时机、校验及恢复入口。

签名从 [Roslyn](../../../../artifacts/docfx/navigation/specifications/bindings.md)生成。
