using System.Security.Principal;
using System.Text.Json.Serialization;

namespace LaunchDesk.Windows.Infrastructure;

internal static class AtomicFiles
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true, WriteIndented = true,
        MaxDepth = 64, Converters = { new JsonStringEnumConverter() }
    };

    internal static T Read<T>(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (stream.Length > 32 * 1024 * 1024) throw new InvalidDataException("Stored document exceeds the size limit.");
        return JsonSerializer.Deserialize<T>(stream, Json) ??
               throw new InvalidDataException("Stored document is empty.");
    }

    internal static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                       FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, value, Json);
                stream.Flush(true);
            }

            if (!MoveFileExW(temporary, path, 0x1 | 0x8))
                throw new IOException("Atomic switch failed.", Marshal.GetLastWin32Error());
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal static async ValueTask<FileStream> LockAsync(string path, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1,
                    FileOptions.WriteThrough);
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(35, ct).ConfigureAwait(false);
            }
        }
    }

    internal static bool Expected(Exception ex)
    {
        return ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException
            or ArgumentException or System.ComponentModel.Win32Exception;
    }

    internal static Result<T> Failure<T>(Exception ex, string code = "StoreUnavailable")
    {
        return Result<T>.Fail(
            ex is UnauthorizedAccessException ? "AccessDenied" :
            ex is JsonException or InvalidDataException ? "InvalidFormat" : code,
            ex is UnauthorizedAccessException ? "当前账户没有访问权限。" :
            ex is JsonException or InvalidDataException ? "文件格式或完整性校验失败，请保留原文件。" : "无法完成文件操作；原有资料将保留供核验。", true);
    }

    internal static void ProtectDirectory(string path)
    {
        Directory.CreateDirectory(path);
        var sid = WindowsIdentity.GetCurrent().User?.Value ??
                  throw new UnauthorizedAccessException("No Windows identity.");
        // 受保护的可继承DACL，仅当前用户、SYSTEM及本机管理员。
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(
                $"D:P(A;OICI;FA;;;{sid})(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)", 1, out var descriptor, out _))
            throw new UnauthorizedAccessException("Cannot create a private control directory.");
        try
        {
            if (!SetFileSecurityW(path, 0x4 | 0x80000000, descriptor))
                throw new UnauthorizedAccessException("Cannot secure control data.");
        }
        finally
        {
            LocalFree(descriptor);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileExW(string existing, string destination, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string text, uint revision,
        out IntPtr descriptor, out uint size);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileSecurityW(string path, uint information, IntPtr descriptor);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}