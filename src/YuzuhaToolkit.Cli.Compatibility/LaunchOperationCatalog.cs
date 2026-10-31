using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PmlTwin.Phayton;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Mcp;

/// <summary>配置操作的结构化失败诊断；版本冲突携带当前修订，校验失败携带字段错误。</summary>
public sealed record LaunchOperationDiagnostic(string? ActualRevision = null, LaunchValidation? Validation = null);

/// <summary>已创建启动入口进程的回执，不代表 AVEVA 登录或 Host 身份核验成功。</summary>
public sealed record LaunchProcessReceipt(int ProcessId, string ProfileId, string Revision, string State);

/// <summary>启动操作附加回执的 Native AOT 元数据。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(LaunchOperationDiagnostic))]
[JsonSerializable(typeof(LaunchProcessReceipt))]
public partial class LaunchOperationJsonContext : JsonSerializerContext;

/// <summary>旧 CLI 的可选启动操作组合层；通信 Core 不引用管理服务。</summary>
public static class LaunchOperationCatalog
{
    /// <summary>将既有调用转接到相同管理服务，保留输入输出与授权语义。</summary>
    public static IEnumerable<OperationRegistration> Registrations(LaunchProfileService? service = null,
        IProductModuleCatalog? modules = null, Func<LaunchProfile, string, int>? startProcess = null)
        => YuzuhaToolkit.Manager.LaunchOperationCatalog.Registrations(service, modules, startProcess);
}
