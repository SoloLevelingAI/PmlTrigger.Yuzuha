# Skill 显式接管独立审查

日期：2026-11-02。范围：`installer/SetupBridge/SkillAdoption.cs`、`PlanEngine.cs`、`NativePages.iss` 及 `installer/tests/SkillAdoptionTests.cs` 当前源码。

结论：前轮确认的两项 P2 已在当前源码封住；限定范围内未发现仍可触发的 P1/P2。此次只读审查，没有重新编译或运行测试，没有安装、修改当前 Codex Skill、修改客户端目录或操作已打开的 WPF。

| 原问题 | 修复核对 | 对应反例 |
| --- | --- | --- |
| 缺少 `managed.original` 被误作显式 null，卸载可能删原文件 | `ValidateManagedEntries` 要求完整字段、显式 null 或有效 Base64 字符串，同时验证路径、摘要及重复目标；`ExistingState`、计划验证、提交和恢复入口使用该校验。`RestoreManagedFiles` 在处理任何文件前校验整个集合，之后按明确的 JSON null 判断删除。新生成记录显式保存 null。 | 缺字段、数值类型、损坏 Base64 的旧 marker：Create/Verify/Commit/Restore 均拒绝，尚未写备份或恢复较早条目，原文件保留；新建文件的显式 null 正例仍按原卸载循环删除。 |
| 非接管/未选 Skill 路径可伪造 `priorState`，添加未预览所有权或篡改初始原文 | 所有计划必需 `markerBeforeSha256` 与显式 `priorState`。`VerifyPriorState` 将原始 marker 摘要、完整历史记录与当前安装绑定；创建计划、验证、提交前、finalize 前均检查，不依赖 `adoptSkill` 或 `skillTarget`。首次原文来自该已绑定状态，不能由计划任意增项。 | `adoptSkill=false` 及空 target 两种计划均覆盖：删摘要/历史字段、篡改 original、追加其他客户端路径、marker 删除或仅改空白；验证及提交拒绝，未选文件不变。 |

其他已核对边界：

- `adoptSkill` 仅接受明确布尔值；默认保护仍拒绝未托管目录，true 还要求明确的现有 Skill 目录。
- 接管冻结完整目录成员、文件摘要与长度；增删改、空目录变化、真实 junction、范围删项、路径逃逸、重复项、伪造字节及预算超限均有反例。
- normal/adopt 两条路径都将 before/after 预览文字与冻结字节核对；ownershipAction 按绑定的既有所有权和 before 内容派生，不能独立伪造标签。
- 只将包内明确目标纳入 managed，extras 正文不进入预览，不新增所有权；卸载逐文件处理，不递归删除整个 Skill 或其他目录。
- 正常及无变化文件均保留首次原字节；更新不替换初始 original。提交失败使用既有事务备份和条件逆序恢复，失败不返回成功。

已阅读 `artifacts/setup-skill-adoption-tests.log`：包含当前完整 NET48 console 的 PASS 输出及真实临时文件/链接/回滚/卸载反例。执行结果来自负责代理已有运行，本审查没有独立重跑；这些证据不升级为真实安装或当前 Skill 接管验收。

本轮核对的源码 SHA256：

- `SkillAdoption.cs`：`c420fa000a6641063602fb933bb77ba7a84b0c5d8ab0d1885cd9b3ac73ca7f63`
- `PlanEngine.cs`：`a35dcf339832eb72e3e969493fc8fb860b7199ef4aaaecc3414569ad688c2ad7`
- `SkillAdoptionTests.cs`：`3a73448916802a8c3a051e6d697935f1fd3a0dc5afce4dba05938c03aeb5f6b9`

实际执行仍需针对重新生成的具体计划和安装包取得批准；本审查不构成安装批准。
