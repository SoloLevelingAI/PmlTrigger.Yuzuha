# Spec Delta

## Purpose

为用户上传的大尺寸图像提供统一后端缩放与压缩，保存轻量项目，并让 UIC 中 Large 和 Small 按钮分别使用精确的 32×32 和 16×16 图标，兼顾透明背景、比例及复用行为。

## ADDED Requirements

### Requirement: 自动准备图标

后端 SHALL 将有效 PNG/JPEG 图像按比例缩放并补透明边距为 32×32 PNG，移除原图元数据；网页上传和项目规范化 SHALL 使用此处理结果。

#### Scenario: 上传矩形大图
- **WHEN** 用户上传 1024×512 图片
- **THEN** 项目保存的图标 SHALL 为 32×32，内容比例不变，边距透明

### Requirement: 按布局输出尺寸

导出 SHALL 为 Large 引用输出 32×32 图片，为 Small 引用输出 16×16 图片。同一按钮两种大小复用时 SHALL 保持命令和提示一致，并生成无冲突定义及正确引用。

#### Scenario: 同一按钮在两 Group 复用
- **WHEN** 一个 Group 使用 Large、另一个使用 Small
- **THEN** 两处引用 SHALL 分别解析到 32×32 与 16×16 的图像，且重复导出字节一致

### Requirement: 拒绝无效输入

后端 SHALL 拒绝无法完整解码、伪造类型、多帧、超过 12 MiB 或超过 8192×8192 的原图，并保留 20 MiB 的请求上限。

#### Scenario: 图片内容损坏
- **WHEN** 上传文件扩展名为 PNG 但字节无效
- **THEN** 后端 SHALL 返回字段错误，不替换原项目图标
