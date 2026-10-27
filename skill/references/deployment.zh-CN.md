# AVEVA 部署

使用正式安装器预览，选择实际安装及接入文件；不再执行 PowerShell MCP 注册。Host：E3D2.1→E3D2.1/NET48；E3D3.1系列→E3D3.1.6/NET48；AM/PDMS→NET35。SDK、模块元数据、登录与实机 Host 核验分别记证。

插件经工作台 C# 流程冻结源/目标哈希→备份→复制→向明确 PID/模块加载验证。原生 CLI 保留独立授权与原始回执；复制成功不代表已加载，结果未知不重发。

Invoke-PmlDevelopment.ps1 / PmlDevelopment.psm1 是待收敛到原生 CLI 的旧命令行入口；新业务写 C#，不继续扩充这条旧路。
