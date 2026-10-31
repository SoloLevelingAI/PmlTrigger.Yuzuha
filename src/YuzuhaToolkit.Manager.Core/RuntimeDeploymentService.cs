using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>明确选择的运行载荷和产品环境；Mode 为 copy 或 reference。</summary>
public sealed record RuntimeDeploymentRequest(string PackageRoot, string RuntimeRoot, string Mode,
    string InstallationRoot, string Product, string Profile, string Framework, string? EnvironmentFile = null, int? CodePage = null,
    string? InstallationExecutable = null, string? ExpectedInstallationId = null);

/// <summary>受管运行目录的文件所有权；仅保存程序文件指纹。</summary>
public sealed record RuntimeOwnership(int SchemaVersion, string Owner, string Root, Dictionary<string, string> Files);

/// <summary>载荷与环境的同次冻结预览。</summary>
public sealed record RuntimeDeploymentPlan(RuntimeDeploymentRequest Request, string ManifestHash, DeploymentPlan? Copy,
    EvarsEditPlan Environment, string? MarkerBeforeHash, string? MarkerBeforeBase64, string? MarkerAfterBase64, string BootstrapToken,
    ManagerInstallationIdentity Installation);

/// <summary>用于条件恢复本次载荷、环境和所有权标记的回执。</summary>
public sealed record RuntimeDeploymentReceipt(string Id, RuntimeDeploymentPlan Plan, DeploymentReceipt? CopyReceipt,
    EvarsEditReceipt EnvironmentReceipt, string TransactionDirectory);

/// <summary>失败阶段、可定位记录和恢复待处理项。</summary>
public sealed record RuntimeDeploymentDiagnostic(string Phase, string TransactionDirectory, string[] RecoveryErrors);

/// <summary>运行部署失败；保留部分恢复结果供 GUI/CLI 诊断。</summary>
public sealed class RuntimeDeploymentException(string message, RuntimeDeploymentDiagnostic diagnostic, Exception inner) : IOException(message, inner)
{
    /// <summary>实际失败阶段与恢复结果。</summary>
    public RuntimeDeploymentDiagnostic Diagnostic { get; } = diagnostic;
}

/// <summary>共用既有复制事务；将运行文件、引导适配和环境登记作为一次可恢复管理任务。</summary>
public sealed class RuntimeDeploymentService(string? stateDirectory = null, string? userDirectory = null, Action<string>? onPhase = null)
{
    private const string Marker = ".yuzuha-runtime.json";
    private const string Owner = "Yuzuha.Manager.Runtime/1";
    private const string Bootstrap = "PMLLIB/Bootstrap/YuzuhaResolveRuntimePath.pmlfnc";
    private readonly string state = Path.GetFullPath(stateDirectory ?? WorkspaceSettings.StateDirectory);
    private readonly string user = Path.GetFullPath(userDirectory ?? UserDataPaths.Root);

    /// <summary>只读取包清单和目标，不创建运行目录或修改环境。</summary>
    public RuntimeDeploymentPlan Preview(RuntimeDeploymentRequest request)
    {
        var installation = ManagerInstallationDiscovery.Select(request.InstallationRoot, request.Product, request.InstallationExecutable, request.EnvironmentFile);
        if (request.ExpectedInstallationId is { } expectedId && expectedId != installation.Id)
            throw new IOException("产品安装身份与所选发现结果不一致，请重新发现并预览。");
        var source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.PackageRoot));
        var target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.RuntimeRoot));
        PathRules.NoLinks(source); PathRules.NoLinks(target);
        if (target.Contains("PMLUI", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("运行根路径不能含 PMLUI 字样，原生引导会将它当作 UI 后缀移除。");
        if (request.Mode is not ("copy" or "reference")) throw new ArgumentException("部署模式必须是 copy 或 reference。");
        if (request.Mode == "reference" && !source.Equals(target, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("引用模式的运行根必须是所选包自身目录。");
        var framework = request.Profile is "AM" or "PDMS" ? "net35" :
            request.Profile is "E3D2.1" or "E3D3.1.6" ? "net48" : throw new ArgumentException("所选包没有已支持的 Host profile 映射。");
        if (request.Framework != framework || ((request.Product == "E3D") != (framework == "net48")) ||
            (request.Product != "E3D" && request.Product != request.Profile)) throw new ArgumentException("产品、profile 与框架不匹配。");
        var manifestFile = Path.Combine(source, ".setup-payload.json"); PathRules.NoLinks(manifestFile);
        if (new FileInfo(manifestFile).Length > 16 * 1024 * 1024) throw new IOException("包清单超过预算。");
        var manifestBytes = File.ReadAllBytes(manifestFile);
        using var manifest = JsonDocument.Parse(manifestBytes);
        var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest.RootElement.GetProperty("files").EnumerateArray())
        {
            var relative = entry.GetProperty("path").GetString()!.Replace('\\', '/');
            if (relative.Split('/').Any(p => p is "" or "." or "..") || relative.Contains(':') || Path.IsPathRooted(relative))
                throw new IOException("包清单包含非规范相对路径。");
            if (!IsRuntimeFile(relative)) continue;
            if (Path.GetFileName(relative).Equals("pml.index", StringComparison.OrdinalIgnoreCase) ||
                new[] { ".sqlite3", ".db", "-wal", "-shm" }.Any(e => relative.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                throw new IOException("运行载荷不能包含数据库或生成索引。");
            var sha = entry.GetProperty("sha256").GetString()!;
            if (sha.Length != 64 || !sha.All(Uri.IsHexDigit) || !expected.TryAdd(relative, sha))
                throw new IOException("包清单有重复文件或非法 SHA256。");
            var file = PathRules.Inside(source, relative); PathRules.NoLinks(file);
            if (!string.Equals(Hash(file), sha, StringComparison.OrdinalIgnoreCase)) throw new IOException("包载荷指纹不匹配：" + relative);
        }
        var host = "runtime/profiles/" + request.Profile + "/" + framework + "/YuzuhaToolkit.PmlHost.Net" + (framework == "net35" ? "35" : "48") + ".dll";
        if (!expected.ContainsKey(host) || !expected.ContainsKey(Bootstrap) ||
            !expected.ContainsKey("runtime/cli/PmlTwin.Phayton.Cli.exe") || !expected.Keys.Any(p => p.StartsWith("PMLUI/", StringComparison.OrdinalIgnoreCase)))
            throw new IOException("包缺少选定 Host、通信 CLI、引导函数或 PMLUI 载荷。");
        var bootstrapFile = PathRules.Inside(source, Bootstrap);
        var bootstrapBytes = File.ReadAllBytes(bootstrapFile);
        if (!Digest(bootstrapBytes).Equals(expected[Bootstrap], StringComparison.OrdinalIgnoreCase)) throw new IOException("引导源码在读取期间变化。");
        var bootstrapText = new UTF8Encoding(false, true).GetString(bootstrapBytes).TrimStart('\ufeff');
        var match = Regex.Match(bootstrapText, @"(?m)^\s*!folderName\s*=\s*'([A-Za-z0-9]+)'\s*$");
        if (!match.Success || Regex.Matches(bootstrapText, @"(?m)^\s*!folderName\s*=").Count != 1)
            throw new IOException("无法唯一定位运行目录引导 token。");
        var token = match.Groups[1].Value;
        string? replacement = null;
        if (!target.Contains(token, StringComparison.OrdinalIgnoreCase))
        {
            if (request.Mode == "reference") throw new IOException("当前包的引导 token 不匹配自身路径；请使用独立复制并预览适配。");
            var prefix = Regex.Match(Path.GetFileName(target).ToUpperInvariant(), "^[A-Z0-9]{3,}").Value;
            if (prefix.Length < 3) throw new IOException("自定义运行目录需要至少三个开头的 ASCII 字母或数字以适配原生引导。");
            token = prefix[..Math.Min(6, prefix.Length)];
            var transformed = bootstrapText[..match.Groups[1].Index] + token + bootstrapText[(match.Groups[1].Index + match.Groups[1].Length)..];
            replacement = Convert.ToBase64String(new UTF8Encoding(false).GetBytes(transformed));
        }
        DeploymentPlan? copy = null; string? markerBefore = null; string? markerAfter = null;
        var markerFile = Path.Combine(target, Marker);
        var owned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (request.Mode == "copy")
        {
            foreach (var protectedRoot in new[] { user, state })
                if (UserDataPaths.Contains(target, protectedRoot) || UserDataPaths.Contains(protectedRoot, target))
                    throw new IOException("运行目录必须与用户资料和管理状态目录分离。");
            for (var dir = new DirectoryInfo(target); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, UserDataPaths.MarkerName))) throw new IOException("不能向用户资料目录部署程序。");
            if (File.Exists(Path.Combine(target, ".yuzuha-inno.json"))) throw new IOException("目标由既有安装器管理，请使用其更新流程或选择新的运行目录。");
            if (File.Exists(markerFile))
            {
                PathRules.NoLinks(markerFile); var bytes = File.ReadAllBytes(markerFile); markerBefore = Convert.ToBase64String(bytes);
                var prior = JsonSerializer.Deserialize(bytes, ManagerJsonContext.Default.RuntimeOwnership) ?? throw new IOException("运行目录标记损坏。");
                if (prior.SchemaVersion != 1 || prior.Owner != Owner || !Path.GetFullPath(prior.Root).Equals(target, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("运行目录所有权标记不匹配。");
                owned = new(prior.Files, StringComparer.OrdinalIgnoreCase);
            }
            else if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
                throw new IOException("非空目标不是管理器的运行目录，拒绝覆盖旧文件。");
            copy = PluginDeployment.PreviewSelection(source, target, expected.Keys);
            copy = copy with { Files = copy.Files.Select(f => f.RelativePath.Equals(Bootstrap, StringComparison.OrdinalIgnoreCase)
                ? f with { ReplacementBase64 = replacement } : f).ToArray() };
            foreach (var file in copy.Files)
            {
                if (file.TargetHash is not null && file.TargetHash != file.DesiredHash &&
                    (!owned.TryGetValue(file.RelativePath, out var prior) || !file.TargetHash.Equals(prior, StringComparison.OrdinalIgnoreCase)))
                    throw new IOException("目标被用户修改或不属于原载荷，未覆盖：" + file.RelativePath);
                owned[file.RelativePath] = file.DesiredHash;
            }
            markerAfter = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new RuntimeOwnership(1, Owner, target, owned), ManagerJsonContext.Default.RuntimeOwnership));
        }
        var environment = installation.EnvironmentFile!;
        var edit = EvarsEditor.Preview(environment, target, request.Profile, request.Framework, request.CodePage);
        return new(request with { PackageRoot = source, RuntimeRoot = target, InstallationRoot = Path.GetFullPath(request.InstallationRoot),
            EnvironmentFile = edit.File, CodePage = edit.CodePage, InstallationExecutable = installation.Executable, ExpectedInstallationId = installation.Id }, Digest(manifestBytes), copy, edit,
            markerBefore is null ? null : Digest(Convert.FromBase64String(markerBefore)), markerBefore, markerAfter, token, installation);
    }

    /// <summary>先复制受管载荷再登记环境；失败时按原回执逆序恢复。</summary>
    public RuntimeDeploymentReceipt Apply(RuntimeDeploymentPlan plan)
    {
        var fresh = Preview(plan.Request);
        if (JsonSerializer.Serialize(fresh, ManagerJsonContext.Default.RuntimeDeploymentPlan) != JsonSerializer.Serialize(plan, ManagerJsonContext.Default.RuntimeDeploymentPlan))
            throw new IOException("部署来源、目标或计划已变化，请重新预览。");
        var id = Guid.NewGuid().ToString("N"); var transaction = Path.Combine(state, "RuntimeDeployments", id);
        PathRules.NoLinks(transaction); Directory.CreateDirectory(transaction);
        DeploymentReceipt? copied = null; EvarsEditReceipt? edited = null; var phase = "plan"; var markerWritten = false;
        try
        {
            File.WriteAllBytes(Path.Combine(transaction, "plan.json"), JsonSerializer.SerializeToUtf8Bytes(plan, ManagerJsonContext.Default.RuntimeDeploymentPlan));
            phase = "copy"; onPhase?.Invoke(phase);
            if (plan.Copy is not null) copied = PluginDeployment.Apply(plan.Copy, state);
            phase = "environment"; onPhase?.Invoke(phase);
            try { edited = EvarsEditor.Apply(plan.Environment); }
            catch (EvarsApplyException error) { edited = error.Receipt; throw; }
            phase = "ownership"; onPhase?.Invoke(phase);
            if (plan.Copy is not null)
            {
                var marker = Path.Combine(plan.Request.RuntimeRoot, Marker);
                if (Hash(marker) != plan.MarkerBeforeHash) throw new IOException("运行目录标记被并发修改。");
                WriteAtomic(marker, Convert.FromBase64String(plan.MarkerAfterBase64!)); markerWritten = true;
            }
            var receipt = new RuntimeDeploymentReceipt(id, plan, copied, edited!, transaction);
            phase = "receipt"; onPhase?.Invoke(phase);
            File.WriteAllBytes(Path.Combine(transaction, "receipt.json"), JsonSerializer.SerializeToUtf8Bytes(receipt, ManagerJsonContext.Default.RuntimeDeploymentReceipt));
            return receipt;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            var failures = Recover(plan, copied, edited, markerWritten);
            if (error is DeploymentApplyException copyFailure && copyFailure.RecoveryErrors.Length > 0)
                failures = [.. failures, .. copyFailure.RecoveryErrors.Select(e => "payload: " + e + " Backup=" + copyFailure.BackupDirectory)];
            var diagnostic = new RuntimeDeploymentDiagnostic(phase, transaction, failures);
            try { File.WriteAllBytes(Path.Combine(transaction, "failure.json"), JsonSerializer.SerializeToUtf8Bytes(diagnostic, ManagerJsonContext.Default.RuntimeDeploymentDiagnostic)); }
            catch (Exception logError) when (logError is IOException or UnauthorizedAccessException)
            { diagnostic = diagnostic with { RecoveryErrors = [.. failures, "failure-log: " + logError.Message] }; }
            throw new RuntimeDeploymentException("运行部署失败：" + (string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message) +
                (failures.Length == 0 ? " 已恢复本次修改。" : " 恢复待处理。"), diagnostic, error);
        }
    }

    /// <summary>条件恢复整次已提交部署，外部修改保持并报告。</summary>
    public RuntimeDeploymentDiagnostic Restore(RuntimeDeploymentReceipt receipt)
    {
        var failures = Recover(receipt.Plan, receipt.CopyReceipt, receipt.EnvironmentReceipt, receipt.Plan.Copy is not null);
        return new("restore", receipt.TransactionDirectory, failures);
    }

    private static string[] Recover(RuntimeDeploymentPlan plan, DeploymentReceipt? copied, EvarsEditReceipt? edited, bool markerWritten)
    {
        var failures = new List<string>();
        if (edited is not null)
        {
            try { EvarsEditor.Restore(edited); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failures.Add("environment: " + error.Message); }
        }
        if (copied is not null && plan.Copy is not null)
        {
            try { failures.AddRange(PluginDeployment.Restore(plan.Copy, copied).Unresolved); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failures.Add("payload: " + error.Message); }
        }
        if (markerWritten)
        {
            try
            {
                var marker = Path.Combine(plan.Request.RuntimeRoot, Marker);
                if (Hash(marker) != Digest(Convert.FromBase64String(plan.MarkerAfterBase64!))) throw new IOException("运行所有权标记已变化。");
                if (failures.Count != 0) throw new IOException("载荷尚有未恢复项，保留当前标记等待处理。");
                if (plan.MarkerBeforeBase64 is null) File.Delete(marker);
                else WriteAtomic(marker, Convert.FromBase64String(plan.MarkerBeforeBase64));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failures.Add("ownership: " + error.Message); }
        }
        return failures.ToArray();
    }

    private static bool IsRuntimeFile(string path) => path.StartsWith("PMLLIB/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("PMLUI/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("runtime/profiles/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("runtime/cli/", StringComparison.OrdinalIgnoreCase);
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string? Hash(string file) { if (!File.Exists(file)) return null; using var stream = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    private static void WriteAtomic(string file, byte[] bytes)
    {
        PathRules.NoLinks(file); Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { output.Write(bytes); output.Flush(true); } File.Move(temporary, file, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
