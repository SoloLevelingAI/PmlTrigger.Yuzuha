namespace Core30.Core;

/// <summary>Stages file changes in the target directory and verifies the reviewed bytes at commit.</summary>
public static class AtomicFileWrite
{
    public static FileStream Lock(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public static void EnsureSnapshot(string path, byte[] bytes, Func<string, byte[]> read)
    {
        if (!File.Exists(path))
            WriteNew(path, bytes);
        else
            RequireDigest(read(path), FileContent.Sha256(bytes), "现有快照内容与摘要不一致。");
    }

    public static void WriteNew(string path, byte[] bytes) => Stage(path, bytes, File.Move);

    public static string Replace(string path, byte[] bytes, string expected, Func<string, byte[]> read,
        bool preserveMetadata = false)
    {
        var after = FileContent.Sha256(bytes);
        Stage(path, bytes, (pending, target) => ReplaceCurrent(pending, target, expected, after, read, preserveMetadata));
        var observed = FileContent.Sha256(read(path));
        FileContent.RequireMatchingSha256(observed, after, "写入后摘要未通过核对。");
        return observed;
    }

    public static void Stage(string path, byte[] bytes, Action<string, string> commit)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var pending = Path.Combine(directory, $".core30-{Guid.NewGuid():N}.tmp");
        var created = false;
        try
        {
            using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            commit(pending, path);
        }
        finally
        {
            if (created)
                File.Delete(pending);
        }
    }

    private static void ReplaceCurrent(string pending, string target, string before, string after,
        Func<string, byte[]> read, bool preserveMetadata)
    {
        int[] retryDelay = [100, 200, 400, 800];
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                RequireDigest(read(target), before, "目标摘要已变化，未替换目标。");
                RequireDigest(read(pending), after, "待写入文件摘要已变化，未替换目标。");
                if (preserveMetadata)
                    File.Replace(pending, target, null);
                else
                    File.Move(pending, target, overwrite: true);
                return;
            }
            catch (IOException error) when (attempt < retryDelay.Length && IsTransientWindowsError(error))
            {
                Thread.Sleep(retryDelay[attempt]);
            }
        }
    }

    private static void RequireDigest(byte[] bytes, string expected, string message) =>
        FileContent.RequireMatchingSha256(FileContent.Sha256(bytes), expected, message);

    private static bool IsTransientWindowsError(IOException error) =>
        OperatingSystem.IsWindows() && error.HResult is unchecked((int)0x80070020)
            or unchecked((int)0x80070021) or unchecked((int)0x80070497);
}
