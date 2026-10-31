# 图标压缩与 UIC 部署

v0.3 现有 UIC 的接管、原路径更新和历史恢复参见 [管理与历史](management-history.md)。以下哈希文件规则仅适用于新建部署。

## 图标

上传 PNG/JPEG 后，独立后端将图像按比例缩放至透明 32×32 PNG，剥离元数据并压缩。矩形图像居中补透明边，不拉伸。项目仅保存此轻量主图；旧项目在保存或导出时也会被处理。Large 引用输出 32×32，Small 输出 16×16。

由于一个 ButtonTool 只保存一个 Image，同一按钮同时用于 Large 和 Small 时，导出生成两个定义，并将引用分别指向适当尺寸。派生名称稳定且避开用户 ID；原始项目结构、Macro、Key 与 Tooltip 不变。

原图最大 12 MiB / 8192×8192，整个请求最大 20 MiB；仅接受可完整解码的单帧图片。输出为 PNG，可保留透明通道，不包含原始 EXIF 数据。

## 使用部署入口

1. 点击“部署到 AVEVA”。页面列出能验证到 mon.exe 和模块清单的本机安装；默认不选择任何目标。
2. 选择安装，或手动填写完整 mon.exe 路径并点击“检测目录”。
3. 选择实际模块清单，例如 DesignCustomization.xml、OutfittingCustomization.xml 或 DrawCustomization.xml。
4. 点击“预览部署”。检查清单路径、生成 UIC 路径、文件大小和 CustomizationFile 登记条目。预览不会写入。
5. 点击“确认部署”。后端先备份，再写入 UIC 和加载条目，显示备份位置。相同版本重复提交不会新增条目。
6. 重新进入对应 AVEVA 模块或重启 AVEVA，检查 Ribbon。部署不执行 Macro，也不自动重启软件。

原用户描述的 xxxCustomzation.uic，在本机实际对应 xxxCustomization.xml 加载清单。它列出要加载的 .uic 文件，二者格式和职责不同，不能互相覆盖。

## 文件与恢复

UIC 以 RibbonStudio.<业务命名空间 SHA-256 前 16 位>.uic 命名，登记条目使用同样稳定名称，因此修改项目内容可更新原部署。改变 namespace 会产生新的独立部署，不自动删除旧部署。

备份位于 mon.exe 同目录下：

~~~text
.ribbon-studio-backups/
  <UTC 时间及唯一 ID>/
    <选中的模块清单>.xml
    RibbonStudio.<摘要>.uic    （仅当原先存在）
    receipt.json
~~~

receipt.json 记录部署前后摘要、路径和 UIC 是否原本不存在。手工恢复时，先关闭相关模块或 AVEVA，确认后续没有其他要保留的清单编辑，再用该次备份恢复清单和原 UIC。如果原 UIC 不存在，可在恢复清单后移除该次创建的 UIC。未提供自动回退入口。

不覆盖同名但未登记为本项目的 UIC，不替换同名指向其他文件的加载条目。预览后目标发生变化则要求重新预览。权限不足返回明确错误；如安装在 Program Files，可能需要由有写权限的用户启动后端，不会自动提权或更改目录权限。

清单保留无关条目、注释、换行及 UTF-8 / 带 BOM 的 UTF-16 字节编码。写入使用临时文件替换，失败时尝试恢复已写部分；若恢复也失败，错误中给出备份和待恢复文件路径。

## 验证边界

本轮真实安装只做发现和只读预览。文件写入、重复部署、更新、备份和冲突测试均使用工作区内的模拟安装，未执行实际 AVEVA 加载和 Macro。

本机读取确认：

- E3D 3.1：mon.exe 同目录存在 DesignCustomization.xml，根为 UICustomizationSet。
- Marine 12.1 SP4：同目录有 DesignCustomization.xml 和 OutfittingCustomization.xml 等。

官方文档同样说明模块 XML 清单登记 UIC 路径，并在模块启动时处理：

- [E3D 3.1 .NET 自定义环境变量及加载清单](https://help.aveva.com/AVEVA_Everything3D/3.1/NCUG/NCUG3.04.3.html)
- [E3D 3.1 创建 UIC](https://help.aveva.com/AVEVA_Everything3D/3.1/PMLPUG/PMLCG24.27.16.html)
