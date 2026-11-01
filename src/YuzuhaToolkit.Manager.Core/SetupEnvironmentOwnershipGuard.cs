using System.Text.Json;
using Microsoft.Win32;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager;

/// <summary>环境仍由安装器管理或所有权无法核验时，阻止管理器重复接管。</summary>
public sealed class SetupEnvironmentOwnershipException(string code, string message) : IOException(message)
{
    /// <summary>可供调用方区分明确移交与人工核对的稳定拒绝码。</summary>
    public string Code { get; } = code;
}

/// <summary>只读核对明确来源和已登记安装的当前 v2 所有权，不执行环境脚本或全盘搜索。</summary>
public sealed class SetupEnvironmentOwnershipGuard(Func<IEnumerable<string>>? registeredRoots = null)
{
    /// <summary>拒绝仍被旧安装器拥有的精确环境目标；未知记录不作为允许覆盖的依据。</summary>
    /// <param name="environmentFile">将要修改的明确环境文件。</param>
    /// <param name="packageRoots">调用方已知的安装包和运行根；不存在标记不代表已证明全机无旧安装。</param>
    public void EnsureAvailable(string environmentFile, params string?[] packageRoots)
    {
        var target = LocalPath(environmentFile);
        var roots = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var supplied in packageRoots.Where(r => !string.IsNullOrWhiteSpace(r))) roots[LocalPath(supplied!)] = false;
        try
        {
            foreach (var installed in (registeredRoots ?? RegisteredRoots)())
            {
                if (roots.Count >= 128) throw new IOException("已登记安装数量超过核验预算。");
                roots[LocalPath(installed)] = true;
            }
            foreach (var (root, registered) in roots)
            {
                var marker = Path.Combine(root, ".yuzuha-inno.json");PathRules.NoLinks(marker);
                var exists = true;
                try { if ((File.GetAttributes(marker) & FileAttributes.Directory) != 0) throw new IOException("安装所有权标记不是文件。"); }
                catch (FileNotFoundException) { exists = false; }
                catch (DirectoryNotFoundException) { exists = false; }
                if (!exists)
                {
                    if (registered) throw new IOException("已登记安装缺少当前所有权标记。");
                    continue;
                }
                using var input = new FileStream(marker, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (input.Length > 8 * 1024 * 1024) throw new IOException("安装所有权记录超过核验预算。");
                using var document = JsonDocument.Parse(input, new() { MaxDepth = 32 });
                var record = document.RootElement;UniqueObject(record);
                if (record.GetProperty("owner").GetString() != "Yuzuha.Inno.Plan.v2" || !LocalPath(record.GetProperty("root").GetString()!).Equals(root, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("安装所有权格式或来源身份无法核验。");
                var request = record.GetProperty("request");UniqueObject(request);
                if (!LocalPath(request.GetProperty("root").GetString()!).Equals(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("安装请求与所有权根不一致。");
                var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);var owned = false;
                foreach (var entry in record.GetProperty("managed").EnumerateArray())
                {
                    UniqueObject(entry);var path = LocalPath(entry.GetProperty("path").GetString()!);
                    if (!paths.Add(path)) throw new IOException("安装所有权含重复目标。");
                    if (!path.Equals(target, StringComparison.OrdinalIgnoreCase)) continue;
                    if (entry.GetProperty("kind").GetString() != "environment" || !entry.TryGetProperty("original", out _) ||
                        entry.GetProperty("afterSha256").GetString() is not { Length: 64 } hash || !hash.All(char.IsAsciiHexDigit))
                        throw new IOException("目标的旧所有权条目无法核验。");
                    owned = true;
                }
                if (owned) throw new SetupEnvironmentOwnershipException("SETUP_OWNERSHIP_TRANSFER_REQUIRED", "该环境仍由旧 Inno 安装器拥有；先核对并明确移交所有权，再重新预览。若已有外部修改，保留现场人工核对。");
            }
        }
        catch (SetupEnvironmentOwnershipException) { throw; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or KeyNotFoundException or JsonException or System.Security.SecurityException)
        { throw new SetupEnvironmentOwnershipException("SETUP_OWNERSHIP_UNRESOLVED", "无法核验已知安装所有权，停止环境修改并人工核对。" + error.Message); }
    }

    private static string LocalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal)) throw new IOException("所有权核验需要本机绝对路径。");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));PathRules.NoLinks(full);return full;
    }
    private static void UniqueObject(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().GroupBy(p => p.Name, StringComparer.Ordinal).Any(g => g.Count() > 1)) throw new IOException("所有权对象损坏或字段重复。");
    }
    private static IEnumerable<string> RegisteredRoots()
    {
        if (!OperatingSystem.IsWindows()) return [];
        using var currentUser = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var installations = currentUser.OpenSubKey(@"Software\YuzuhaToolkit\Installations");
        if (installations is null) return [];
        var result = new List<string>();
        foreach (var name in installations.GetSubKeyNames())
        {
            if (result.Count >= 128) throw new IOException("已登记安装数量超过核验预算。");
            using var installation = installations.OpenSubKey(name);
            result.Add(installation?.GetValue("InstallLocation") as string ?? throw new IOException("已登记安装缺少来源目录。"));
        }
        return result;
    }
}
