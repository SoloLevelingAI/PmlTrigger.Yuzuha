using LaunchDesk.Next.Application.Infrastructure.Contracts;

namespace LaunchDesk.Next.Platform.Files;

public sealed class WindowsFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public IEnumerable<string> EnumerateFiles(string directory) => Directory.EnumerateFiles(directory);
    public IEnumerable<string> EnumerateDirectories(string directory) => Directory.EnumerateDirectories(directory);
    public bool IsReparsePoint(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    public string ReadText(string path, int maximumBytes)
    {
        if (maximumBytes < 1) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        using var stream = File.OpenRead(path);
        if (stream.Length > maximumBytes) throw new InvalidDataException("Text file exceeds the configured byte limit.");
        var bytes = new byte[maximumBytes + 1];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = stream.Read(bytes, count, bytes.Length - count);
            if (read == 0) break;
            count += read;
        }
        if (count > maximumBytes) throw new InvalidDataException("Text file grew beyond the configured byte limit.");
        using var reader = new StreamReader(new MemoryStream(bytes, 0, count),
            new System.Text.UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
