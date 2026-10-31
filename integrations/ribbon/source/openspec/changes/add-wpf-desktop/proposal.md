# WPF 桌面客户端

## Why
用户要求以已验证的 Office Ribbon 样式实现桌面端，迁移网页版现有功能，继续保持前后端独立，并在完成后启动查看。

## What Changes
- 增加原生 WPF 工程，复用 FigCraft Office Ribbon 的颜色、字体资源与原生矢量图标。
- 客户端仅通过 HTTP JSON API 访问现有后端，保留原始导入 XML 和来源标识。
- 迁移结构编辑、图标、Macro/Tooltip、JSON/UIC 文件、历史、权限管理、穿梭框和部署预览。
- 增加可重复构建、独立启动器、隔离端到端验证及运行文档。

## Impact
不改变后端业务规则，不覆盖 FigCraft 验收目录或真实 AVEVA 安装。开发启动默认复用现有工作区数据，部署写入仅由用户确认界面触发。
