# 独立 API

v0.4 由 apps/backend 的独立 C# 服务实现；共享 DTO 位于 packages/contracts-csharp，网页 TypeScript 契约保持同形。GET /api/health 返回 implementation=csharp 与 contractVersion=1。

默认地址 http://127.0.0.1:4318 ，npm run dev:api 无须前端即可启动。POST 请求体直接传项目 JSON，Content-Type 为 application/json，UTF-8，总请求上限 20 MiB。

| 方法 / 路径 | 返回 |
| --- | --- |
| GET /api/health | 状态与版本 |
| GET /api/v1/projects/example | 含两 Group、四按钮和图片的示例项目 |
| POST /api/v1/projects/validate | {valid, issues:[{code,path,message}]}，参数问题以 200 返回列表 |
| POST /api/v1/projects/normalize | 补缺失 ID 与稳定命令 Key，验证后返回完整项目 |
| POST /api/v1/uic/export | UTF-8 XML，Content-Disposition 下载名 ribbon.uic |
| POST /api/v1/images/prepare | 请求 {image}，返回 32×32 PNG image、width、height、originalBytes、outputBytes |
| GET /api/v1/deployment/targets | 返回验证存在的本机安装 targets，每项含 monPath、directory、manifests |
| POST /api/v1/deployment/inspect | 请求 {monPath}，返回安装目录和模块清单 |
| POST /api/v1/deployment/preview | 请求 {project,monPath,manifestName}，返回只读计划和原文件摘要 |
| POST /api/v1/deployment/apply | 同 preview 再传 expectedManifestHash、expectedUicHash；写入并返回 backupDirectory |

normalize/export 参数错误为 422；JSON 解析失败 400；未知接口 404；错误方法 405；请求超限 413；不支持的媒体类型 415。

生成 API 不接受任意输出文件路径，不执行 Macro；由客户端保存响应内容。新建部署写入 mon.exe 同目录；已明确接管的既有条目按后端绑定更新原 UIC 路径和对应模块清单。API 不开放跨域头；开发网页通过代理访问。部署文件冲突为 409，并发部署为 423，权限错误为 403。

## 无前端导出

后端启动后，在另一个 PowerShell 中执行：

~~~powershell
$ribbonProject = Invoke-RestMethod 'http://127.0.0.1:4318/api/v1/projects/example'
$ribbonBody = $ribbonProject | ConvertTo-Json -Depth 30
Invoke-WebRequest 'http://127.0.0.1:4318/api/v1/uic/export' -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($ribbonBody)) -OutFile './ribbon.uic'
~~~

## 契约

完整定义在 packages/contracts/index.ts。根为 schemaVersion=1、namespace、buttons 数组、tabs 数组。

Button 包含 id、caption、macro、commandKey、image、tooltipText、tooltipTitle。image 为 null 或 {mediaType:"image/png"|"image/jpeg",base64:"..."}。Tab 包含 id、caption、groups；Group 包含 id、caption、items；Item 为 {buttonId,size:"large"|"small"}。

Tab、Group 和 Button 的标识在项目内唯一，按钮命令 Key 唯一。Tooltip 可为空，Macro 与显示名不能为空。合法 Macro 保留前后空白与换行。

normalize 补空的或省略的标识与 Key，既有值不变；布局引用必须指向最终已知按钮标识。网页先分配按钮 ID，再让 normalize 补 Key。export 不产生随机值，同一规范化项目得到相同字节。

输出根 XML 命名空间为 www.aveva.com，业务 Namespace 独立设置。按钮定义位于 Tools/ButtonTool，布局位于 Tabs/Tab/Groups/Group/Tools/Tool；LargeImage 显式写 True/False；Image 嵌入 Base64。TooltipFormattedText 将纯文本进行富文本转义再 XML 序列化。

## v0.3 导入、管理与持久化

- POST /uic/import：接受 {path} 读取明确的绝对路径，或 {bytesBase64,fileName} 上传字节，或 {xml,fileName}。返回 project、document、version，保存原始基线。
- POST /workspace/save：{project,kind:"edit"|"save",note}，返回持久化记录。文档标识保存在 project.documentId。
- GET /workspace/documents：文档列表。
- POST /workspace/versions：{documentId}，返回版本摘要。
- POST /workspace/open：{documentId,versionId?}，返回项目，省略版本读取最新。
- POST /workspace/restore：{documentId,versionId}，追加恢复版本并返回项目，不修改源文件。
- POST /workspace/original：{documentId,versionId}，下载该导入/接管基线原始字节。
- POST /workspace/context：{documentId}，返回后端托管绑定。
- POST /management/list：{monPath,manifestName}，返回含来源、权限、实际路径、加载状态、文件摘要和条目 key 的行。
- POST /management/adopt：{monPath,manifestName,key,expectedManifestHash,documentId?}，明确接管自定义项。可关联原始摘要匹配的当前导入文档，保留编辑。
- POST /management/toggle：{monPath,manifestName,key,expectedManifestHash,enabled}，移除或恢复托管加载，不删除 UIC。

以上路径均带 /api/v1 前缀。保护条目接管或移除返回 403，过期摘要/重复源路径部署返回 409。

部署目标为现有托管文档时，后端从绑定读取原 UIC 路径；不接受 HTTP 传入的内部 managed 字段。新建文档继续使用 mon.exe 同目录文件。存储默认 data/uic-workspace，可用 UIC_DATA_DIR 配置。

本机路径导入的已有 UIC 使用独立文件接口，更新不依赖模块选择：

- POST /deployment/modules：{project,monPath}，返回该安装的 modules（manifestName、entryNames、error），供穿梭框区分已有引用和未关联模块；扫描只读，支持相对与绝对 UIC 路径。

- POST /deployment/file-preview：{project}，以持久文档 sourcePath 为准，返回 uicPath、uicHash、nextUicHash、uicBytes、unchanged、references（各项含 monPath、manifestName、entryName、registrationPath）、scanErrors。
- POST /deployment/file-apply：{project,expectedUicHash}，备份并覆盖 UIC 一次，保存前后快照和关联信息，所有 XML 原字节保持不变。
- POST /deployment/registration-preview：{project,monPath,manifestName,entryName}，预览把已有文件加入模块。
- 预览同时返回 beforeXml、afterXml、beforeEntries、afterEntries、addedEntries、nextManifestHash、uicModified=false，前端据此显示实际加载登记变化；完整 XML 是后端最终补丁内容，保留原编码写入。
- POST /deployment/registration-apply：以上参数加 expectedManifestHash、expectedUicHash，只备份并修改所选 XML，不写 UIC。

关联收集扫描已发现安装、持久绑定及当前文档的安装提示；未读取的位置通过 scanErrors 返回，不宣称发现所有磁盘中的安装。已有 UIC 加入模块使用磁盘当前文件，草稿需单独确认更新。
