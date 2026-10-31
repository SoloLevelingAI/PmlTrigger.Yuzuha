# 单仓迁移验收记录

验收对象是 rewrite、Next、Ribbon/UIC 的源码、设计、作品集与本仓 Manager 到 Ribbon 的调用链。标准 SDK、NuGet/npm 包由正常工具还原；不复制相邻项目的构建产物，不登录 AVEVA。

## 全新克隆方法

使用 `git clone --no-local --no-hardlinks` 从已提交源码建立独立目录，父目录仅含该克隆。进入克隆后执行 `./scripts/Test-IntegratedWorkspace.ps1`，默认 Debug、恢复依赖、失败立即停止。报告绑定源码提交和逐步退出码。原始日志及 TRX 留在对应克隆的 `artifacts/portable-validation/`；本目录保留可迁移摘要。

## 失败与修复

1. `attempt-1.json`：提交 `0dadf7b`。rewrite 构建因 Microsoft.OpenApi 2.0.0 的 NU1903 高危漏洞失败。升级至同主版本修复版 2.7.5，保留漏洞审计；另行运行 DevHost 验证 OpenAPI 生成及 Scalar HTTP 200。
2. `attempt-2.json`：提交 `233dc1e`。rewrite 构建零警告通过，但 AR01-25 固定读取 Release 下的 Desktop DLL，干净 Debug 克隆未生成该文件。修复测试定位为当前配置，同时移除旧产品测试隐式生成 Release 的行为。没有修改为 Release 验收来掩盖问题。
3. `attempt-3.json`：提交 `309948d`。rewrite 完整通过，Next 的进程参数测试在文件出现但写入句柄尚未关闭时读取 JSON，触发共享冲突。按完成发布信号修复测试夹具，不通过重跑或放宽参数断言掩盖问题。

最终完整验收结果将在通过后记录。失败记录不替换、不伪装成成功。

## 边界

可选 7zr、真实设备发现、交互式 WPF、CAF/UIC 实机加载与新机器 APS 登录不计入本轮默认测试。原机器 APS/Host/PML 证据保留在原验证文档，不能据此宣称另一台电脑已实机验证。来源资产迁入不自动完成 OpenSpec 原平台业务融合任务。
