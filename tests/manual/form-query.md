# 原生 Form / Gadget 查询人工验收

完整人工验收默认保持 **NotRun / 未完整验收**。局部实机合成 Smoke 的具体结果单独记录在 `docs/validation/2026-10-21-form-gadget-query.md`，不能覆盖所有步骤或所有产品。离线 xUnit、合成 NET35/NET48 RPC 与 Host 编译结果也不能代替这里的 AVEVA 实机结果。

可执行的局部复刻脚本和自有 PMLFNC/PMLFRM 见 `tests/live/form-query/README.md`。先核验源码与部署文件，再显式选择实际 PID；测试输出留本机 artifacts，不保存工程。

使用独立测试工程，分别对 NET35 Marine/PDMS 和 NET48 E3D 记录产品版本、模块、提交、Host DLL SHA256、进程 PID/启动时间、UTC 和 RequestId。先按正式安装流程加载匹配 Host；不在当前工程自动替换 DLL。通过会话发现选择实际 PID 并核验身份。输出只保存在本机 `artifacts/manual/<测试ID>/<UTC>/`，不上传工程、截图或真实控件文本。

## manual:aveva-form-query

1. 用用户选定的测试窗体建立“已加载且显示”和“已加载但隐藏”两种状态。通过原生 PMLFORMS 的 Only shown form 开关分别查看两个清单；先完成这些人工准备，再记录基线，避免把打开 PMLFORMS 自身带来的变化误认为查询副作用。
2. 在已显式选择的同一会话调用 `list_pml_forms(shownOnly=true,maxItems=2)` 和 `shownOnly=false`，分别对照 `!!FMSYS.SHOWNFORMS()` / `!!FMSYS.FORMS()`，核对名称与原生引用。用实际返回的名字继续后续控件测试。
3. 逐页使用 NextIndex，直到 0；核对 Scanned、IndexUpperBound、Truncated。缩小字符预算，确认完整项、正确续读或首项超预算失败。分页间不主动改变窗体；另做一次有意改变窗体的实验，确认文档没有承诺跨请求固定快照。
4. 查询前后对照显示状态、控件值与回调文本：不应 Show/Hide/Kill/Load/Refresh 或改写回调。查询临时变量不应残留。不对原生管理器执行任何修改命令。
5. 未选择会话、PID/启动时间不符、未升级的 Host 应明确失败；旧 Host 不执行原始查询回退。保存响应与 RequestId。仅在独立测试会话中安排旧/新 Host 的人工切换，不改生产会话。

预期：原生清单一致，预算和续读正确，没有生命周期副作用。任一必要查询失败应记录 Failed；无法运行或没有准确基线应记录 NotRun，不能根据历史报告通过。

## manual:aveva-gadget-query

1. 在原生 PMLFORMS 选定已加载的合成测试窗体，包含按钮、文本、列表、普通 STRING/ARRAY 成员、MENU；若产品支持，再包含 CONTAINER。记录原生属性与类型，不调用 YuzuhaDecoratedCall/GadgetDecorated，也不 Activate 选择器。
2. 对实际窗体调用 `list_pml_gadgets(formName=<实际名称>,maxItems=2)`。逐页对照 `.attributes()`、`.attribute(属性名).objectType()`；只列 GADGET，普通成员和 MENU 不列出，过滤项仍占扫描预算。列表 Type 应对应原生 `.Type()`。
3. 对按钮、文本、列表和可用的 CONTAINER 分别调用 `get_pml_gadget`。核对 Type、shown、active、visible、tooltip、callback。产品不支持的属性应为 unavailable、Value=null 和可定位错误；不应被当成 false 或完整支持。
4. 在合成回调里预先设置可观察的计数器，仅记录基线，不触发它。查询详情后计数必须不变；callback 只返回文本。对照控件 val、显示和激活状态不变。CONTAINER 只返回原生 GADGET 元数据，不承诺内部 WPF 控件树。
5. 查询不存在的窗体/控件、普通成员、非 FORM 全局，以及 `!!前缀`、点路径、换行和引号输入，确认失败且没有命令注入。使用较小字符预算验证超预算失败与临时变量清理。

预期：已支持的原生信息正确、失败可区分、回调不执行且窗体状态不变。按属性和产品分别记录实际支持；部分 unavailable 不等于全功能通过。保存工具响应、必要的本机截图与 RequestId，无法验证的项明确 NotRun。
