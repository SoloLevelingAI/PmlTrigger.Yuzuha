using System.Text;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Manager.LaunchDesk;

#pragma warning disable CS1591 // Small port boundary matching the LaunchDesk prototype filesystem contract.
/// <summary>项目与模块静态发现使用的只读文件边界，便于以受控目录或测试文件系统提供证据。</summary>
public interface IFileSystem
{
    /// <summary>判断配置或模块可执行文件是否存在。</summary>
    bool FileExists(string path);
    /// <summary>判断候选项目目录是否存在。</summary>
    bool DirectoryExists(string path);
    /// <summary>枚举目录的直接文件，不递归进入子目录。</summary>
    IEnumerable<string> EnumerateFiles(string directory);
    /// <summary>枚举目录的直接子目录，由扫描器控制递归预算。</summary>
    IEnumerable<string> EnumerateDirectories(string directory);
    /// <summary>判断路径是否为应被发现流程跳过的重解析点。</summary>
    bool IsReparsePoint(string path);
    /// <summary>在字节预算内读取配置文本；超出预算时拒绝读取。</summary>
    string ReadText(string path, int maximumBytes);
}
/// <summary>记录静态发现未能处理的路径、原因代码和说明，不把候选结果当作运行时环境。</summary>
public sealed record DiscoveryDiagnostic(string Code, string Path, string Message);

/// <summary>识别不应进入项目发现变量表的凭据类字段名称。</summary>
internal static class SensitiveFields
{
    /// <summary>按大小写不敏感的 PASSWORD、SECRET、TOKEN 或 CREDENTIAL 片段识别敏感字段。</summary>
    internal static bool ContainsSecret(string name) => new[] { "PASSWORD", "SECRET", "TOKEN", "CREDENTIAL" }
        .Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}

/// <summary>只读文件适配器；不执行 INIT/BAT，不继承 LaunchDesk 的凭据或进程服务。</summary>
public sealed class DiscoveryFileSystem : IFileSystem
{
    /// <summary>使用本机文件系统判断文件是否存在，不打开或执行文件。</summary>
    public bool FileExists(string path) => File.Exists(path);
    /// <summary>使用本机文件系统判断目录是否存在。</summary>
    public bool DirectoryExists(string path) => Directory.Exists(path);
    /// <summary>拒绝路径链中的链接后枚举直接文件，不递归扫描内容。</summary>
    public IEnumerable<string> EnumerateFiles(string directory) { PathRules.NoLinks(directory); return Directory.EnumerateFiles(directory); }
    /// <summary>拒绝路径链中的链接后枚举直接子目录。</summary>
    public IEnumerable<string> EnumerateDirectories(string directory) { PathRules.NoLinks(directory); return Directory.EnumerateDirectories(directory); }
    /// <summary>读取文件属性并检测 ReparsePoint 标记。</summary>
    public bool IsReparsePoint(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    /// <summary>拒绝链接和超预算文件，以带 BOM 检测的严格 UTF-8 默认编码只读配置文本。</summary>
    public string ReadText(string path, int maximumBytes)
    {
        PathRules.NoLinks(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maximumBytes) throw new IOException("配置文件超过读取预算。");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
        return reader.ReadToEnd();
    }
}
