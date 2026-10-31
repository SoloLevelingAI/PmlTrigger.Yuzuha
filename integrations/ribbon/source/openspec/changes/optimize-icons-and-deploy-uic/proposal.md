# Proposal

## Why

原图直接嵌入 UIC 会造成文件过大，用户希望按 Large/Small 自动生成 32×32 / 16×16 图标。同时需要在页面选择 mon.exe 安装及模块，完成 UIC 文件和模块加载清单的部署。

## What Changes

- 后端上传处理和项目规范化将图标压缩为透明 32×32 PNG；导出按引用生成 32×32 或 16×16。
- 支持同一按钮在两种大小布局复用，输出无名称冲突的尺寸变体。
- 网页提供“部署到 AVEVA”：发现安装、手工路径检测、选择模块清单、部署预览及确认写入。
- 将 UIC 写到 mon.exe 同目录，在实际 xxxCustomization.xml / xxxCustomisation.xml 中登记；写前备份、拒绝预览后文件冲突、失败尽力恢复。
- 用户本轮仅要求做好部署入口，由其在页面选择，不对实际 AVEVA 安装执行部署。

## Capabilities

### New Capabilities

- ribbon-image-optimization：统一后端图像压缩与尺寸化导出。
- uic-deployment：安装检测、部署预览、备份及加载登记。

### Modified Capabilities

无已归档能力。初版 ribbon-uic-authoring 仍为进行中的新增能力，本变更补充其图像处理和交付流程。

## Impact

修改核心图片处理、规范化、XML 输出、HTTP API 和网页；增加独立 packages/uic-deployment，保留前后端边界。测试仅写项目内模拟安装，不执行 PML 或自动重启 AVEVA。
