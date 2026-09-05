# 构建配置
> 中文版（供作者审阅）。英文版 / English: [build-configuration.md](build-configuration.md)

AVEVA 程序集是专有的前置条件，本项目绝不提交（commit）或重新分发它们。

## AVEVA Profile

| Profile | 宿主 | AVEVA 工具程序集 |
|---|---|---|
| `AM` | .NET Framework 3.5 x86 | `Aveva.Pdms.Utilities.dll` |
| `PDMS` | .NET Framework 3.5 x86 | `Aveva.Pdms.Utilities.dll` |
| `E3D2.1` | .NET Framework 4.8 x86 | `Aveva.Core.Utilities.dll` |
| `E3D3.1.0` | .NET Framework 4.8 x86 | `Aveva.Core.Utilities.dll` |
| `E3D3.1.6` | .NET Framework 4.8 x86 | `Aveva.Core.Utilities.dll` |

使用以下命令构建全部 Profile：

```powershell
.\scripts\Build-AvevaProfiles.ps1 `
  -ProfileRoot 'D:\AVEVA\AvevaProfile'
```

该脚本会把可直接加载的输出写入 `runtime/profiles/<profile>/<framework>`，并把 MCP 写入 `runtime/net10`。AVEVA 的 `PMLNet.dll` 和工具程序集仅作引用，不会被复制。MCP 的输出是一个精简的、自包含的 Windows x64 可执行文件：`runtime/net10/YuzuhaToolkit.Mcp.exe`。PlantHost.Rpc、其 RPC 契约以及 Newtonsoft.Json 被设置为裁剪根（root），因为它们的动态代理与序列化路径使用了反射；移除这些根不是受支持的体积优化方式。

若只构建一个宿主，请把 `AvevaProfileRoot` 和 `AvevaProfile` 传给 MSBuild：

```powershell
dotnet msbuild src\YuzuhaToolkit.PmlHost.Net48\YuzuhaToolkit.PmlHost.Net48.csproj `
  /t:Build /p:Configuration=Release `
  /p:AvevaProfileRoot='D:\AVEVA\AvevaProfile' `
  /p:AvevaProfile='E3D3.1.6'
```

直接使用 `AvevaInstallDir`、`AVEVA_INSTALL_DIR` 以及本地 `src/build/Aveva.Local.props` 配置的方式仍然受支持，适用于单个 Profile。

## 本地一次性 Profile 构建

当某个 AVEVA 版本没有预构建的 Profile 时，可针对用户自己的安装环境构建一个本地命名的 Profile。只有 NET35/NET48 PMLNet 宿主采用这种方式构建——它是唯一与 AVEVA 版本绑定的组件。Net10 MCP 和 Knowledge 服务器完全不依赖 AVEVA，绝不会在用户机器上重新构建：

```powershell
.\scripts\Build-LocalHost.ps1 `
  -AvevaInstallDir 'C:\AVEVA\Everything3D' `
  -ProfileName 'E3D3.2.0' `
  -OutputRoot '<install>\runtime\profiles'
```

该脚本会根据 `PMLNet.dll` 旁边的工具程序集 DLL 推导出框架家族（E3D→net48，AM/PDMS→net35），拒绝遮蔽预构建 Profile 的名称，打印再分发/兼容性警告，并选择匹配的宿主项目。它需要有源码检出（clone 或源码发布包），因为 Agent 安装包只附带二进制文件；skill 参考 `skill/references/local-build.md` 描述了 Agent 侧的流程：告知用户、收集环境信息、征得同意、说明风险、执行构建，然后用 `set Yuzuha=<name>` 选择该 Profile 并验证 `GetRpcServerStatus()`。

## Knowledge 服务器

`src/YuzuhaToolkit.Knowledge` 以 Native AOT 单文件可执行程序的形式发布（需要 Visual Studio C++ 链接器）：

```powershell
dotnet publish src\YuzuhaToolkit.Knowledge\YuzuhaToolkit.Knowledge.csproj `
  -c Release -r win-x64
```

`Microsoft.Data.Sqlite` 捆绑了包含 FTS5 的 `e_sqlite3.dll`；该文件随 exe 一起发布，位于 `runtime\net10` 目录中。

## 运行时 Profile 选择

在启动 AVEVA 之前，设置所选的构建 Profile：

```bat
set Yuzuha=E3D2.1
```

有效取值为 `AM`、`PDMS`、`E3D2.1`、`E3D3.1.0` 和 `E3D3.1.6`。该自定义 EVAR 变量名严格为 `Yuzuha`（不含下划线）。PML 引导程序使用该值来选择匹配的 DLL。
