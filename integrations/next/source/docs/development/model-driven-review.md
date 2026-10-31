---
document_kind: reference
implementation_status: mixed
reviewed_on: 2026-10-29
uid: model-driven-development-review
---
# 同一模型编辑与双包交付：八节说明

**产品包＋独立开发测试工具包已确认并实现核心链路。** 既有UI未改，未启动VM；主题仍待认可。具体源码与错误返回统一见[开发索引](INDEX.md)，不再复制一套参数表。

## ① 客户得到什么

修改同一份配置后直接执行，后端记录当时输入；失败能找到阶段和原因，后续编辑不会改变正在执行的请求。普通远程不需要实验开关或VMware。

## ② 使用者如何操作

原窗口选已有配置或候选 → 编辑字段 → 自动保存草稿 → 点击执行 → 查看结果与快照。保存配置按钮仍用于命名配置；执行当前草稿不要求再点保存。口令仍走原有临时输入/本机凭据，不写入模型。管理员修改许可服务参数也通过同一模型。

## ③ Model与文件在哪里

[LaunchModel](../../src/LaunchDesk.Next.Application/Features/LoginLaunch/LaunchModel.cs)是唯一可编辑对象；端口用字符串保留未完成输入。WPF适配器只有转发属性，没有第二份字段值。draft.json保存Generation与Model；执行快照是snapshots/<AttemptId>.json。文件代次冲突停止覆盖，重新打开载入；不宣称已有外部文件监视自动合并。当前为JSON，没有引入Tomlyn或依赖安装。

## ④ 前端究竟做什么

```text
控件 ⇄ 薄绑定适配器 ⇄ 同一个LaunchModel
Model.Changed → 后端保存草稿
Model.Changed → 绑定刷新字段与校验提示
```

前端只转发输入、选择和执行事件，显示候选、状态、格式提示及证据；候选合并、扫描记录、保存版本、业务准入与脚本选择归后端。输入Model不依赖WPF或CommunityToolkit。

## ⑤ 后端如何执行

```text
LaunchWorkspace.ExecuteAsync:
  frozen = Model.Freeze()                 // 深拷贝、AttemptId、Revision
  CheckSecretFields()
  FlushDraftWithGenerationCheck()
  AtomicPersist(snapshot/frozen)
  AppendRequiredRecord()
  request = ParseParameters(frozen)       // 非法端口拒绝，不替换成0
  coordinator.ExecuteSnapshotAsync(request)
    → 校验 → 许可检查 → 凭据 → StartMonitor/Marine/Direct → Windows实现
```

快照/记录/版本门槛失败不进入脚本。既有保存配置API仍保留保存版本约束；新的草稿执行入口复用同一协调器主体，仅不要求输入先等于命名配置，避免两套启动实现。

## ⑥ 开发工具如何配置VM和许可

开发CLI用同一LaunchModel文件执行普通启动，许可服务器、端口、RULES等没有写死在实验器。lab.example.json另保存VM路径、允许根、预期身份、VIX路径、凭据文件引用、超时与证据目录。本机显式授权、目标摘要和后端身份检查同时满足才允许VM动作；配置本身不启用实验、不自动开VM。可调参数不包含许可密钥、绑定或时间修改。

## ⑦ 如何失败、返回与验证

List<Record>＋NLog＋完整输入快照关联AttemptId；非法输入、版本冲突、写入失败都返回阶段与原因，空异常消息也有类型/HResult兜底。创建进程之后失败必须保留NeedsVerification，不能说“未执行”或自动重试。回显不覆盖较新草稿。离线回归覆盖冻结后再编辑、非法原值留存、记录失败零调用、冲突拒绝及双向通知；VM测试只用临时文本夹具，未连接VMware。

## ⑧ 包、Feature与未完成范围

同一个WorkspaceHost装配login.launch，两个包内四个共用DLL须字节一致。产品不带VM入口；开发工具额外交付实验Feature入口，移除不影响普通启动。这是可由宿主装配的边界，尚未对接具体CAF插件API，XML Addin控制已编译Feature的装配，不是任意DLL动态加载器。历史FNF脚本保留为不编译的History证据，不再维护为第二启动器。

当前交付为框架依赖包；新远程连接/TOML授权流程、FigCraft新界面、VM真实参数回归和完整CAF适配仍未实现。原XAML、资源与视觉主题不改；DocFX主题也没有被标成视觉通过。

启用规则现由[Addin清单](addins.md)决定；没有声明即不装配，代码存在不能自行启用。
