using System.Text.Json;
using LaunchDesk.Next.Application.Infrastructure.Contracts;

namespace LaunchDesk.Next.Platform.Storage;

public sealed class JsonDocumentStore(string path) : IJsonDocumentStore, IDocumentLock
{
    public async Task<IDisposable> AcquireAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var deadline=DateTime.UtcNow.AddSeconds(5);
        while(true)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
            catch(IOException) when(DateTime.UtcNow<deadline) { await Task.Delay(25,ct); }
        }
    }
    public async Task<T?> ReadAsync<T>(CancellationToken ct = default)
    {
        if (!File.Exists(path)) return default;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: ct);
    }

    public async Task ReplaceAsync<T>(T value, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, value, new JsonSerializerOptions { WriteIndented = true }, ct);
                await stream.FlushAsync(ct);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
