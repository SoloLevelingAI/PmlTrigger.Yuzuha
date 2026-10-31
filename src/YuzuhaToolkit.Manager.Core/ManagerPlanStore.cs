using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>可供 GUI/CLI 审阅和执行的计划引用，不向输出泄露原环境文件字节。</summary>
public sealed record ManagerPlanReference(string PlanId, string Kind, string Sha256, JsonElement Summary);

/// <summary>本机保存的版本化计划内容。</summary>
public sealed record ManagerStoredPlan(int SchemaVersion, string Id, string Kind, JsonElement Payload);

/// <summary>只在受管状态目录保存计划；执行时核对用户审阅的完整内容指纹。</summary>
public sealed class ManagerPlanStore(string? stateDirectory = null)
{
    private readonly string root = Path.Combine(Path.GetFullPath(stateDirectory ?? WorkspaceSettings.StateDirectory), "ManagerPlans");

    /// <summary>保存新计划并返回有界摘要，原始内容留在本机状态目录。</summary>
    public ManagerPlanReference Save<T>(string kind, T plan, JsonTypeInfo<T> metadata, JsonElement summary)
    {
        ValidateKind(kind); PathRules.NoLinks(root); Directory.CreateDirectory(root);
        var id = Guid.NewGuid().ToString("N");
        var envelope = new ManagerStoredPlan(1, id, kind, JsonSerializer.SerializeToElement(plan, metadata));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, ManagerJsonContext.Default.ManagerStoredPlan);
        if (bytes.Length > 8 * 1024 * 1024) throw new IOException("保存的计划超过 8 MiB 预算，请缩小选择范围。");
        using (var output = new FileStream(Path.Combine(root, id + ".json"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { output.Write(bytes); output.Flush(true); }
        return new(id, kind, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), summary);
    }

    /// <summary>只读取正确类型、完整指纹且未被替换的计划。</summary>
    public T Read<T>(string kind, string id, string expectedHash, JsonTypeInfo<T> metadata)
    {
        ValidateKind(kind);
        if (!Guid.TryParseExact(id, "N", out _) || expectedHash.Length != 64 || !expectedHash.All(Uri.IsHexDigit))
            throw new ArgumentException("计划标识或 SHA256 无效。");
        var file = PathRules.Inside(root, id + ".json"); PathRules.NoLinks(file);
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 8 * 1024 * 1024) throw new IOException("保存的计划超过预算。");
        if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new IOException("计划内容已变化，请重新预览。");
        stream.Position = 0;
        var envelope = JsonSerializer.Deserialize(stream, ManagerJsonContext.Default.ManagerStoredPlan)
            ?? throw new IOException("计划正文为空。");
        if (envelope.SchemaVersion != 1 || envelope.Id != id || envelope.Kind != kind)
            throw new IOException("计划类型、标识或版本不匹配。");
        return envelope.Payload.Deserialize(metadata) ?? throw new IOException("计划载荷为空。");
    }

    private static void ValidateKind(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind) || kind.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '-'))
            throw new ArgumentException("计划类型无效。");
    }
}
