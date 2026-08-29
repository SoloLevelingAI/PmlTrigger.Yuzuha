# Net10 / Net48 构建与发布

## 已验证组合

| 目标 | 结果 | 备注 |
|---|---|---|
| Net10 framework-dependent | 通过，0 警告/0 错误 | 需要目标机安装 .NET 10 |
| Net10 win-x64 self-contained single-file | 通过并启动验证 | 不需要目标机安装 .NET 10 |
| Net10 win-x64 Native AOT | 通过，0 警告/0 错误；启动与 MCP 握手验证通过 | 无需目标机安装 .NET 10 |
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

项目使用 `PlantHost.Rpc.Net10`、显式 RPC 路由以及 source-generated JSON
metadata，不含 `DispatchProxy`、Newtonsoft.Json 或运行时反射代理。发布命令：

```powershell
dotnet publish src\YuzuhaToolkit.Mcp\YuzuhaToolkit.Mcp.csproj `
  -p:PublishProfile=win-x64-nativeaot
```

构建机必须安装 Visual Studio 的 **Desktop Development for C++** 工作负载。
输出目录为 `artifacts/publish/win-x64-nativeaot`。已验证原生程序能够完成
MCP initialize、tools/list、`get_connection_status` 调用、连续心跳状态转换和
正常关闭。

另外使用 `YUZUHA_PML_PIPE=yuzuha.pml.command.v1.codex-aot-smoke` 隔离测试
Pipe，与不依赖 AVEVA 的 NET48 冒烟宿主完成真实 RPC 往返：心跳状态为
`Connected`、连续失败为 0、`run_pml_command` 返回 `Code=SMOKE_OK`。真实
AVEVA 主线程执行仍应在安装 E3D/PMLNet 的目标环境验收。

## Net48

```powershell
dotnet msbuild src\YuzuhaToolkit.PmlHost.Net48\YuzuhaToolkit.PmlHost.Net48.csproj `
  /t:Build /p:Configuration=Release `
  /p:AvevaInstallDir=C:\path\to\AVEVA
```

AVEVA 专有程序集只从本机安装目录解析，不进入开源仓库或交付包。
