---
uid: user-manual
document_kind: user-guide
implementation_status: navigation
reviewed_on: 2026-10-29
canonical_fact: ../features/launch-by-profile.md
---
# 使用手册：按配置启动应用

## ① 先知道有什么用

保存并核对应用、项目和许可参数，减少重复填写。功能范围以[正文①②](../features/launch-by-profile.md#brd)和[当前状态](../status.md)为准；本手册不包含待实现的连接、迁移和共享授权界面。

## ② 首次准备

使用产品包，解压后保留Addins目录与登录清单，再运行LaunchDesk.Next.Desktop.exe。包依赖兼容的Windows Desktop Runtime，具体目标见[项目文件](../../src/LaunchDesk.Next.Desktop/LaunchDesk.Next.Desktop.csproj)；已有[交付证据](../../artifacts/addins/manifest.json)。目标应用须自行安装，有可用工程和许可。不要将开发VM工具当使用前提。

## ③ 找到应用和工程

启动后扫描 → 选候选 → 核对路径/init → 从“快照与记录”查看ProjectModules / StaticCandidates来源 → 把确认的Project与Module填回工作台。详细操作与预填限制只维护在[正文③](../features/launch-by-profile.md#usage)。候选和目录存在不是已验证可登录。

## ④ 编辑与执行

按[正文③](../features/launch-by-profile.md#usage)核对用户、MDB、模块和许可等参数；编辑自动保存草稿，点击启动执行当前冻结输入。“保存配置”保存命名配置。需要密码时选Prompt并在本机提示中输入，模型和演示资料不写密码；Stored当前只引用已有本机凭据。

## ⑤ 如何知道结果

按AttemptId查看快照与记录。Created表示已创建进程；WindowObserved表示观察到窗口；两者都不能直接等同工程登录成功。状态含义与限制见[正文③④](../features/launch-by-profile.md#requirements)。数据目录可查看实际文件，勿公开未脱敏配置。

## ⑥ 出错先做什么

字段错误先修改；草稿代次冲突重新打开读取，不覆盖其他编辑；许可TCP通过仍需核实真实许可。遇到Unconfirmed/NeedsVerification先检查目标是否已启动，避免重复操作。取消密码不启动。精确边界见[正文③⑥](../features/launch-by-profile.md#backend)。

## ⑦ 管理员与拓展功能

管理员也通过同一配置核对许可参数。远程路径使用不要求VM开关；连接配置向导、TOML导入导出和凭据共享尚未交付，见[拟议范围](../status.md)。不按设计图去寻找尚不存在的按钮。

## ⑧ 需要演示或报问题

演示用[核心流程入口](demo.md)；报问题提供版本、脱敏配置、AttemptId、阶段与实际错误，不提供密码。开发定位从[开发手册](developer.md)进入，验证结论看[分批证据](../verification.md)。
