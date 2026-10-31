# Spec Delta

## Purpose

允许用户明确选择本机 AVEVA 安装和模块，预览生成文件与加载清单的更改，再以备份和冲突检测保护已有配置，完成可追溯的 UIC 部署，为实际环境加载验收准备文件。

## ADDED Requirements

### Requirement: 显式选择部署目标

系统 SHALL 支持安装发现及手工 mon.exe 路径检测，仅允许在其同目录的有效模块 Customization/Customisation XML 中登记。未选择目标时 SHALL 不执行写入。

#### Scenario: 多版本共存
- **WHEN** 本机存在 E3D 与 Marine
- **THEN** 用户 SHALL 能选择安装及具体模块，其他安装文件 SHALL 不变

### Requirement: 只读预览

系统 SHALL 在确认前展示 UIC 路径、清单路径、文件大小及加载登记条目。预览 SHALL 不写入文件，并记录目标内容摘要供确认时检查。

#### Scenario: 查看部署计划
- **WHEN** 用户选择目标并点击预览部署
- **THEN** 页面 SHALL 展示具体路径和登记条目，安装目录字节 SHALL 不变

### Requirement: 备份并登记部署

确认部署 SHALL 先备份原清单及已有的本项目 UIC，再写入 UIC 并登记单个加载条目，保留其他条目、注释、换行和支持的编码。同一版本重复部署 SHALL 不重复登记。

#### Scenario: 更新已部署项目
- **WHEN** 项目 Macro 更新后再次部署
- **THEN** 旧 UIC 与清单 SHALL 可从备份恢复，清单中 SHALL 只存在一个本项目登记

### Requirement: 处理冲突与写入失败

系统 SHALL 拒绝过期预览、冲突文件名、非法清单和越出所选安装的清单路径。权限失败 SHALL 明确报告。写入失败 SHALL 尝试恢复已写部分，并在恢复失败时报告备份和待恢复路径。

#### Scenario: 预览后清单被其他程序修改
- **WHEN** 用户确认旧预览
- **THEN** 部署 SHALL 被拒绝并要求重新预览，不覆盖新清单
