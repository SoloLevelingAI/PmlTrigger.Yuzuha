# Design

## Context

初版将源图直接嵌入 Image。每个 ButtonTool 只有一份 Image，布局引用的 LargeImage 决定显示大小。本机实际文件为 E3D 3.1 及 Marine 12.1 SP4 的 DesignCustomization.xml，均含 UICustomizationFiles/CustomizationFile。

## Goals / Non-Goals

**Goals:** 统一后端生成尺寸合适图像；部署逻辑独立于前端与生成核心；只在用户页面确认后写入具体安装。

**Non-Goals:** 不执行 Macro、不自动重启 AVEVA、不替换系统 design.uic、不修改环境变量；本轮不向实际安装部署示例。

## Decisions

- 上传 /images/prepare 和 normalize 使用 sharp 生成 32×32 PNG 主图；导出按布局再生成 16×16 或 32×32，统一保持比例、透明边距、移除元数据。原图上限提升至 12 MiB / 8192×8192，请求仍限 20 MiB。
- 混合大小引用需要额外 ButtonTool，稳定派生名称且避开用户标识；两份定义共享原始 Macro、Key 和提示。仅一种尺寸时保留按钮原标识。
- packages/uic-deployment 实现检测、预览、备份和写入，API 封装，网页只负责选择和展示。注册表优先发现，验证文件后补目录候选；允许用户手动填写路径。
- 部署文件名为 RibbonStudio.<业务命名空间摘要>.uic，加载条目 Name 使用同样稳定前缀。仅接受 mon.exe 同目录真实存在的模块清单，拒绝符号链接清单及目标 UIC。
- 预览记录清单和原 UIC SHA-256；确认先复查内容，再建立 .ribbon-studio-backups/<时间及唯一 ID>，保存原字节和 receipt.json。临时文件写完后重命名替换，验证摘要；失败则尽力回退并报告无法恢复的路径。
- 清单采用解析验证加最小插入，保留无关文本、注释、UTF-8 / 带 BOM 的 UTF-16 及换行。不支持的编码或 DTD 拒绝写入。同名不同路径条目和未登记的同名 UIC 拒绝覆盖。

## Risks / Trade-offs

- [Program Files 无写权限] → 返回明确错误，由有权限用户启动后端后操作，不自动提升权限。
- [外部程序并发编辑] → 预览摘要及提交前复查；进程内按安装目录互斥。无法提供跨进程的全局文件锁，仍可能出现极短竞争窗口。
- [两个文件无法单次原子提交] → 备份、逐文件原子替换、写后校验和失败恢复；保留备份供手工恢复。
- [实际 Ribbon 加载尚未验收] → 仅完成文件部署功能和模拟目录验收，不宣称 AVEVA 已加载。
