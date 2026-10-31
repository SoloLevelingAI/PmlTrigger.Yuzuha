using System.Text;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager.LaunchDesk;

#pragma warning disable CS1591 // Small port boundary matching the LaunchDesk prototype filesystem contract.
public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    IEnumerable<string> EnumerateFiles(string directory);
    IEnumerable<string> EnumerateDirectories(string directory);
    bool IsReparsePoint(string path);
    string ReadText(string path, int maximumBytes);
}
public sealed record DiscoveryDiagnostic(string Code, string Path, string Message);

internal static class SensitiveFields
{
    internal static bool ContainsSecret(string name) => new[] { "PASSWORD", "SECRET", "TOKEN", "CREDENTIAL" }
        .Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}

/// <summary>只读文件适配器；不执行 INIT/BAT，不继承 LaunchDesk 的凭据或进程服务。</summary>
public sealed class DiscoveryFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public IEnumerable<string> EnumerateFiles(string directory) { PathRules.NoLinks(directory); return Directory.EnumerateFiles(directory); }
    public IEnumerable<string> EnumerateDirectories(string directory) { PathRules.NoLinks(directory); return Directory.EnumerateDirectories(directory); }
    public bool IsReparsePoint(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    public string ReadText(string path, int maximumBytes)
    {
        PathRules.NoLinks(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maximumBytes) throw new IOException("配置文件超过读取预算。");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
        return reader.ReadToEnd();
    }
}
