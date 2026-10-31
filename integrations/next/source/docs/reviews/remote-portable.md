---
document_kind: design
implementation_status: proposed
reviewed_on: 2026-10-29
uid: remote-portable-review
---
# 远程工程：连接、授权与配置迁移

2026-10-28。**设计待实现；G0/G1实验环境已通过。** 本页解释操作与方案，完整规则只维护在[OpenSpec](../../openspec/changes/portable-profiles-and-credential-grants/specs/portable-profiles-and-credential-grants/spec.md)，不另存一份长说明。

<a id="brd"></a>

## ① 为什么做（BRD）

**工程连不上能找到原因，换电脑不用重填配置，密码由你在本机授权。** 使用者少试错，管理员看清影响，维护者顺着脚本找到系统能力。目录可读、工程注册正确、数据库可用和许可签出成功分别验证，不互相替代。

<a id="prd"></a>

## ② 做到哪里（PRD）

已有本机应用与Project/Module发现、JSON保存、启动协调器、凭据基础和NLog快照。拟增加远程检查与手动连接、共享凭据授权、TOML迁移。**本轮不改产品代码、不安装依赖、不实现WPF。**

E3D3.1共用E3D2.1工程有效，不按应用版本改写Proj。现有应用发现查目录，未用注册表；AM按Marine识别。Windows共享账户与AVEVA登录账户分开。

<a id="usage"></a>

## ③ 怎么操作（FRD）

例子：宿主机E3D3.1启动APS/DESIGN，使用远程测试服务的E3D2.1目录：`\\192.168.244.10\AVEVAProjects\E3D2.1\AvevaPlantSample\APS000`。网络账户现为`ZHANGSH\ld_reader`，AVEVA账户SYSTEM、MDB为ALL。**测试目录只有标记文件，没有真实AVEVA数据库。**

1. **检查工程。** 选APS，核对目录和init/evars，点“检查连接”。已有会话可读就继续；未配置或失败时显示原因，提供手动配置、重新检查、取消。
2. **确认连接。** 填服务器、共享、相对目录、网络账户及本机凭据。“仅保存草稿”不连接；“确认连接并检查”才调用Windows。默认UNC、不自动重连；确需P:才单独确认空闲盘符，绝不覆盖。
3. **核对注册。** 比较init/evars中APS的声明目录。冲突时改选正确来源；无法静态判断时展示来源，人工确认绑定路径、文件指纹与Revision，任一改变即失效。不改厂商初始化文件、不生成包装器，也不假定注入变量能覆盖原set。
4. **授权登录。** 选本机AVEVA凭据，预览工程、模块、账户与旧绑定，勾APS/DESIGN、APS/DRAFT；需跨工程再勾SEA/DESIGN。确认只改变所选项，不复制明文、不授权未来模块。网络凭据不混入此列表。
5. **撤销或更新。** 单项撤销只解除该项；更新/删除先展示全部受影响引用。撤销只影响未来启动，不能召回秘密或关闭已启动应用。
6. **迁移。** 预览后导出非秘密配置；导入检查格式、版本、路径与ID冲突，默认创建副本。允许仅导入配置，但受影响项均未绑定；本机另选凭据、确认授权、重新检查后才能启动。同名不自动匹配，未受影响项保持。

<a id="requirements"></a>

## ④ 数据与规则（SRS）

| 数据 | 内容与责任 | 导出边界 |
|---|---|---|
| ProjectLocation / ConnectionDraft | 工程路径、初始化来源；服务器、共享、账户提示、模式、Revision | 参数可迁移，Windows会话不可迁移 |
| LoginProfile / CredentialSlot | 配置ID、应用/工程/模块/账户/MDB；所需凭据槽位与Kind | 可迁移；槽位不是授权 |
| LocalCredentialBinding / CredentialGrant | 本机秘密引用；精确ProfileId→BindingId及安全指纹 | 只留本机，不随配置导入导出 |
| OperationResult / Record | OperationId、Revision、状态、原生错误、时间、下一步 | 脱敏记录，不含密码、令牌 |

同Slot不等于同授权：仅勾选项改指向指定Binding；更新共享Binding才影响其全部引用。应用、工程路径、模块、账户或Kind改变则旧授权失效。应用授权不是抵御同用户恶意进程的独立安全边界。

每次保存写新generation：`profiles.toml`与本机`bindings.local.json`齐备后切换`current.json`。读者固定同一代；单写者锁和Revision防过期覆盖。它不是文件、凭据库和SMB的跨系统事务，也不保证断电零丢失。导出按字段白名单，不能打包目录；导入校验禁止字段与引用，不能携带有效授权。

[Tomlyn 2.10.1](../decisions/toml-selection.md)已批准，尚未安装实测。用语法树最小修改保留注释/格式；未知键或模式版本先只读/拒绝保存。JSON迁移先预览、备份并保留原文件，实施前仍用JSON。[TOML示例](examples/portable-profile.toml)只有配置与槽位。

<a id="frontend"></a>

## ⑤ 前端怎么接（SDD）

先评审[FigCraft表单和状态](../design/remote-portable-review.md)，再实现WPF。ViewModel校验与显示；普通输入信息类承载参数；后端协调器执行。属性setter不调用系统。

```text
初始化：配置快照 + 本机凭据非秘密目录 → ViewModel显示
编辑：校验字段 → 增加编辑版本，不连接
点击检查：复制输入 → 冻结(OperationId, Revision) → 协调器.CheckProjectAccess
确认连接：预览副作用 → 冻结输入 + 临时秘密句柄 → 协调器.ConnectProjectShare
确认授权：所选ProfileId、凭据ID、Kind、预览版本 → 协调器.GrantLoginCredential
确认导入：路径/冲突选择及预览版本 → 协调器.ImportProfiles → 新项显示未绑定
返回：OperationId仍为当前且Revision未变才更新页面，否则丢弃旧回显
取消/切页：取消等待；副作用操作仍按OperationId查询真实结果
```

WPF须显式模板，遵守图稿尺寸/字体/颜色；100%/125%/150%DPI实拍核对焦点、错误、不可用、换行与遮挡，不能以默认样式代替验收。本轮不改变现有UI。

<a id="backend"></a>

## ⑥ 后端怎么做、为什么（SDD）

协调器显式调用具体C#脚本；脚本决定业务，后勤接口只接路径、账户、字节与秘密句柄，不认识APS/DESIGN。以下脚本拟放LoginLaunch/Scripts，**尚未实现**，不为每步再加接口或注册DSL。

```text
CheckProjectAccess：校验路径边界/Revision → 先记录意图
  → 同用户只读C# Worker检查目录 → 返回原生错误与ProbeResult
  8秒总截止：终止自己的Worker；清理失败阻止启动；丢弃迟到结果
ConnectProjectShare：校验确认范围/Kind/空闲盘符 → 记录意图
  → IShareConnections/WNet，UNC盘符=null，默认不设置CONNECT_UPDATE_PROFILE
  → 成功后再检查目录；超时/取消返回OutcomeUnknown，先查询、不自动重试
GrantLoginCredential：锁内重读版本/指纹 → 意图记录 → 仅改勾选项 → 发布新代
  同OperationId同内容返回原回执；内容不同拒绝
ImportProfiles：限大小/严格解析与校验 → 预览确认 → 无本机绑定的新配置
  → 意图记录成功才完整发布，失败保留旧代
ExportProfiles：固定快照 → 白名单投影 → 确认目标 → 临时写入并替换
  不改本机授权或current指针；失败保留旧目标
启动：当前工程访问 → CheckProjectRegistration匹配/有效人工确认 → 许可检查
  → 当前绑定和授权 → 临时取秘密 → CreateIntent成功 → 原有StartMonitor
```

只读检查可终止；连接却可能已改变Windows会话，取消不能宣称“未发生”。不自动断开其他连接、不换地址绕过1219、不改ACL或防火墙。网络断开不代表数据库损坏；创建结果不明时人工核验、不自动重试。[WNet依据](https://learn.microsoft.com/en-us/windows/win32/api/winnetwk/nf-winnetwk-wnetaddconnection2w)

新增/更新秘密先写全新条目再提交引用；失败保留旧绑定，新孤立条目标记CleanupPending，只清理本次新增且无引用项。启动与撤销在同一锁下最后核对授权。创建前记录失败就停止；提交后日志失败则报告“已提交、证据待核验”，不能盲目重试。

现有IJsonDocumentStore实施时改为格式中性的文档契约；IFileAccessProbe、IShareConnections及秘密/记录能力提供系统服务。序列化和OS包留在适配层，用ArchUnitNET检查边界。详细生命周期与故障场景以OpenSpec为准。

<a id="failures"></a>

## ⑦ 失败后做什么（SDS）

| 结果 | 下一步与边界 |
|---|---|
| 未配置 / 路径不可用 | 保留草稿，核对服务器、共享、目录；不据此判断密码错或数据库坏 |
| 登录失败 / 权限拒绝 | 分别核对账户与共享/NTFS权限；TCP445通不代表目录可读 |
| 1219冲突 / 盘符占用 | 查看已有会话或改选盘符；人工处理，不自动断开/覆盖 |
| 检查超时 / 取消 | 阻止启动，清理只读Worker；清理未完明确提示 |
| 连接结果未知 | 提示连接可能已发生，查询后决定，不自动重连 |
| 注册冲突 / 无法确认 | 核对初始化来源，确认当前路径、指纹、版本 |
| 未绑定 / 未授权 / 版本冲突 | 绑定授权或刷新预览，不取秘密启动 |
| 解析失败 / 未知模式 / 禁止字段 | 标出位置但不回显秘密，保留原文件 |
| 写入或证据失败 / 待清理 | 区分未提交与已提交，展示待核验项，不承诺跨系统回滚 |

<a id="verification"></a>

## ⑧ 怎么验收（SDS）

**已完成G0/G1：** VMware Host-only、专用账户、假工程目录，10项真实SMB检查通过；两份快照保留，原VMX克隆重写例外仍公开。[实际结果](xref:remote-project-lab-results)列出证据与边界。

**待完成G2–G5：** 产品连接/取消/1219/断网/盘符、授权竞争、导入导出与TOML注释往返、FigCraft到WPF截图、真实AVEVA工程和许可。分规则测试、VM系统测试、厂商工程测试三层，保存List<Record>与脱敏日志；现有99项产品测试不与10项OS检查混算。127.*仅可本机冒烟，不等于远程/匿名，不放宽Guest或签名策略。

**许可在哪里？** 宿主机有E3D客户端；测试VM发现Licensing System 4.1，未发现E3D客户端。服务运行不等于许可签出；恢复原生启动模式后已进入Model并归还许可，开发对照见[维护记录](licence-checkout.md)。SMB实验无需先装E3D，真实数据库/锁/登录必须另验。

入口：[VM手册与验收顺序](remote-project-lab.md#acceptance)、[逐条需求](../../openspec/requirements.md)、[任务与批准状态](../../openspec/changes/portable-profiles-and-credential-grants/tasks.md)。
