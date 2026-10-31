# Design

WPF 客户端位于 apps/desktop，采用已在样例环境验证的 .NET Framework 4.8 与 WPF，使用系统编译器及框架内 HTTP/JSON 实现，无第三方 NuGet 依赖。提供正式 csproj 与 Node 构建入口。

Models 仅声明客户端契约；Services/ApiClient 只处理 HTTP、JSON、错误响应；Views 负责原生窗口、编辑状态与渲染。C# 不引用后端 TypeScript 包，不直接写 AVEVA 清单。可在后端独立运行时直接启动 EXE 并指定 --api。

OfficeRibbon.xaml 是原样复制的已验证设计资源，OfficeIcons.xaml 从同一验收样例的按钮视觉树抽取。界面重新使用原生布局容器与真实业务数据，避免按固定设计节点编号编辑业务。

自动草稿通过队列和文档代次串行写入后端；加载历史不会立即覆盖实际 UIC。API JSON 序列化省略可选 null origin/sourceId/preferredBinding/documentId，以保持 TypeScript 的可选字段语义。

启动器复用健康的独立后端；未运行时以独立隐藏进程创建，仅负责清理它自己创建的后端。EXE 本身不要求 Node 或后端源文件，只要求可访问 API。开发环境保留既有数据目录，正式交付可通过 UIC_DATA_DIR 指定外部持久目录。

隔离验证使用临时工作区内模拟 mon.exe 和模块清单，不调用 AVEVA，不执行 Macro。验证包括 WPF 窗口渲染、数据编辑与实际 API 部署往返；另外使用原生 UI 自动化检查真实桌面窗口。
