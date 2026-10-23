using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YuzuhaToolkit.Workspace;

/// <summary>开发流程固定的会话身份，不以发现窗口代替 Host 核验。</summary>
public sealed record DevelopmentTarget(int ProcessId, long StartTicks, string Model);
/// <summary>保留原生回执的单次操作结果；未知不能自动重试。</summary>
public sealed record DevelopmentReply(bool Success, JsonElement Raw, string? Error = null, bool MayHaveExecuted = false);
/// <summary>验证表达式返回的结构化行及原始回执。</summary>
public sealed record DevelopmentRows(DevelopmentReply Reply, int UnparsedCount, DevelopmentValue[] Items);
/// <summary>格式化行的路径和值；保持大小写和内容用于精确核对。</summary>
public sealed record DevelopmentValue(string Path, string? Value);
/// <summary>将现有 IPC 适配给桌面流程，工作区本身不依赖 AVEVA 或 MCP。</summary>
public interface IPmlDevelopmentHost
{
    /// <summary>重新核验固定会话，失败时停止，不切换到其他进程。</summary>
    Task<DevelopmentTarget> VerifyAsync(CancellationToken token);
    /// <summary>执行一次普通 PML 命令，不自动重试。</summary>
    Task<DevelopmentReply> ExecuteAsync(string command, CancellationToken token);
    /// <summary>通过既有数组读取解析结果并删除本流程自有结果变量。</summary>
    Task<DevelopmentRows> ReadAsync(string global, CancellationToken token);
}

/// <summary>声明要重载的自有类型、验证宏和精确预期；不接受 PMLCMD。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DevelopmentManifest(int SchemaVersion, string PluginName, string[] Objects,
    string[] Forms, string ValidationMacro, string ResultGlobal, Dictionary<string,string> Expected);
/// <summary>用户审阅的文件计划和验证契约，执行前再次比对两者。</summary>
public sealed record DevelopmentPreview(DeploymentPlan Plan, string ManifestPath, string ManifestHash,
    DevelopmentManifest Manifest);
/// <summary>一个步骤的派发前状态及原始结果。</summary>
public sealed class DevelopmentStep
{
    /// <summary>步骤名称。</summary>
    public string Name { get; set; } = "";
    /// <summary>Running、Passed 或 FailedOrUnknown。</summary>
    public string Status { get; set; } = "Running";
    /// <summary>原始 IPC 结果，不以步骤成功替代业务验证。</summary>
    public JsonElement? Result { get; set; }
    /// <summary>失败原因。</summary>
    public string? Error { get; set; }
}
/// <summary>可审阅的开发流程证据；模型数据只在本机保存。</summary>
public sealed class DevelopmentRun
{
    /// <summary>当前运行状态。</summary>
    public string Status { get; set; } = "Running";
    /// <summary>用户预览的来源、目标和契约。</summary>
    public required DevelopmentPreview Preview { get; init; }
    /// <summary>本次固定会话。</summary>
    public required DevelopmentTarget Target { get; init; }
    /// <summary>按发生顺序记录的步骤。</summary>
    public List<DevelopmentStep> Steps { get; set; } = [];
    /// <summary>自有结果数组的读取回执。</summary>
    public DevelopmentRows? Result { get; set; }
    /// <summary>复制回执与备份。</summary>
    public DeploymentReceipt? Deployment { get; set; }
    /// <summary>起始 UTC。</summary>
    public DateTime StartedUtc { get; init; } = DateTime.UtcNow;
    /// <summary>结束 UTC。</summary>
    public DateTime? CompletedUtc { get; set; }
    /// <summary>流程失败或未知原因。</summary>
    public string? Error { get; set; }
}

/// <summary>桌面部署、REHASH、Form/Object 重载和验证；复用文件事务和现有 IPC。</summary>
public static class PmlDevelopmentWorkflow
{
    /// <summary>冻结来源/目标文件与清单版本，供用户预览；不执行复制或 PML。</summary>
    public static DevelopmentPreview Preview(DeploymentPlan plan, string manifestPath)
    {
        manifestPath = Path.GetFullPath(manifestPath);
        var bytes = File.ReadAllBytes(manifestPath);
        var manifest = JsonSerializer.Deserialize(bytes, DevelopmentJsonContext.Default.DevelopmentManifest)
            ?? throw new InvalidDataException("验证清单为空。");
        static bool Name(string? value) => value is not null && System.Text.RegularExpressions.Regex.IsMatch(value, "^[A-Za-z][A-Za-z0-9_]{0,63}$");
        if (manifest.SchemaVersion != 1 || manifest.Objects is null || manifest.Forms is null || manifest.Expected is null ||
            manifest.Expected.Count == 0 || manifest.Objects.Length + manifest.Forms.Length == 0 ||
            !Name(manifest.ResultGlobal) || manifest.Objects.Concat(manifest.Forms).Any(n => !Name(n)) ||
            manifest.Objects.Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Objects.Length ||
            manifest.Forms.Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Forms.Length ||
            manifest.Expected.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value is null) ||
            manifest.PluginName != Path.GetFileName(plan.Target) ||
            string.IsNullOrWhiteSpace(manifest.ValidationMacro) ||
            Path.GetFileName(manifest.ValidationMacro) != manifest.ValidationMacro ||
            !manifest.ValidationMacro.EndsWith(".pmlmac", StringComparison.OrdinalIgnoreCase) || manifest.ValidationMacro.Contains(".."))
            throw new ArgumentException("验证清单中的名称、预期或插件目录无效。");
        var required = manifest.Objects.Select(n => n + ".pmlobj").Concat(manifest.Forms.Select(n => n + ".pmlfrm")).Append(manifest.ValidationMacro);
        if (required.Any(name => plan.Files.Count(f => f.RelativePath.Equals(name, StringComparison.OrdinalIgnoreCase)) != 1))
            throw new ArgumentException("重载类型和验证宏必须包含在已预览载荷的根目录。");
        return new(plan, manifestPath, Convert.ToHexString(SHA256.HashData(bytes)), manifest);
    }

    /// <summary>按已冻结的预览执行一次；每步先记录，失败或未知立即停止，不回放命令。</summary>
    public static async Task<DevelopmentRun> RunAsync(DevelopmentPreview preview, DevelopmentTarget target,
        string evidenceDirectory, IPmlDevelopmentHost host, CancellationToken token = default)
    {
        evidenceDirectory = Path.GetFullPath(evidenceDirectory);
        if (Directory.Exists(evidenceDirectory)) throw new IOException("请为每次运行选择新的证据目录。");
        var registered = Path.GetDirectoryName(preview.Plan.Target)!;
        if (evidenceDirectory.Equals(registered, StringComparison.OrdinalIgnoreCase) ||
            evidenceDirectory.StartsWith(registered + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("证据及备份必须在 PMLLIB 外。");
        var fresh = Preview(PluginDeployment.Preview(preview.Plan.Source, registered, preview.Manifest.PluginName), preview.ManifestPath);
        if (fresh.ManifestHash != preview.ManifestHash || !fresh.Plan.Files.SequenceEqual(preview.Plan.Files))
            throw new IOException("来源、目标或清单已变化，请重新预览。");
        Directory.CreateDirectory(evidenceDirectory);
        var record = new DevelopmentRun { Preview = preview, Target = target };
        var file = Path.Combine(evidenceDirectory, "run.json");
        void Save()
        {
            File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(record, DevelopmentJsonContext.Default.DevelopmentRun));
            File.Move(file + ".tmp", file, true);
        }
        async Task Step(string name, Func<Task> body)
        {
            var item = new DevelopmentStep { Name = name }; record.Steps.Add(item); Save();
            try { await body(); item.Status = "Passed"; }
            catch (Exception error) { item.Status = "FailedOrUnknown"; item.Error = error.Message; throw; }
            finally { Save(); }
        }
        async Task Verify()
        {
            if (await host.VerifyAsync(token) != target) throw new InvalidOperationException("AVEVA 会话身份已变化；流程停止。");
        }
        async Task Command(string name, string command) => await Step(name, async () =>
        {
            await Verify();
            var reply = await host.ExecuteAsync(command, token); record.Steps[^1].Result = reply.Raw;
            if (!reply.Success) throw new IOException(reply.Error ?? "命令失败或结果未知；不会自动重试。");
        });
        static void Check(DevelopmentRows rows)
        {
            if (!rows.Reply.Success) throw new IOException(rows.Reply.Error ?? "结果读取失败或未知。");
            if (rows.UnparsedCount != 0) throw new InvalidDataException("结果包含未解析行。");
        }
        Save();
        try
        {
            await Step("verify-session", Verify);
            var pathGlobal = "YzDevPath" + Guid.NewGuid().ToString("N")[..20];
            var pathMacro = Path.Combine(evidenceDirectory, "query-path.pmlmac");
            File.WriteAllText(pathMacro, "!!" + pathGlobal + " = object array()\nvar !path evar PMLLIB\n!!" + pathGlobal + ".append('[0] PmlLib<STRING> ' + !path)\n", new UTF8Encoding(false));
            await Command("query-pmllib", Macro(pathMacro));
            await Step("verify-pmllib", async () =>
            {
                await Verify(); var rows = await host.ReadAsync(pathGlobal, token); Check(rows);
                record.Steps[^1].Result = rows.Reply.Raw;
                if (rows.Items.Length != 1 || rows.Items[0].Value is not { } paths ||
                    !paths.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => Path.GetFullPath(p.Trim()).TrimEnd('\\','/'))
                        .Contains(registered.TrimEnd('\\','/'), StringComparer.OrdinalIgnoreCase))
                    throw new IOException("所选 PMLLIB 不是该会话报告的搜索根目录，请按实际路径重新选择。");
            });
            await Step("copy-payload", async () =>
            {
                await Verify();
                if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(preview.ManifestPath))) != preview.ManifestHash)
                    throw new IOException("清单已变化，请重新预览。");
                record.Deployment = PluginDeployment.Apply(preview.Plan, Path.Combine(evidenceDirectory, "deployment"));
            });
            await Command("rehash-all", "PML REHASH ALL");
            foreach (var type in preview.Manifest.Objects) await Command("reload-object:" + type, "pml reload object " + type);
            foreach (var form in preview.Manifest.Forms) await Command("reload-form:" + form, "pml reload form !!" + form);
            await Command("execute-validation", Macro(Path.Combine(preview.Plan.Target, preview.Manifest.ValidationMacro)));
            await Step("read-results", async () =>
            {
                await Verify(); record.Result = await host.ReadAsync(preview.Manifest.ResultGlobal, token);
                record.Steps[^1].Result = record.Result.Reply.Raw; Check(record.Result);
            });
            await Step("verify-results", () =>
            {
                var rows = record.Result!;
                if (rows.Items.Length != preview.Manifest.Expected.Count || preview.Manifest.Expected.Any(e =>
                    rows.Items.Count(i => i.Path == e.Key && i.Value == e.Value) != 1))
                    throw new InvalidDataException("已加载行为与清单预期不一致。");
                return Task.CompletedTask;
            });
            record.Status = "Passed";
        }
        catch (Exception error) { record.Status = "FailedOrUnknown"; record.Error = error.Message; }
        finally { record.CompletedUtc = DateTime.UtcNow; Save(); }
        return record;
    }

    /// <summary>引用本流程宏路径，拒绝 PML 展开字符和多行。</summary>
    public static string Macro(string path)
    {
        if (path.Any(c => c is '$' or '\r' or '\n')) throw new ArgumentException("宏路径不能包含 PML 展开符或换行。");
        return "$M '" + path.Replace("'", "''") + "'";
    }
}

/// <summary>开发证据与清单的 AOT JSON 元数据。</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DevelopmentManifest))]
[JsonSerializable(typeof(DevelopmentRun))]
public partial class DevelopmentJsonContext : JsonSerializerContext;
