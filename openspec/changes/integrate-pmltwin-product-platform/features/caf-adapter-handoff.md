# CAF 薄适配入口 · 2026-10-31

任务5.7的独立切片：`src/PmlTwin.Caf.Adapter`为net48，自写最小真实SDK接口实现；不引用现代Database、Manager、AVEVA数据库、WPF或通信Host。只读入口`pmltwin.caf.readonly-entry`是适配器描述身份，不是注册CLI操作。构造和Start都不启动程序、不注册命令、不读取凭据、不写安装；启动管理器及任何业务动作仍需明确操作，本切片未实现自动启动。

## 真实接口证据

环境清单见`artifacts/platform/environment.json`。本轮通过PEReader/GetMetadataReader读取本机三份`Aveva.ApplicationFramework.dll`元数据，没有Assembly.Load/LoadFrom或执行vendor代码。读取到真实IAddin包含string Name/Description getter、Start(ServiceManager)、Stop()。AM的`C:/AVEVA/Marine/OH12.1.SP4/ApplicationFramework.xml`第213–237行及厂商Samples下ExamplesAddin也佐证该契约；未将厂商源码复制到仓库。

AM同一XML第72–99行描述AddinManager.Load(assemblyPath)、Unload(Addin)、LoadAddins、StartAddins、StopAddins；LoadAddins按应用名Addins.xml发现程序集，Start/Stop由宿主协调。AM样例DesignAddins.xml的ArrayOfString列程序集名。以上是静态SDK证据，不宣称各版本实际装配已通过，不修改任何现有Addins清单。

3.1编译直接证实IAddin已过时，错误建议IAddinInjected。继续读取真实元数据，确认IAddinInjected继承IAddin并新增Start(IDependencyResolver)，因而3.1构建采用该接口并保留继承的旧Start签名。不通过关掉obsolete错误将旧接口冒充推荐入口。

## 兼容矩阵

| Profile | 本机SDK根 | 精确身份 | 入口 | 当前边界 |
|---|---|---|---|---|
| E3D21 | C:/Program Files (x86)/AVEVA/Everything3D2.10 | CAF 1.2.2.0 / token 17c64733a9775004 | IAddin | 本机SDK编译及隔离检查；实机注册未实施 |
| E3D31 | C:/Program Files (x86)/AVEVA/Everything3D3.1 | CAF 4.0.0.0 / 同token | IAddinInjected | 本机SDK编译及隔离检查；实机注册未实施 |
| AM121 | C:/AVEVA/Marine/OH12.1.SP4 | CAF 1.1.20.0 / 同token | IAddin | net48静态编译，不据此承诺旧AM宿主运行net48；需实际CLR/宿主验收 |
| 其他 | 未确认 | 未确认 | 未支持 | 不推定兼容，不隐式选版本 |

每个Profile独立输出目录，编译引用完整版本/签名，SpecificVersion=true、Private=false。运行Start重验已加载接口程序集版本及token，不匹配拒绝，不可依赖bindingRedirect绕过检查。未知profile或缺失SDK明确构建失败。SDK原件不会复制到输出/发行包，SDK路径可用`-p:CafSdkRoot=...`覆盖。

## 生命周期与验证

Start验证身份与非空服务上下文后只改变内存状态，重复Start幂等；Stop幂等清理，Describe返回独立只读快照。无数据库、外部资源或业务受理，Stop不会取消别处已经受理的操作。不宣称应用内卸载等同CLR卸载。

隔离测试项目`tests/PmlTwin.Caf.Adapter.Tests`将自写纯生命周期源码链接到net10 helper，测重复Start/Stop、快照、缺服务、错误版本/签名。编译产物检查只用PEReader：验证真实接口引用及精确SDK版本、仅mscorlib和CAF程序集依赖、输出无复制vendor DLL。测试不在真实AVEVA进程注册，不加载vendor程序集，不冒充实际服务注入/UI挂载通过。

复现：先`./scripts/Use-YuzuhaDotnet.ps1`配置SDK；对E3D21/E3D31/AM121分别执行`dotnet build src/PmlTwin.Caf.Adapter/PmlTwin.Caf.Adapter.csproj -c Release -p:CafProfile=<profile> -m:1`；再`dotnet test tests/PmlTwin.Caf.Adapter.Tests/PmlTwin.Caf.Adapter.Tests.csproj -c Release -m:1`。真实SDK未安装时构建/产物测试会失败，不能以假接口替换。

任务5.7仍有实际注册/真实宿主Start/Stop与UI入口验收缺口，未改tasks勾选；本次不部署。最终构建与测试结果由本轮执行结果补在下文。

实际结果：三个profile Release真实SDK构建均0警告/0错误；隔离生命周期/PE依赖/接口装配元数据检查7/7通过（33ms）。3.1检查同时核对继承的IAddin和IAddinInjected两个元数据实现。将CafSdkRoot设置为不存在目录的负例按预期失败，输出“Local CAF SDK missing. Set CafSdkRoot; vendor assemblies are never downloaded or redistributed.”，无mock fallback。首次NuGet恢复因离线漏洞源NU1900失败，使用本机缓存与命令级`-p:NuGetAudit=false`完成测试包恢复，未改仓库漏洞策略；真实SDK编译不引入新包。沙箱testhost停滞后中断，受控批准的同一`--no-build --no-restore`测试命令通过。
