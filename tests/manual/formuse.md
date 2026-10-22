# FormUse 人工案例（v0.4 第一阶段）

所有案例当前均为 **NotRun / 未实测**。先在目标 AVEVA 的测试工程确认产品版本、Host Framework、DLL 哈希、提交、模块、PID 和启动时间。通过现有会话发现/选择流程核验目标；不按名称猜 PID。PMLFORMS / FMSYS 是原生窗体管理来源。本阶段不替换或修改 PMLLIB/PMLUI 文件。

每次记录测试 ID、UTC 时间、环境、实际命令、预期/实际、Host RequestId/Code/ErrorMessage；将 JSONL 与截图保留在本机 artifacts/manual/<测试ID>/<运行编号>，记录文件 SHA256。RPC Success 只证明调度，不能证明回调、布尔返回或记录成功。没有实际证据时保持 NotRun。超时后不自动重试。

## manual:formuse-get-gadget

1. 用原生 PMLFORMS 打开用户选定的无模型副作用测试窗体，记录 `!!FMSYS.SHOWNFORMS()` 返回的实际名称。
2. 在 PML 命令窗中将实际窗体对象传给 `!!YuzuhaGetGadget(实际窗体,||)`；记录结果和 `实际窗体.attributes()` 对照，逐个确认已设置 GADGET。
3. 用 `|'BUTTON','FRAME'|` 再调用，确认被排除类型不在结果；用包含 unset 控件的测试窗体验证跳过行为。
4. 预期：返回实际控件名称，不包含排除类型；只证明此窗体/产品版本。嵌套 .NET 控件不声称完整覆盖。

## manual:formuse-get-gadget-type

1. 选取实际 BUTTON、TEXT、LIST、FRAME、CONTAINER 及 unset 控件，各调用一次 `!!YuzuhaGetGadgetType(实际控件)`。
2. 对照原生 `Q VAR` 输出中的方括号类型；记录空字符串及异常分支。
3. 预期：可识别控件返回对应类型；不能识别时返回空字符串。不把字符串格式解析当作跨版本兼容保证。

## manual:formuse-decorated-call

1. 在测试窗体中选一个无模型副作用的 callback，先保存 callback、FORMTITLE 和窗口状态。
2. 调用 `!!YuzuhaDecoratedCall(实际控件)`，核对保存的回调内容和 `!!YuzuhaDecoratedCallBack` 索引，确认 `!this` 替换结果。
3. 再装饰一次，核对没有重复包装；另检查原回调含 `Gadget` 字样的情况。当前宽泛匹配可能错误跳过，登记实际结果。
4. 预期：原回调可追溯、一次调用一次回调。此操作会改变运行期回调和标题；结束后用原生窗体管理恢复，仅在确认可以丢弃窗体状态时重开。

## manual:formuse-gadget-decorated

1. 使用上个案例的测试窗体，分别准备返回值、无返回值、明确失败的无副作用回调；用户点击一次并保存前后证据。
2. 检查 BeginLog 返回 ID、EndLog 返回值、GetLastLogError 和 JSONL；关联 RequestId、记录 ID、相位、截图文件。
3. 核对真实触发控件、前后时间、返回值与执行次数。当前源码复用 Before 时间、循环覆盖 gadgetStr，且未重新采样 After 控件值，分别记录为限制。
4. 预期：不从 RPC Success 或 Later 文本推断回调成功；记录失败和回调失败分别登记，CONTAINER 不支持内部状态。

## manual:formuse-choose-form

1. `show !!YuzuhaChooseFormToTrack`；把列表与 `!!FMSYS.SHOWNFORMS()` 对照，使用 Refresh 后再次核对。
2. 仅选择可丢弃状态的测试窗体后 Activate，核对被装饰的控件；PMLFORMS、GPH3DDESIGN 和选择器本身应被排除。
3. 点击 Cancel 并记录实际结果：当前 `.hide()` 为空，不能预设按钮会隐藏。使用显式 `hide !!YuzuhaChooseFormToTrack` 清理。
4. DeActivate All 会 KILL/SHOW 所有列出的目标，可能丢失窗体未保存状态；只在独立测试会话且明确可以重置所有目标时验证。预期按原生重建后的状态核对，不声明支持无损解除装饰。

## manual:aveva-bounded-array

1. 在对应 NET35/NET48 新 Host 中验证功能声明，使用专用合成全局数组；禁止直接选择用户工程大数组。
2. 调用 `read_pml_array`，覆盖空、稠密、稀疏、分页末端、超出末端和超长字符串，记录请求预算、实际读取数量、MAXINDEX 上界、截断和续读位置。稀疏空槽位须保留原生显示或明确失败，不压缩成稠密数组。
3. 旧 Host 应在数组读取执行前拒绝不支持的请求；记录次数，禁止降级为全量读取或自动重试。续页只查询已有数组，不重放生成表达式。
4. 核对成功及失败时唯一临时全局的清理，源数组不得被删除。离线算法和协议测试不代表该 AVEVA 产品已实测。
