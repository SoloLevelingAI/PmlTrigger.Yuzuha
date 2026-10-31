# Spec Delta

## Purpose

让用户将已有 UIC 转换为可编辑 Ribbon 项目，在修改图标、Macro、Tooltip 和 Group 时保留与本次编辑无关的 XML 信息，保证现有自定义文件能够继续维护而不发生静默数据丢失。

## ADDED Requirements

### Requirement: 导入已有 Macro Ribbon

系统 SHALL 支持上传 UIC 或读取明确输入的本机绝对路径，并保存原始快照。导入 SHALL 不修改源文件。

#### Scenario: 导入 YtoolsAM
- **WHEN** 用户导入现有 YtoolsAM.uic
- **THEN** 普通 Tab、Group、Macro 按钮和图标 SHALL 可编辑，源文件字节 SHALL 不变

### Requirement: 保留未编辑字段

导出 SHALL 保留原快捷键、额外属性及未编辑 XML 节点；重命名和移动按钮 SHALL 更新引用。不能安全支持的可见工具或危险 XML SHALL 明确拒绝，不能静默丢弃。

#### Scenario: 移动含快捷键的按钮
- **WHEN** 用户编辑 Macro 并移动按钮到另一 Group
- **THEN** 原快捷键及附加属性 SHALL 保留，目标 Group 中 SHALL 有正确引用
