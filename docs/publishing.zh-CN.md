# Net10 / Net48 构建与发布

## 已验证组合

| 目标 | 结果 | 备注 |
|---|---|---|
| Net10 framework-dependent | 通过，0 警告/0 错误 | 需要目标机安装 .NET 10 |
| Net10 win-x64 self-contained single-file | 通过并启动验证 | 不需要目标机安装 .NET 10 |
| Net10 win-x64 Native AOT | 代码分析通过，最终链接未完成 | 构建机缺少 Visual Studio C++ linker |
| Net48 x86 PMLNet host | 通过 | 使用 AVEVA Everything3D 2.10 程序集 |

## 普通 Net10

```powershell
dotnet restore src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj --configfile src\NuGet.config
dotnet build src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj -c Release --no-restore
```

## 免安装 .NET 10

推荐兼容性优先的 self-contained single-file：

```powershell
dotnet publish src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj `
  -p:PublishProfile=win-x64-self-contained
```

输出为单个 `YuzuhaToolkit.Mcp.exe`。目标 Windows x64 机器不需要安装 .NET 10。

## Native AOT

项目已将工具响应的 JSON 序列化改为 source-generated metadata，并提供：

```powershell
dotnet publish src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj `
  -p:PublishProfile=win-x64-nativeaot
```

构建机必须安装 Visual Studio 的 **Desktop Development for C++** 工作负载。当前审查机
没有 `link.exe`，所以没有把未经验证的 AOT 二进制放入交付包。安装工具链后仍需继续验证
PlantHost.Rpc 动态代理和 MCP 工具发现的运行时行为。

## Net48

```powershell
dotnet msbuild src\YuzuhaToolkit.PmlHost.Net48\YuzuhaToolkit.PmlHost.Net48.csproj `
  /t:Build /p:Configuration=Release `
  /p:AvevaInstallDir=C:\path\to\AVEVA
```

AVEVA 专有程序集只从本机安装目录解析，不进入开源仓库或交付包。
