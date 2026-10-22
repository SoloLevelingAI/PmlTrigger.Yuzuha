using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using ModelContextProtocol;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Mcp;

/// <summary>开发文件的版本与执行证据；测试成功和人工审阅分别记录。</summary>
public sealed class DevelopmentEvidence
{
    /// <summary>本机源码文件。</summary>
    public string SourceFile { get; set; } = "";
    /// <summary>源文件 SHA256。</summary>
    public string SourceSha256 { get; set; } = "";
    /// <summary>source 或 test。</summary>
    public string Kind { get; set; } = "source";
    /// <summary>NotRun、Dispatched、Passed、Failed 或 Unknown；调度成功不等于断言通过。</summary>
    public string ExecutionStatus { get; set; } = "NotRun";
    /// <summary>人工审阅始终由人另行完成，自动运行不替代审阅。</summary>
    public string HumanReview { get; set; } = "Pending";
    /// <summary>Host 执行回执，未运行时为空。</summary>
    public RunPmlCommandResponse? Receipt { get; set; }
    /// <summary>执行异常或传输未知信息。</summary>
    public string Error { get; set; } = "";
}

/// <summary>开发证据的编译期 JSON 元数据。</summary>
[System.Text.Json.Serialization.JsonSerializable(typeof(DevelopmentEvidence))]
internal partial class DevelopmentJsonContext : System.Text.Json.Serialization.JsonSerializerContext { }

/// <summary>明确绑定开发环境、限定源码写入及执行快照；不自动选择会话或 SAVEWORK。</summary>
[McpServerToolType]
public sealed class DevelopmentTools(ExecutionAuthorizationStore authorization, YuzuhaRpcBridge bridge, RuntimeJournal journal)
{
    /// <summary>查询当前授权，不执行 PML。</summary>
    [McpServerTool, Description("Read persisted Conservative/Standard/Development policy, exact development target, source root, project bypass rules, reason and expiry. Does not execute PML.")]
    public string GetExecutionAuthorization() => JsonSerializer.Serialize(authorization.Read(), AuthorizationJsonContext.Default.ExecutionAuthorization);

    /// <summary>切换到保守或标准模式，同时撤销开发许可和所有项目规则规避。</summary>
    [McpServerTool, Description("Set Conservative (registration only, no PML dispatch) or Standard (authorized registered functions and typed read-only queries). Revokes development target and all project bypass rules. Use connect_development_session to grant Development mode.")]
    public string SetExecutionAuthorization(string mode) => Guard(()=>SetAuthorizationCore(mode));
    private string SetAuthorizationCore(string mode)
    {
        if (mode is not ("Conservative" or "Standard")) throw new ArgumentException("Use Conservative or Standard; Development requires an explicit connection.");
        return JsonSerializer.Serialize(authorization.Save(new() { Mode = mode }), AuthorizationJsonContext.Default.ExecutionAuthorization);
    }

    /// <summary>连接准确 PID 并核验完整身份后授予有时限的开发许可；不执行 PML。</summary>
    [McpServerTool, Description("Explicitly bind Development authorization to a discovered PID, process start identity, module and existing source directory. bypassRules is a comma-separated allowlist: raw-command,function-permission. Record a reason; expires in 1..24 hours. Never bypass identity, source hash, logs or automatic MARKDB. This grants project-level execution authority, not an OS sandbox.")]
    public Task<string> ConnectDevelopmentSession(int processId, string expectedModel, string sourceRoot, string reason, string bypassRules = "", int validHours = 8, CancellationToken cancellationToken = default) => GuardAsync(()=>ConnectCore(processId,expectedModel,sourceRoot,reason,bypassRules,validHours,cancellationToken));
    private async Task<string> ConnectCore(int processId,string expectedModel,string sourceRoot,string reason,string bypassRules,int validHours,CancellationToken cancellationToken)
    {
        if (validHours is < 1 or > 24) throw new ArgumentOutOfRangeException(nameof(validHours));
        var status = await bridge.SelectSessionAsync(processId, expectedModel, cancellationToken).ConfigureAwait(false);
        if (!status.TargetVerified || status.Host == null) throw new InvalidOperationException("PML_DEVELOPMENT_TARGET_NOT_VERIFIED");
        var state = authorization.Save(new() { Mode = "Development", ProcessId = processId, ProcessStartTimeUtcTicks = status.Host.ProcessStartTimeUtcTicks,
            Model = status.Host.Model!, SourceRoot = Path.GetFullPath(sourceRoot), Reason = reason,
            BypassRules = bypassRules.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray(), ExpiresUtc = DateTimeOffset.UtcNow.AddHours(validHours) });
        return JsonSerializer.Serialize(state, AuthorizationJsonContext.Default.ExecutionAuthorization);
    }

    /// <summary>按准确路径创建或比较哈希后更新开发源码/测试；生成待人工审阅的证据文件。</summary>
    [McpServerTool, Description("Write UTF-8 PML source or test inside the configured Development source root. relativePath must be relative, with .pmlfnc/.pmlobj/.pmlfrm/.pmlmac extension. kind is source or test. Creates by default; overwrite requires exact current expectedSha256. Refuses links and traversal. Resets execution evidence to NotRun and human review to Pending. Does not load or execute PML.")]
    public string WriteDevelopmentFile(string relativePath, string content, string kind = "source", string expectedSha256 = "") => Guard(()=>WriteCore(relativePath,content,kind,expectedSha256));
    private string WriteCore(string relativePath,string content,string kind,string expectedSha256)
    {
        var state = RequireDevelopment(); var path = Resolve(state, relativePath);
        if (kind is not ("source" or "test") || !new[] { ".pmlfnc", ".pmlobj", ".pmlfrm", ".pmlmac" }.Contains(Path.GetExtension(path).ToLowerInvariant())) throw new ArgumentException("Use PML source/test and a supported extension.");
        var bytes = new UTF8Encoding(false, true).GetBytes(content);
        if (bytes.Length > 1048576) throw new ArgumentException("Source exceeds 1 MiB.");
        var call = journal.Begin("development.file.write", JsonSerializer.Serialize(new System.Text.Json.Nodes.JsonObject { ["relativePath"] = relativePath, ["kind"] = kind, ["expectedSha256"] = expectedSha256 }, YuzuhaToolJsonContext.Default.JsonObject), state.ProcessId);
        try
        {
            if (journal.Snapshot().LoggingError != null) throw new IOException("PML_AUDIT_UNAVAILABLE");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); ExecutionAuthorizationStore.RequireSourcePath(state.SourceRoot, path);
            ExecutionAuthorizationStore.RequireSourcePath(state.SourceRoot, path + ".write.lock");
            ExecutionAuthorizationStore.RequireSourcePath(state.SourceRoot, path + ".evidence.json");
            using var held = new FileStream(path + ".write.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            if (File.Exists(path))
            {
                if (expectedSha256.Length == 0 || !Hash(File.ReadAllBytes(path)).Equals(expectedSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("PML_SOURCE_OVERWRITE_REVISION_MISMATCH");
            }
            else if (expectedSha256.Length != 0) throw new InvalidDataException("PML_SOURCE_OVERWRITE_TARGET_MISSING");
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temporary, bytes); File.Move(temporary, path, expectedSha256.Length > 0); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            var evidence = new DevelopmentEvidence { SourceFile = path, SourceSha256 = Hash(bytes), Kind = kind };
            SaveEvidence(evidence); journal.Complete(call, "succeeded", "sha256=" + evidence.SourceSha256 + "; humanReview=Pending", state.ProcessId);
            return JsonSerializer.Serialize(evidence, DevelopmentJsonContext.Default.DevelopmentEvidence);
        }
        catch (Exception error) { journal.Complete(call, "failed", error.Message, state.ProcessId); throw; }
    }

    /// <summary>重新核验开发身份并运行指定宏快照；测试断言和人工审阅状态不自动改为通过。</summary>
    [McpServerTool, Description("Run an exact SHA256 .pmlmac revision in the persisted Development session. Reconnects only the configured PID/start identity/module. Copies an execution snapshot and Host verifies its SHA256; automatic MARKDB is mandatory. Optional testOutcomeGlobal is reset to NotRun before macro execution and removed afterwards. Test code must assign exactly Passed or Failed: reason to this STRING; only explicit Passed marks assertion success. No SAVEWORK or automatic retry. Human review remains Pending.")]
    public Task<string> RunDevelopmentFile(string relativePath, string sourceSha256, string testOutcomeGlobal = "", CancellationToken cancellationToken = default) => GuardAsync(()=>RunCore(relativePath,sourceSha256,testOutcomeGlobal,cancellationToken));
    private async Task<string> RunCore(string relativePath,string sourceSha256,string testOutcomeGlobal,CancellationToken cancellationToken)
    {
        var state = RequireDevelopment(); var path = Resolve(state, relativePath);
        if (!path.EndsWith(".pmlmac", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Run a .pmlmac harness; load functions explicitly from reviewed test code.");
        YuzuhaToolkit.Pml.PmlSourceIdentity.Verify(path, sourceSha256);
        var evidence = ReadEvidence(path, sourceSha256);
        await bridge.SelectSessionAsync(state.ProcessId, state.Model, cancellationToken).ConfigureAwait(false);
        var snapshot = Resolve(state, Path.Combine(".yuzuha-runs", Guid.NewGuid().ToString("N") + ".pmlmac"));
        Directory.CreateDirectory(Path.GetDirectoryName(snapshot)!); ExecutionAuthorizationStore.RequireSourcePath(state.SourceRoot, snapshot);
        var bytes = File.ReadAllBytes(path);
        if (!Hash(bytes).Equals(sourceSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("PML_SOURCE_CHANGED");
        using (var output = new FileStream(snapshot, FileMode.CreateNew, FileAccess.Write, FileShare.None)) output.Write(bytes);
        try
        {
            evidence.Receipt = await bridge.RunDevelopmentMacroAsync(snapshot, sourceSha256, cancellationToken, testOutcomeGlobal).ConfigureAwait(false);
            evidence.ExecutionStatus = evidence.Receipt.Success ? evidence.Receipt.TestOutcome == "Passed" ? "Passed" : "Dispatched" : "Failed";
        }
        catch (Exception error)
        {
            evidence.ExecutionStatus = error is IOException or TimeoutException or OperationCanceledException ? "Unknown" : "Failed"; evidence.Error = error.Message;
            SaveEvidence(evidence); throw;
        }
        SaveEvidence(evidence); return JsonSerializer.Serialize(evidence, DevelopmentJsonContext.Default.DevelopmentEvidence);
    }

    /// <summary>读取准确开发版本的证据，源码变化时拒绝复用旧结果。</summary>
    [McpServerTool, Description("Read saved evidence for the exact Development source SHA256. Reports NotRun/Dispatched/Failed/Unknown and Pending human review. Dispatch success is not a test assertion result.")]
    public string GetDevelopmentEvidence(string relativePath,string sourceSha256) => Guard(()=>EvidenceCore(relativePath,sourceSha256));
    private string EvidenceCore(string relativePath,string sourceSha256)
    {
        var path = Resolve(RequireDevelopment(), relativePath); YuzuhaToolkit.Pml.PmlSourceIdentity.Verify(path, sourceSha256);
        return JsonSerializer.Serialize(ReadEvidence(path, sourceSha256), DevelopmentJsonContext.Default.DevelopmentEvidence);
    }
    private static T Guard<T>(Func<T> action)
    {
        try { return action(); }
        catch(Exception error) when(error is ArgumentException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or IOException or NotSupportedException) { throw new McpException(error.Message); }
    }
    private static async Task<T> GuardAsync<T>(Func<Task<T>> action)
    {
        try { return await action().ConfigureAwait(false); }
        catch(Exception error) when(error is ArgumentException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or IOException or NotSupportedException) { throw new McpException(error.Message); }
    }
    private ExecutionAuthorization RequireDevelopment()
    {
        var state = authorization.Read();
        if (state.Mode != "Development" || state.ExpiresUtc <= DateTimeOffset.UtcNow) throw new UnauthorizedAccessException("PML_ACTIVE_DEVELOPMENT_GRANT_REQUIRED");
        return state;
    }
    private static string Resolve(ExecutionAuthorization state, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new ArgumentException("Use a relative development file path.");
        return ExecutionAuthorizationStore.RequireSourcePath(state.SourceRoot, Path.Combine(state.SourceRoot, relative));
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void SaveEvidence(DevelopmentEvidence evidence)
    {
        var path = evidence.SourceFile + ".evidence.json";
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(evidence, DevelopmentJsonContext.Default.DevelopmentEvidence)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static DevelopmentEvidence ReadEvidence(string path, string sha)
    {
        var file = path + ".evidence.json";
        if ((File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) || File.Exists(file) && new FileInfo(file).Length > 1048576) throw new InvalidDataException("PML_EVIDENCE_FILE_INVALID");
        var evidence = File.Exists(file) ? JsonSerializer.Deserialize(File.ReadAllText(file), DevelopmentJsonContext.Default.DevelopmentEvidence) : null;
        if (evidence == null) return new() { SourceFile = path, SourceSha256 = sha };
        if (evidence.SourceSha256 != sha || evidence.SourceFile != path) throw new InvalidDataException("PML_EVIDENCE_REVISION_MISMATCH");
        return evidence;
    }
}
