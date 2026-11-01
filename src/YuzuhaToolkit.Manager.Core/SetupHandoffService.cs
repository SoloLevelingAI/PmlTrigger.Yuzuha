using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>安装交接的只读核验摘要；文件就绪不代表 AVEVA 已接入或 Host 可用。</summary>
public sealed record SetupHandoffInspection(string PackageRoot, string Version, string IntegrationMode, string ManifestHash,
    string MarkerHash, int FilesVerified, int LegacyEnvironmentCount, string NextAction);
/// <summary>冻结旧安装器环境所有权的交接；原记录字节仅存于本机计划，不进入公开摘要。</summary>
public sealed record SetupLegacyTransferPlan(string PackageRoot, string EnvironmentFile, string EnvironmentHash,
    string MarkerHash, string MarkerBeforeBase64, string MarkerAfterBase64, string OriginalEntryJson);
/// <summary>交接结果和持久备份定位；不会改写 EVARS 正文。</summary>
public sealed record SetupLegacyTransferReceipt(string Id, string State, string PackageRoot, string EnvironmentFile,
    string EnvironmentHash, string MarkerBeforeHash, string MarkerAfterHash, string BackupFile, string ReceiptFile);
/// <summary>可供 GUI/CLI 审阅的所有权变化摘要，不包含原环境字节。</summary>
public sealed record SetupLegacyTransferSummary(string PackageRoot, string EnvironmentFile, string EnvironmentHash,
    string MarkerHash, bool OriginalAvailable, string Effect);
/// <summary>已触及安装标记但无法可靠持久记录最终结果，要求人工核对而非重放。</summary>
public sealed class SetupTransferUnknownException(string message, SetupLegacyTransferReceipt receipt, Exception inner) : IOException(message, inner)
{
    /// <summary>部分执行后的非秘密定位信息。</summary>
    public SetupLegacyTransferReceipt Receipt { get; } = receipt;
}
/// <summary>安装交接与旧 v2 所有权移交；使用同一份当前安装标记保留旧卸载兼容性。</summary>
public sealed class SetupHandoffService(string? stateDirectory = null, Action<string>? onPhase = null, Action<string>? checkProcesses = null)
{
    private const string Owner = "Yuzuha.Inno.Plan.v2";
    private readonly string state = Path.GetFullPath(stateDirectory ?? WorkspaceSettings.StateDirectory);
    private readonly Action<string> processCheck = checkProcesses ?? CheckSetupProcesses;

    /// <summary>核验安装根、v2 标记及每项载荷摘要，只读返回下一步。</summary>
    public SetupHandoffInspection Inspect(string packageRoot)
    {
        var root = Root(packageRoot);var markerBytes = Read(Path.Combine(root, ".yuzuha-inno.json"), 8 * 1024 * 1024);
        var marker = Object(markerBytes);ValidateMarker(root, marker);
        var manifestBytes = Read(Path.Combine(root, ".setup-payload.json"), 16 * 1024 * 1024);var manifest = Object(manifestBytes);
        var files = manifest["files"] as JsonArray ?? throw new IOException("安装清单缺少 files。");
        if (files.Count is 0 or > 20000) throw new IOException("安装清单文件数量超出预算。");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in files)
        {
            var relative = Text(item, "path");if (!seen.Add(relative)) throw new IOException("安装清单有重复路径。");
            var target = PathRules.Inside(root, relative);var expected = Text(item, "sha256");
            if (!ValidHash(expected) || HashFile(target) != expected.ToLowerInvariant()) throw new IOException("安装载荷与清单不一致：" + relative);
        }
        if (Hash(Read(Path.Combine(root, ".yuzuha-inno.json"), 8 * 1024 * 1024)) != Hash(markerBytes)) throw new IOException("安装记录在核验期间发生变化。");
        var mode = marker["request"]?["integrationMode"]?.GetValue<string>() ?? "aveva";
        if (mode is not ("manager" or "aveva" or "none")) throw new IOException("不支持的安装交接模式。");
        if (mode == "manager" && (!seen.Contains("runtime/desktop/YuzuhaToolkit.Desktop.exe") || !seen.Contains("runtime/manager/Yuzuha.Manager.Cli.exe")))
            throw new IOException("Manager交接缺少桌面或管理CLI载荷，请核对安装包。");
        return new(root, Text(manifest, "version"), mode, Hash(manifestBytes), Hash(markerBytes), files.Count,
            ((JsonArray)marker["managed"]!).Count(x => x?["kind"]?.GetValue<string>() == "environment"), "选择产品环境并预览部署；不会自动写入或启动。");
    }

    /// <summary>只读冻结一个旧环境条目的移交；支持当前 v2 所有权，拒绝漂移和未知格式。</summary>
    public SetupLegacyTransferPlan PreviewLegacy(string packageRoot, string environmentFile)
    {
        var inspection = Inspect(packageRoot);var root = inspection.PackageRoot;var file = RootFile(environmentFile);
        if (file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("环境目标不能位于 Inno 应用根。");
        if (!new[] { ".init", ".bat" }.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) throw new IOException("仅支持已登记的 INIT/BAT 环境目标。");
        var before = Read(Path.Combine(root, ".yuzuha-inno.json"), 8 * 1024 * 1024);if (Hash(before) != inspection.MarkerHash) throw new IOException("安装所有权已变化，请重新预览。");
        var marker = Object(before);var managed = (JsonArray)marker["managed"]!;
        var selected = managed.Where(x => x?["kind"]?.GetValue<string>() == "environment" && Same(Text(x, "path"), file)).ToArray();
        if (selected.Length != 1) throw new IOException("目标没有唯一的旧安装器环境所有权。");
        var entry = selected[0]!;var applied = Text(entry, "afterSha256");var targetBytes = Read(file, 2 * 1024 * 1024);var targetHash = Hash(targetBytes);
        if (!ValidHash(applied) || targetHash != applied.ToLowerInvariant()) throw new IOException("旧环境在安装后已变化，保留原文件并停止移交。");
        ValidateLegacyBlock(targetBytes);
        if (entry is not JsonObject originalObject || !originalObject.ContainsKey("original")) throw new IOException("旧记录没有原始字节字段，无法安全移交。");
        var original = entry["original"]?.GetValue<string>();if (original is not null) _ = Convert.FromBase64String(original);
        var originalEntry = entry.ToJsonString();managed.Remove(entry);
        if (marker["request"]?["environments"] is JsonArray environments)
            for (var i = environments.Count - 1; i >= 0; i--) if (Same(Text(environments[i], "path"), file)) environments.RemoveAt(i);
        return new(root, file, targetHash, Hash(before), Convert.ToBase64String(before),
            Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(marker, SetupHandoffJsonContext.Default.JsonObject)), originalEntry);
    }

    /// <summary>仅条件替换安装器标记并持久保留旧记录；旧卸载随后读取 managed 时不再恢复此目标。</summary>
    public SetupLegacyTransferReceipt Transfer(SetupLegacyTransferPlan plan)
    {
        var root = Root(plan.PackageRoot);var marker = Path.Combine(root, ".yuzuha-inno.json");
        var fresh = PreviewLegacy(root, plan.EnvironmentFile);
        if (fresh != plan) throw new IOException("交接来源或计划已变化，请重新预览。");
        processCheck(root);var id = Guid.NewGuid().ToString("N");var directory = Path.Combine(state, "SetupTransfers", id);
        PathRules.NoLinks(directory);Directory.CreateDirectory(directory);
        var lease = Path.Combine(state, "SetupTransfers", Hash(System.Text.Encoding.UTF8.GetBytes(root.ToUpperInvariant())) + ".lease");
        PathRules.NoLinks(lease);
        using var ownerLease = new FileStream(lease, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var backup = Path.Combine(directory, "installer-marker.before.json");var receiptFile = Path.Combine(directory, "receipt.json");
        var receipt = new SetupLegacyTransferReceipt(id, "accepted", root, plan.EnvironmentFile, plan.EnvironmentHash, plan.MarkerHash,
            Hash(Convert.FromBase64String(plan.MarkerAfterBase64)), backup, receiptFile);
        WriteNew(backup, Convert.FromBase64String(plan.MarkerBeforeBase64));WriteReceipt(receiptFile, receipt);
        var temporary = marker + ".transfer-" + id + ".tmp";var touched = false;
        try
        {
            WriteNew(temporary, Convert.FromBase64String(plan.MarkerAfterBase64));onPhase?.Invoke("before-marker");processCheck(root);
            if (HashFile(marker) != plan.MarkerHash || HashFile(plan.EnvironmentFile) != plan.EnvironmentHash) throw new IOException("交接写入前来源已变化，未覆盖。");
            File.Move(temporary, marker, true);touched = true;onPhase?.Invoke("after-marker");
            if (HashFile(marker) != receipt.MarkerAfterHash || HashFile(plan.EnvironmentFile) != plan.EnvironmentHash) throw new IOException("交接后发生并发变化，请人工核对安装记录和环境。");
            receipt = receipt with { State = "transferred" };WriteReceipt(receiptFile, receipt);return receipt;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            receipt = receipt with { State = touched ? "unknown" : "rejected" };
            try { WriteReceipt(receiptFile, receipt); } catch (Exception recording) when (recording is IOException or UnauthorizedAccessException) { }
            if (touched) throw new SetupTransferUnknownException("安装标记可能已移交；不自动重放或恢复，按回执人工核对。", receipt, error);
            throw;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void ValidateMarker(string root, JsonObject marker)
    {
        if (Text(marker, "owner") != Owner || !Same(Text(marker, "root"), root) || marker["request"] is not JsonObject request || !Same(Text(request, "root"), root) || marker["managed"] is not JsonArray managed)
            throw new IOException("仅支持可核验的 Yuzuha.Inno.Plan.v2 安装记录；旧格式须独立迁移。");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in managed) if (!paths.Add(RootFile(Text(entry, "path")))) throw new IOException("旧所有权记录含重复目标。");
    }
    private static void ValidateLegacyBlock(byte[] bytes)
    {
        var encoding = bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe }) ? System.Text.Encoding.Unicode :
            bytes.AsSpan().StartsWith(new byte[] { 0xfe, 0xff }) ? System.Text.Encoding.BigEndianUnicode :
            bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? System.Text.Encoding.UTF8 : System.Text.Encoding.Latin1;
        var text = encoding.GetString(bytes).TrimStart('\ufeff');
        const string start = @"(?im)^[ \t]*rem >>> Yuzuha managed settings[ \t]*\r?$", end = @"(?im)^[ \t]*rem <<< Yuzuha managed settings[ \t]*\r?$";
        if (System.Text.RegularExpressions.Regex.Matches(text, start).Count != 1 || System.Text.RegularExpressions.Regex.Matches(text, end).Count != 1)
            throw new IOException("旧环境块不是已核验的Yuzuha v2格式，需要独立迁移；未移交。");
        var block = System.Text.RegularExpressions.Regex.Match(text, @"(?ims)^[ \t]*rem >>> Yuzuha managed settings[ \t]*\r?\n(.*?)^[ \t]*rem <<< Yuzuha managed settings[ \t]*\r?$");
        if (!block.Success || System.Text.RegularExpressions.Regex.Matches(block.Groups[1].Value, @"(?im)^set Yuzuha=(?:AM|PDMS|E3D2\.1|E3D3\.1\.6)\r?$").Count != 1)
            throw new IOException("旧环境块缺少唯一的已支持Profile，未移交。");
    }
    private static string Root(string value)
    {
        var full = RootFile(value);if (!Directory.Exists(full)) throw new IOException("安装目录不存在。");return Path.TrimEndingDirectorySeparator(full);
    }
    private static string RootFile(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value) || value.StartsWith("\\\\", StringComparison.Ordinal)) throw new IOException("需要明确的本机绝对路径。");
        var path = Path.GetFullPath(value);PathRules.NoLinks(path);return path;
    }
    private static bool Same(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    private static bool ValidHash(string text) => text.Length == 64 && text.All(char.IsAsciiHexDigit);
    private static string Text(JsonNode? node, string key) => node?[key]?.GetValue<string>() ?? throw new IOException("安装记录缺少字段：" + key);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string HashFile(string path) { PathRules.NoLinks(path);using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    private static byte[] Read(string file, int max) { PathRules.NoLinks(file);using var input = File.OpenRead(file);if (input.Length > max) throw new IOException("安装记录超出预算。");using var output = new MemoryStream();input.CopyTo(output);return output.ToArray(); }
    private static JsonObject Object(byte[] bytes) => JsonNode.Parse(bytes, documentOptions: new() { MaxDepth = 32 }) as JsonObject ?? throw new IOException("安装记录不是对象。");
    private static void WriteNew(string path, byte[] bytes) { using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);output.Write(bytes);output.Flush(true); }
    private static void WriteReceipt(string file, SetupLegacyTransferReceipt receipt)
    {
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { WriteNew(temporary, JsonSerializer.SerializeToUtf8Bytes(receipt, SetupHandoffJsonContext.Default.SetupLegacyTransferReceipt));File.Move(temporary, file, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void CheckSetupProcesses(string root)
    {
        foreach (var process in Process.GetProcesses()) using (process)
        {
            if (process.Id == Environment.ProcessId) continue;
            string name;try { name = process.ProcessName; } catch (InvalidOperationException) { continue; }
            if (name.Equals("Yuzuha.SetupBridge", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Phayton-", StringComparison.OrdinalIgnoreCase) && name.EndsWith("-Setup", StringComparison.OrdinalIgnoreCase))
                throw new IOException("请关闭正在运行的安装器/安装桥接程序后移交所有权。");
            if (name.StartsWith("unins", StringComparison.OrdinalIgnoreCase))
            {
                try { if (process.MainModule?.FileName is { } file && file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("请关闭此安装的卸载程序后移交所有权。"); }
                catch (System.ComponentModel.Win32Exception) { throw new IOException("无法核对卸载进程，请关闭后再移交。"); }
            }
        }
    }
}

/// <summary>安装交接的独立 AOT JSON 元数据，避免与管理器主目录相互覆盖。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(SetupHandoffInspection))]
[JsonSerializable(typeof(SetupLegacyTransferPlan))]
[JsonSerializable(typeof(SetupLegacyTransferReceipt))]
[JsonSerializable(typeof(SetupLegacyTransferSummary))]
[JsonSerializable(typeof(JsonObject))]
public partial class SetupHandoffJsonContext : JsonSerializerContext;
