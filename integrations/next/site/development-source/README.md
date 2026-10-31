# 独立开发测试工具包

产品与开发工具复用Application、Domain、Platform、Bootstrap的同一批程序集；`WorkspaceHost`是唯一登录Feature装配入口。不同分发目录中的相同DLL不是两套源码，包manifest对四个DLL逐一比较SHA256。CAF具体宿主适配尚未实现，不宣称已有CAF插件兼容。

产品包不带LabControl、LabVix、实验模型或VMware依赖；开发包附带模型CLI与VM控制工具。既有WPF界面不改；Settings未来可作为编辑器，不承担依赖隔离。所有命令都必须手动明确执行，不在安装或初始化时运行。

## 编辑和执行普通启动模型

```text
LaunchDesk.Next.Development.exe model-init --file launch.json
LaunchDesk.Next.Development.exe model-check --file launch.json
LaunchDesk.Next.Development.exe model-execute --file launch.json --data <独立数据目录>
```

launch.json采用共用LaunchModel，许可服务器/端口、RULES、本机服务、应用与工作目录都在这里编辑。开发CLI与产品都经过LaunchWorkspace：冻结模型→保存snapshots/<AttemptId>.json→记录→后端校验与具体脚本；不复制Monitor调用实现。输入密码不写文件，CLI只使用None或本机Stored引用；Prompt无输入时取消。本机草稿是draft.json的Generation/Model封装，外部变更导致冲突时停止覆盖，重新打开载入；尚无文件监视自动合并。

## 显式操作实验VM

先复制并编辑lab.example.json，填VM路径、允许根、预期UUID/MAC、VIX路径、凭据引用文件、超时和证据目录。示例占位值不能用于真实操作。许可/应用参数继续来自launch.json，不在实验代码再存一份。

```text
LabControl.exe status
LabControl.exe fingerprint --model <lab.json>
LabControl.exe enable --local
LabControl.exe run --runtime <本包目录> --model <lab.json> --confirm-target <核对后的指纹> probe
LabControl.exe disable --local
```

fingerprint只计算配置摘要，不连接VMware；启用只写本机/用户绑定开关。run先查本机开关，再验证模型与目标身份；LabVix再次检查后才连接VIX。只支持明确probe/start/stop/snapshot，普通启动模型不会自动启动VM。复制配置不能转移本机授权；disable不是取消正在执行的操作。目标校验检查允许根、无链接路径、UUID/MAC、Host-only及VIX返回路径，是防误操作约束，不是安全沙箱。

开发动作在副作用前保存输入快照，结果同时存内存Records、records.jsonl与stdout；失败保留stage/error。VIX超时为结果未知，不能自动重试。凭据文件由现有受保护交接准备，DPAPI仅本机用户可读，示例不包含口令；不要将实际lab.json或凭据文件放入产品包/Git。

## 构建两包

使用现有SDK与本地缓存构建Tool项目，再运行：

```text
LaunchDesk.Next.Development.exe package --repo <仓库> --output <全新输出目录>
```

生成product/development目录、两个ZIP和manifest.json；两个包为框架依赖包，需现有.NET10 Desktop Runtime（产品）、.NET10 Runtime（开发CLI），VM工具还需Windows内置.NET Framework与另装的VMware。打包不带VIX厂商DLL、不安装依赖，也不运行VM。Framework工具由同一份DevelopmentSwitch/LabSettings源码编译，使用build.rsp；未使用两份维护中的开关实现。

`History`是保留的FNF复现实验源码，不编译、不分发、不再作为正常启动入口；Guest目录保留历史环境审计工具，不自动执行或作为通用新VM配置向导。历史artifacts固定当次证据，活动实现只维护这里和共用产品后端。`.local`、口令、实际配置、VM和构建产物不提交Git。

## Addin控制装配

运行时从同目录Addins/*.addin.xml读取启用声明；产品只有login.launch，开发包再加development.remote-project-lab。删去清单、改扩展名为.disabled或enabled=false后重启，相关Feature不装配。开发CLI的features列出声明，帮助不显示未注册业务命令，直接调用被拒绝。旧features.json不再生成。

XML仅三个属性，例如`<Addin id="login.launch" enabled="true" contractVersion="1" />`。清单不存业务参数或凭据，不做任意DLL扫描。VM脚本通过同包开发CLI复用唯一AddinCatalog，仍保留本机授权、模型快照和目标身份校验；启用Addin不自动启动VM。Framework工具应使用完整匹配的开发包，单独复制exe缺少Addin宿主时拒绝执行。
