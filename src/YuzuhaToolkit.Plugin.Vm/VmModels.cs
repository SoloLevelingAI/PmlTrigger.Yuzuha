using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Plugins.Vm;

/// <summary>显式有状态动作；快照还原不会隐式启动虚拟机。</summary>
public sealed record VmTaskStep(string Action, string? Snapshot = null);
/// <summary>从原实验模型抽取的目标与证据范围，秘密只通过本插件拥有的保护文件引用。</summary>
public sealed record VmTaskRequest(string AllowedVmRoot, string VmPath, string ExpectedUuid, string ExpectedMac,
    string Profile, string OutputDirectory, VmTaskStep[] Steps, string VixLibraryPath, string? CredentialFile = null, int TimeoutSeconds = 30);
/// <summary>只读 VMX 身份与快照候选，不代表 VMware 已打开或已启动。</summary>
public sealed record VmCandidate(string VmPath, string Uuid, string Mac, bool HostOnly, string[] Snapshots, string? Warning);
/// <summary>冻结 VM、快照清单、厂商库、产品、输出目录与步骤的执行预览。</summary>
public sealed record VmTaskPlan(VmTaskRequest Request, string VmHash, string? SnapshotFile, string? SnapshotHash,
    string VixHash, string CredentialTargetKey, string? CredentialHash, string Confirmation, string[] Effects);
/// <summary>逐步观察结果，不从电源成功推定 AVEVA 或许可成功。</summary>
public sealed record VmStepEvidence(int Index, string Action, string State, int? PowerState, int? ToolsState);
/// <summary>关联证据回收；unknown 不能自动重复执行。</summary>
public sealed record VmTaskEvidence(string AttemptId, string Confirmation, string Profile, string State, bool MayHaveExecuted,
    string PlanFile, string ResultFile, VmStepEvidence[] Steps, string Message);
/// <summary>明确模拟的 Host 结果，不能作为真实连接或许可证据。</summary>
public sealed record VmHostSimulation(string Mode, string Profile, string Scenario, string HostFramework, int FormQueryVersion,
    int BoundedArrayReadVersion, string Outcome, bool RealConnectionVerified, string CheckoutState);

/// <summary>独立插件的 AOT JSON 元数据。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(VmTaskRequest))]
[JsonSerializable(typeof(VmTaskPlan))]
[JsonSerializable(typeof(VmCandidate[]))]
[JsonSerializable(typeof(VmTaskEvidence))]
[JsonSerializable(typeof(VmHostSimulation))]
[JsonSerializable(typeof(VmProtectedCredential))]
[JsonSerializable(typeof(VmCredentialReceipt))]
public partial class VmJsonContext : JsonSerializerContext;

/// <summary>迁移原实验的允许根、UUID/MAC、无链接、Host-only 和确认指纹约束。</summary>
public static class VmPlanning
{
    /// <summary>只读 VMX/快照元数据，不加载 VIX 或运行 VMware。</summary>
    public static VmCandidate Inspect(string file)
    {
        file = Local(file); var values = ReadAssignments(file);
        var uuid = values.GetValueOrDefault("uuid.bios") ?? "";
        var mac = values.GetValueOrDefault("ethernet0.generatedAddress") ?? values.GetValueOrDefault("ethernet0.address") ?? "";
        var adapters = values.Keys.Where(k => Regex.IsMatch(k, @"^ethernet\d+\.present$", RegexOptions.CultureInvariant)).Where(k => values[k].Equals("TRUE", StringComparison.OrdinalIgnoreCase)).ToArray();
        var hostOnly = adapters.Length > 0 && adapters.All(k => values.GetValueOrDefault(k[..^8] + ".connectionType")?.Equals("hostonly", StringComparison.OrdinalIgnoreCase) == true);
        var snapshots = SnapshotNames(Path.ChangeExtension(file, ".vmsd"));
        return new(file, uuid, mac, hostOnly, snapshots, uuid.Length == 0 || mac.Length == 0 ? "VMX 缺少可核验 UUID/MAC。" : !hostOnly ? "所有启用网卡必须为 Host-only。" : null);
    }

    /// <summary>限定根、深度与数量的静态发现；不会扫描虚拟磁盘正文。</summary>
    public static VmCandidate[] Discover(string[] roots, int maxDepth = 3, CancellationToken token = default)
    {
        if (roots.Length is < 1 or > 16 || maxDepth is < 0 or > 6) throw new ArgumentException("VM 发现范围无效。");
        var found = new List<VmCandidate>(); var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var pending = new Queue<(string Path, int Depth)>();
        foreach (var root in roots) pending.Enqueue((Local(root), 0));
        while (pending.TryDequeue(out var item))
        {
            token.ThrowIfCancellationRequested(); if (!visited.Add(item.Path)) continue;
            if (visited.Count > 2000 || found.Count >= 200) throw new IOException("VM 发现超过预算，请缩小选定范围。");
            PathRules.NoLinks(item.Path);
            foreach (var file in Directory.EnumerateFiles(item.Path, "*.vmx")) found.Add(Inspect(file));
            if (item.Depth < maxDepth) foreach (var directory in Directory.EnumerateDirectories(item.Path))
            { PathRules.NoLinks(directory); pending.Enqueue((directory, item.Depth + 1)); }
        }
        return found.ToArray();
    }

    /// <summary>生成具体可审阅任务，不创建输出目录、不读取明文秘密或连接 SDK。</summary>
    public static VmTaskPlan Preview(VmTaskRequest request)
    {
        var root = Local(request.AllowedVmRoot); var vm = Local(request.VmPath); var output = Local(request.OutputDirectory); var library = Local(request.VixLibraryPath);
        if (!Directory.Exists(root) || root.Equals(Path.TrimEndingDirectorySeparator(Path.GetPathRoot(root)!), StringComparison.OrdinalIgnoreCase) ||
            !Contains(root, vm) || !vm.EndsWith(".vmx", StringComparison.OrdinalIgnoreCase) || Contains(root, output) || Contains(output, root) || File.Exists(output) ||
            !Path.GetFileName(library).Equals("vix.dll", StringComparison.OrdinalIgnoreCase) || request.TimeoutSeconds is < 1 or > 90 || request.Steps is null || request.Steps.Length is < 1 or > 8)
            throw new ArgumentException("VM、证据输出、VIX 路径或任务预算无效。");
        _ = Framework(request.Profile); var candidate = Inspect(vm);
        if (string.IsNullOrWhiteSpace(request.ExpectedUuid) || string.IsNullOrWhiteSpace(request.ExpectedMac) || candidate.Warning is not null ||
            !candidate.Uuid.Equals(request.ExpectedUuid, StringComparison.OrdinalIgnoreCase) || !candidate.Mac.Equals(request.ExpectedMac, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("VM 身份或 Host-only 配置不匹配；未连接 VMware。");
        var steps = request.Steps.Select(step => step is null ? throw new ArgumentException("VM 步骤为空。") : step with { }).ToArray();
        foreach (var step in steps)
        {
            if (step.Action is not ("probe" or "start" or "stop" or "createSnapshot" or "revertSnapshot")) throw new ArgumentException("VM 动作未支持。");
            if (step.Action is "createSnapshot" or "revertSnapshot")
            {
                if (step.Snapshot is null || !Regex.IsMatch(step.Snapshot, @"^[A-Za-z0-9][A-Za-z0-9 _.\-]{0,99}$", RegexOptions.CultureInvariant)) throw new ArgumentException("快照名称无效。");
                var matches = candidate.Snapshots.Count(s => s == step.Snapshot);
                if (step.Action == "revertSnapshot" && matches != 1) throw new InvalidDataException("选定快照缺失或名称不唯一。");
                if (step.Action == "createSnapshot" && matches > 0) throw new InvalidDataException("同名快照已存在。");
            }
            else if (step.Snapshot is not null) throw new ArgumentException("该动作不接受快照参数。");
        }
        request = request with { AllowedVmRoot = root, VmPath = vm, OutputDirectory = output, VixLibraryPath = library,
            CredentialFile = request.CredentialFile is null ? null : Local(request.CredentialFile), Steps = steps };
        var snapshotFile = Path.ChangeExtension(vm, ".vmsd"); if (!File.Exists(snapshotFile)) snapshotFile = null;
        var unsigned = new VmTaskPlan(request, Hash(vm), snapshotFile, snapshotFile is null ? null : Hash(snapshotFile), Hash(library),
            VmCredentials.TargetKey(vm, request.ExpectedUuid, request.ExpectedMac), request.CredentialFile is null ? null : Hash(request.CredentialFile),
            "", steps.Select(s => s.Action + (s.Snapshot is null ? "" : ":" + s.Snapshot)).ToArray());
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(unsigned, VmJsonContext.Default.VmTaskPlan))).ToLowerInvariant();
        return unsigned with { Confirmation = fingerprint };
    }

    /// <summary>复查完整冻结任务；任何身份、快照、厂商库或输出变化均拒绝。</summary>
    public static void Verify(VmTaskPlan plan, string confirmation)
    {
        var fresh = Preview(plan.Request);
        if (confirmation != plan.Confirmation || !JsonSerializer.SerializeToUtf8Bytes(fresh, VmJsonContext.Default.VmTaskPlan).SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(plan, VmJsonContext.Default.VmTaskPlan)))
            throw new IOException("VM 任务或确认指纹变化，请重新预览。");
    }
    /// <summary>支持的三个产品配置，分别标明 Host 框架。</summary>
    public static string Framework(string profile) => profile switch { "AM" or "PDMS" => "net35", "E3D2.1" or "E3D3.1.6" => "net48", _ => throw new ArgumentException("未知产品 profile。") };
    private static string[] SnapshotNames(string file) => !File.Exists(file) ? [] : ReadAssignments(file).Where(p => Regex.IsMatch(p.Key, @"^snapshot\d+\.displayName$", RegexOptions.CultureInvariant)).Select(p => p.Value).ToArray();
    private static Dictionary<string, string> ReadAssignments(string file)
    {
        PathRules.NoLinks(file); using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 2 * 1024 * 1024) throw new IOException("VM 元数据超过 2 MiB 预算。");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true); var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (reader.ReadLine() is { } line)
        {
            var match = Regex.Match(line, "^\\s*(?<key>[A-Za-z0-9_.]+)\\s*=\\s*\"(?<value>[^\"]*)\"\\s*$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            if (match.Success && !values.TryAdd(match.Groups["key"].Value, match.Groups["value"].Value)) throw new InvalidDataException("VM 元数据有重复字段。");
        }
        return values;
    }
    internal static string Local(string path) { if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal)) throw new ArgumentException("请明确选择本机绝对路径。"); path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); PathRules.NoLinks(path); return path; }
    internal static string Hash(string file) { PathRules.NoLinks(file); using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    private static bool Contains(string root, string path) => path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
