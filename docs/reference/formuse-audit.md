# Gadget / FormUse 支持登记

基线为 `codex/yuzuha-native-workspace` 的 `5d73e1dc5d1dc648f57cef16771938830a87671b`。以下结论来自源码检查；当前阶段没有执行 AVEVA 实机案例。原生 PMLLIB / PMLUI 保持不变。新增的 [C# Form/Gadget 查询](form-gadget-query.md)复用 PMLFORMS / FMSYS 的读取路径，不修改或依赖这里的装饰原型。

| 入口 | 已有能力 | 缺失 / 限制 | 复刻 ID |
| --- | --- | --- | --- |
| YuzuhaGetGadget | 枚举 form.attributes，识别已设置 GADGET，按类型排除 | 依赖显示字符串识别；未证明跨版本、递归子控件或 .NET 容器完整性 | manual:formuse-get-gadget |
| YuzuhaGetGadgetType | 从 Q VAR 输出方括号解析类型，错误返回空串 | 无结构化失败原因，未知类型不区分 unset / 格式变化 | manual:formuse-get-gadget-type |
| YuzuhaDecoratedCall | 保存回调、替换 !this、改写 callback 与标题 | `*Gadget*` 防重判断宽泛；无精确恢复入口、无生命周期清理 | manual:formuse-decorated-call |
| YuzuhaGadgetDecorated | BeginLog/EndLog、回调调用、尝试保留返回值 | 循环覆盖触发 gadgetStr，复用 Before 时间，After 未重采样；不检查记录返回值；异常与无返回值混淆；CONTAINER 内部不支持 | manual:formuse-gadget-decorated |
| YuzuhaChooseFormToTrack | FMSYS.SHOWNFORMS、刷新、多选装饰、原生 KILL/SHOW 重置 | hide 方法为空；DeActivate 对所有 shown forms 重建，非无损解除；没有单控件恢复 | manual:formuse-choose-form |

每个 ID 均可通过 `scripts/run-documented-test.ps1 -TestId '<ID>'` 核验源码并显示 `tests/manual/formuse.md`，返回 NotRun（退出码 2），不会擅自操作活动工程。

`docs/validation/2026-09-19-e3d.md`仅是历史记录。它记录过选择器显示、枚举和刷新成功，也登记了数组 RPC 查询失败、Unknown 模块身份和上述原型问题；不将其移植为本次通过状态。xUnit、原生 AOT 协议、NET35/NET48 夹具与 AVEVA 实机验收分别记录。
