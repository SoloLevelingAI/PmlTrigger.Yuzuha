using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.DevHost;

/// <summary>只提供清单登记的文档；旧安装残留不进入站点，每次打开均重新核对文件身份。</summary>
/// <param name="root">已验证的站点绝对目录。</param>
/// <param name="files">该站点允许公开的文件及原始字节身份。</param>
internal sealed class DocumentationManifestFileProvider(string root, IReadOnlyDictionary<string, DocumentationFile> files) : IFileProvider
{
    /// <inheritdoc />
    public IFileInfo GetFileInfo(string subpath)
    {
        var relative = subpath.TrimStart('/');
        if (!files.TryGetValue(relative, out var file)) return new NotFoundFileInfo(relative);
        try { using var stream = DocumentationPackage.OpenRead(root, file); return new Member(root, file); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException) { return new NotFoundFileInfo(relative); }
    }
    /// <inheritdoc />
    public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;
    /// <inheritdoc />
    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

    /// <summary>仅从验证后的只读流提供成员，不暴露可绕过校验的 PhysicalPath。</summary>
    /// <param name="root">文件所属的站点目录。</param>
    /// <param name="file">清单指定的路径、长度与摘要。</param>
    private sealed class Member(string root, DocumentationFile file) : IFileInfo
    {
        /// <inheritdoc />
        public bool Exists => true;
        /// <inheritdoc />
        public long Length => file.Bytes;
        /// <inheritdoc />
        public string? PhysicalPath => null;
        /// <inheritdoc />
        public string Name => Path.GetFileName(file.Path);
        /// <inheritdoc />
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        /// <inheritdoc />
        public bool IsDirectory => false;
        /// <inheritdoc />
        public Stream CreateReadStream() => DocumentationPackage.OpenRead(root, file);
    }
}
