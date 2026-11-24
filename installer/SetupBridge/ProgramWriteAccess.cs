using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json.Linq;

/// <summary>检查程序文件的写入权限并原子替换安装标记。</summary>
internal static partial class Program
{
    const uint DeleteAccess = 0x00010000;
    const uint ReplaceExisting = 0x00000001;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFile(string path, uint access, FileShare share, IntPtr security,
        FileMode mode, uint flags, IntPtr template);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool MoveFileEx(string existing, string destination, uint flags);

    static void ReplaceProgramMarker(string path, byte[] content)
    {
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temp, content);
            if (!MoveFileEx(temp, path, ReplaceExisting))
                throw ProgramWriteError(path, new Win32Exception(Marshal.GetLastWin32Error()));
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    static void RequireProgramWriteAccess(JObject plan, bool retainUnchanged)
    {
        string root = (string)plan["root"];
        var paths = ((JArray)plan["payload"]).OfType<JObject>()
            .Where(item => !retainUnchanged || (string)item["action"] != "unchanged").Select(item => (string)item["path"])
            .Concat(new[] { Marker(root), Path.Combine(root, ".setup-payload.json") });
        var checkedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in paths)
        {
            if (Directory.Exists(path))
                throw Bad("文件目标已是一个目录 / A folder occupies the program file path: " + path);
            if (File.Exists(path))
            {
                using (var handle = CreateFile(path, DeleteAccess, FileShare.ReadWrite | FileShare.Delete,
                    IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero))
                {
                    if (handle.IsInvalid)
                        throw ProgramWriteError(path, new Win32Exception(Marshal.GetLastWin32Error()));
                }
            }

            string directory = Path.GetDirectoryName(path);
            while (!Directory.Exists(directory))
            {
                if (File.Exists(directory))
                    throw Bad("目录目标已是一个文件 / A file occupies the program directory path: " + directory);
                directory = Path.GetDirectoryName(directory);
            }
            if (!checkedDirectories.Add(directory)) continue;
            try
            {
                string probe = Path.Combine(directory, ".phayton-write-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.DeleteOnClose)) { }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                throw ProgramWriteError(directory, error);
            }
        }
    }

    static Exception ProgramWriteError(string path, Exception error)
    {
        int code = error is Win32Exception win32 ? win32.NativeErrorCode : error.HResult & 0xffff;
        string reason = code == 32 || code == 33
            ? "文件正在被占用。关闭使用此文件的程序后可重试。 / The file is in use. Close the application using it, then retry."
            : "Windows 无法替换此路径。请检查目录权限或文件是否被占用，再重试。 / Windows cannot replace this path. Check access and file usage, then retry.";
        return new IOException(reason + "\r\n" + path + "\r\nWindows " + code + ": " + error.Message, error);
    }
}
