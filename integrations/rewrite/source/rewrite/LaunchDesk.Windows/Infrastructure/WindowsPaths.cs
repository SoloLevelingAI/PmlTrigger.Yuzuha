using System.ComponentModel;

namespace LaunchDesk.Windows.Infrastructure;

internal static class WindowsPaths
{
    internal sealed class Pin(List<SafeFileHandle> handles, CanonicalPath path) : IDisposable
    {
        internal CanonicalPath Path { get; } = path;

        public void Dispose()
        {
            foreach (var handle in handles) handle.Dispose();
        }
    }

    internal static string Full(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith(@"\\", StringComparison.Ordinal) ||
            path.AsSpan(Math.Min(2, path.Length)).Contains(':') || path.IndexOfAny(['\0', '*', '?']) >= 0)
            throw new InvalidDataException("Only local normal file-system paths are supported.");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var root = Path.GetPathRoot(full)!;
        if (!string.Equals(new DriveInfo(root).DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Only local NTFS is supported for writes.");
        return full;
    }

    internal static bool Overlap(string first, string second)
    {
        return Within(first, second) || Within(second, first);
    }

    internal static bool Within(string path, string root)
    {
        return path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(
            System.IO.Path.TrimEndingDirectorySeparator(root) + System.IO.Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    internal static string CombineRelative(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':') || relative
                .Split(['/', '\\']).Any(p => p is ".." or "." or "" || p.EndsWith(' ') || p.EndsWith('.')))
            throw new InvalidDataException("Invalid relative mapping.");
        var result = Full(Path.Combine(root, relative));
        if (!Within(result, Full(root)) || result.Equals(Full(root), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Mapping escapes the target.");
        return result;
    }

    internal static Pin PinPath(string path)
    {
        var full = Full(path);
        var handles = new List<SafeFileHandle>();
        try
        {
            var root = Path.GetPathRoot(full)!;
            var current = root;
            string? identity = null;
            var resolved = root;
            foreach (var part in new[] { "" }.Concat(full[root.Length..]
                         .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)))
            {
                if (part.Length > 0) current = Path.Combine(current, part);
                if (!Directory.Exists(current) && !File.Exists(current))
                {
                    var suffix = Path.GetRelativePath(Path.GetDirectoryName(current)!, full);
                    return new Pin(handles,
                        new CanonicalPath(Path.Combine(resolved, suffix),
                            "missing:" + identity + "/" + suffix.ToUpperInvariant(), false, true));
                }

                var handle = Open(current, 0x80, 0x1 | 0x2, 3, 0x02000000 | 0x00200000);
                if (!GetFileInformationByHandle(handle, out var info))
                {
                    handle.Dispose();
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if ((info.Attributes & 0x400) != 0 || ((info.Attributes & 0x10) == 0 && info.Links > 1))
                {
                    handle.Dispose();
                    throw new InvalidDataException("Reparse points and multiply linked files are not supported.");
                }

                handles.Add(handle);
                identity = Identity(info);
                resolved = FinalName(handle);
            }

            var last = handles[^1];
            GetFileInformationByHandle(last, out var lastInfo);
            return new Pin(handles, new CanonicalPath(resolved, identity!, true, (lastInfo.Attributes & 0x10) != 0));
        }
        catch
        {
            foreach (var handle in handles) handle.Dispose();
            throw;
        }
    }

    internal static void CreatePinnedDirectory(string path, List<Pin> pins)
    {
        var full = Full(path);
        var root = Path.GetPathRoot(full)!;
        var current = root;
        foreach (var part in full[root.Length..]
                     .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            Directory.CreateDirectory(current);
            var pin = PinPath(current);
            if (!pin.Path.Path.Equals(current, StringComparison.OrdinalIgnoreCase))
            {
                pin.Dispose();
                throw new InvalidDataException("Directory identity changed.");
            }

            pins.Add(pin);
        }
    }

    private static string FinalName(SafeFileHandle handle)
    {
        var buffer = new StringBuilder(32768);
        var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
        if (length == 0 || length >= buffer.Capacity) throw new Win32Exception(Marshal.GetLastWin32Error());
        var value = buffer.ToString();
        if (value.StartsWith(@"\\?\", StringComparison.Ordinal)) value = value[4..];
        return Path.TrimEndingDirectorySeparator(value);
    }

    internal static FileStream OpenStableFile(string path, bool write, bool create)
    {
        var handle = Open(Full(path), write ? 0x80000000 | 0x40000000 : 0x80000000, 0x1, create ? 1u : 3u,
            0x00200000 | (write ? 0x80000000u : 0));
        try
        {
            if (!GetFileInformationByHandle(handle, out var info))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if ((info.Attributes & (0x400 | 0x10)) != 0 || info.Links > 1)
                throw new InvalidDataException("The target is a directory, link or multiply linked file.");
            return new FileStream(handle, write ? FileAccess.ReadWrite : FileAccess.Read);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    internal static void DeleteStable(string path, string expectedHash)
    {
        using var handle = Open(Full(path), 0x80000000 | 0x10000, 0x1, 3, 0x00200000);
        if (!GetFileInformationByHandle(handle, out var info) || (info.Attributes & (0x400 | 0x10)) != 0 ||
            info.Links > 1) throw new InvalidDataException("Invalid deletion target.");
        using var input = new FileStream(handle, FileAccess.Read);
        if (Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(input)) != expectedHash)
            throw new InvalidDataException("Deletion target changed.");
        var disposition = new FileDisposition { Delete = true };
        if (!SetFileInformationByHandle(handle, 4, ref disposition, (uint)Marshal.SizeOf<FileDisposition>()))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDisposition
    {
        [MarshalAs(UnmanagedType.Bool)] public bool Delete;
    }

    internal static SafeFileHandle Open(string path, uint access, uint share, uint disposition, uint flags)
    {
        var handle = CreateFileW(path, access, share, IntPtr.Zero, disposition, flags, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            if (error == 5) throw new UnauthorizedAccessException("Access denied.");
            throw new Win32Exception(error);
        }

        return handle;
    }

    private static string Identity(FileInformation info)
    {
        return $"{info.Volume:X8}:{info.IndexHigh:X8}{info.IndexLow:X8}";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, uint length,
        uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass,
        ref FileDisposition information, uint size);
}