# 构建配置

中文 | [English](build-configuration.md)

仓库包含可再分发的 RPC、JSON、NuGet 和所需参考程序集。AVEVA 程序集不会重新分发，
必须由用户已获得许可的本机安装提供。

## 配置 AVEVA SDK 路径

以下方式任选一种：

1. 将 `src/build/Aveva.Local.props.example` 复制为
   `src/build/Aveva.Local.props`，然后设置 `AvevaInstallDir`。
2. 设置环境变量 `AVEVA_INSTALL_DIR`。
3. 调用 MSBuild 时传入 `/p:AvevaInstallDir=C:\path\to\AVEVA`。

`Aveva.Local.props` 被 Git 忽略。路径不存在或不含 `PMLNet.dll` 时，构建会提前给出
明确错误。Rider 用户修改该文件后应重新加载解决方案，以刷新代码补全和设计时引用。

```powershell
dotnet msbuild <project.csproj> `
  /p:AvevaInstallDir="D:\AVEVA\Everything3D2.10"
```

## 构建 MCP 与 Native AOT

```powershell
dotnet restore src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj `
  --configfile src\NuGet.config

dotnet build src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj `
  -c Release --no-restore

dotnet publish src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj `
  -p:PublishProfile=win-x64-nativeaot --no-restore
```

构建 Native AOT 的机器需要 Visual Studio C++ Desktop 工作负载；生成的 EXE
不要求目标机安装 .NET 10。

## 构建 Net48 Host

```powershell
dotnet msbuild `
  src\YuzuhaToolkit.PmlHost.Net48\YuzuhaToolkit.PmlHost.Net48.csproj `
  /t:Build /p:Configuration=Release `
  /p:AvevaInstallDir=C:\path\to\AVEVA
```

只把项目自身的 Host 和可再分发依赖放入 Release；不要复制 AVEVA、PMLNet、
ForeignLanguage 或 Infragistics 专有程序集。
