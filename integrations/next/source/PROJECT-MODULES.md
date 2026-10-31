# Project/Module发现与逐条需求记录 — 2026-10-28

> 历史阶段资料：保留当时行为和结果；当前实现与未完成项以[交付状态](docs/status.md)为准。

当前实现从b16016c继续，分支codex/project-module-discovery。新增具体C#脚本FindAvevaProjectsAndModules，通过IFileSystem.ReadText做有界只读检查，前端/后端边界和UI布局保持。

读取evars.bat、evars.init、launch.init及静态引用，补充launch.bat/pdms.bat模块分支；按projects_dir扫描子目录，识别项目代码000声明和目录线索，跳过数据库内容及已声明绘图数据目录。返回项目/模块候选、文件行号、目录存在标记和限制诊断。候选写入独立ProjectModules记录，未知/多项不自动挑首项；手动值和预设优先。

本机扫描：Marine6个项目代码/5个模块名；E3D2.10和3.1各9个项目代码/9个模块名。没有触发配置文件、目录或超时上限。保留E3D3.1共用E3D2.1路径，不按安装版本猜配置。静态线索不是当前运行环境，也不证明项目权限、模块许可或数据库健康。

安装发现仍用配置目录递归，不读注册表；AM按Marine处理，PDMS新增目录识别但未做真实产品登录。没有执行任何被读取的脚本。

40单元+9集成+47架构+3界面=99项通过。发布EXE已验证安全流程；不截取可能被用户其他窗口遮挡的截图。证据在artifacts/project-modules，DocFX验证页链接实际扫描和测试报告。

用户新增诉求逐条记录于openspec/requirements.md，共17条。已实施PM-01—05；RP-01—03（远程提示/手动连接）、PF-01—02（TOML持久化与导入导出）、CR-01—07（本机秘密/共享引用/列表授权/生成项/撤销/导入重绑定/安全边界）保留明确状态。没有安装TOML库，也没有执行网络映射、导出或批量授权。

新发布目录artifacts/publish-project-modules；前两版发布目录和快照保留。原项目及Rewrite所在两仓库HEAD/工作区状态与初始只读快照一致。Git Diff和新的可恢复快照用于后续继续修改。
