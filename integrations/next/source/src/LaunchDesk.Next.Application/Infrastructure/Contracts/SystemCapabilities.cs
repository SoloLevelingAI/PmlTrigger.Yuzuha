namespace LaunchDesk.Next.Application.Infrastructure.Contracts;

public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    IEnumerable<string> EnumerateFiles(string directory);
    IEnumerable<string> EnumerateDirectories(string directory);
    bool IsReparsePoint(string path);
    string ReadText(string path, int maximumBytes);
}

public interface ITcpProbe
{
    Task<bool> ConnectAsync(string host, int port, CancellationToken ct);
}

public interface IWindowsServiceProbe
{
    bool IsRunning(string serviceName);
}

public interface IJsonDocumentStore
{
    Task<T?> ReadAsync<T>(CancellationToken ct = default);
    Task ReplaceAsync<T>(T value, CancellationToken ct = default);
}

// A directory of named documents; callers own schemas, adapters own filesystem IO.
public interface IJsonDocumentDirectory
{
    IJsonDocumentStore Open(string name);
}

public interface IDocumentLock
{
    Task<IDisposable> AcquireAsync(CancellationToken ct = default);
}

public interface ISecretHandle : IDisposable
{
    string Reveal();
}

public interface ICredentialStore
{
    ISecretHandle? Read(string reference);
    void Save(string reference, ISecretHandle secret);
}

public interface IRecordLog
{
    IReadOnlyList<Record> Records { get; }
    Task<bool> AppendAsync(Guid attemptId, Guid? runId, string stage, string outcome,
        IReadOnlyDictionary<string, string>? snapshot = null);
}
