namespace LaunchDesk.Bootstrap;

/// <summary>供隔离验证替换外部副作用端口；为空时使用 Windows 生产适配器。</summary>
/// <param name="Secrets">测试可替换的凭据端口</param>
/// <param name="Probe">最近探测快照/可替换探针端口</param>
/// <param name="Processes">测试可替换的进程端口</param>
/// <param name="Environment">子进程环境或可替换的变量存储端口</param>
/// <param name="Privilege">可替换的有限管理员执行端口</param>
public sealed record DesktopAdapters(ISecretVault? Secrets = null, IResourceProbe? Probe = null,
    IProcessGateway? Processes = null, IEnvironmentStore? Environment = null, IPrivilegeBroker? Privilege = null);
