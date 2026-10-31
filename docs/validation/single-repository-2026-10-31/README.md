# 单仓迁移验收记录

验收对象是 rewrite、Next、Ribbon/UIC 的源码、设计、作品集与本仓 Manager 到 Ribbon 的调用链。标准 SDK、NuGet/npm 包由正常工具还原；不复制相邻项目的构建产物，不登录 AVEVA。

## 全新克隆方法

使用 `git clone --no-local --no-hardlinks` 从已提交源码建立独立目录，父目录仅含该克隆。进入克隆后执行 `./scripts/Test-IntegratedWorkspace.ps1`，默认 Debug、恢复依赖、失败立即停止。报告绑定源码提交和逐步退出码。原始日志及 TRX 留在对应克隆的 `artifacts/portable-validation/`；本目录保留可迁移摘要。

## 失败与修复

1. `attempt-1.json`：提交 `0dadf7b`。rewrite 构建因 Microsoft.OpenApi 2.0.0 的 NU1903 高危漏洞失败。升级至同主版本修复版 2.7.5，保留漏洞审计；另行运行 DevHost 验证 OpenAPI 生成及 Scalar HTTP 200。
2. `attempt-2.json`：提交 `233dc1e`。rewrite 构建零警告通过，但 AR01-25 固定读取 Release 下的 Desktop DLL，干净 Debug 克隆未生成该文件。修复测试定位为当前配置，同时移除旧产品测试隐式生成 Release 的行为。没有修改为 Release 验收来掩盖问题。
3. `attempt-3.json`：提交 `309948d`。rewrite 完整通过，Next 的进程参数测试在文件出现但写入句柄尚未关闭时读取 JSON，触发共享冲突。按完成发布信号修复测试夹具，不通过重跑或放宽参数断言掩盖问题。
4. `attempt-4.json`：提交 `9544cce`。469 项测试通过，最后 Manager 到 Ribbon 的保存调用在深目录下失败，原生原子重命名返回路径错误。该轮不算整体通过；保留长目录作为回归条件，修复后端处理。
5. `attempt-5.json`：提交 `dc1122b`。Manager 原有批处理测试一边追加输出、一边读完成标志，发生文件共享冲突。补齐测试夹具的完成发布协议，并检查同项目中的同类读取；不改变生产启动或 Host 核验逻辑。

## 最终通过

第六份独立克隆（父目录仅含 `pmltwin`）验证提交 `0cd401bcf622e4d4d1c57372b008036fdee62d9a`，执行前后 Git 工作区干净。`final-result.json` 的 22 个步骤全部退出 0、`completed=true`。后续交接提交只更新文档和证据，不改变已验收源码。

| 验证 | 结果 |
|---|---|
| Manager / Database / Architecture | 197 / 14 / 21 通过；Manager 两项可选 7zr 跳过 |
| rewrite / rewrite Node | 71 / 10 通过 |
| Next Unit / Architecture / Integration / InputBehavior | 47 / 64 / 8 / 3 通过 |
| Ribbon | 35 通过，含超过 320 字符工作区的保存、重启与恢复 |
| 源码和设计资产 | 1,204 份导入原件最终哈希及项目引用闭包通过 |
| 三份作品集 | 172 个实际本地 HTTP 请求通过 |
| Manager 到仓内 Ribbon | 六次调用通过，重复导出字节相同，保存/读取/版本验证通过 |

测试共 470 项通过、2 项可选测试跳过。`evidence.json` 记录 TRX 计数与原始日志哈希，`ribbon-chain.json` 记录调用链结果。原始日志保留在本机第六份克隆的 `artifacts/portable-validation/`，在新电脑运行相同命令可重新生成。没有改动 AVEVA 安装或执行 Macro。

三个 `gpt-6.1-sol` 子代理分别负责来源集成、故障修复及独立复核。DocFX 构建零警告零错误；OpenSpec 严格校验通过。前五轮失败摘要保留，不替换为成功结果。

## 边界

可选 7zr、真实设备发现、交互式 WPF、CAF/UIC 实机加载与新机器 APS 登录不计入本轮默认测试。原机器 APS/Host/PML 证据保留在原验证文档，不能据此宣称另一台电脑已实机验证。来源资产迁入不自动完成 OpenSpec 原平台业务融合任务。
