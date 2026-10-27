using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace YuzuhaToolkit.Workspace;

/// <summary>后端的 Windows 凭据适配；只接受本应用引用，秘密不写入配置或回填界面。</summary>
internal static class WindowsCredentialStore
{
    private const string Prefix = "YuzuhaToolkit/Profile/";
    /// <summary>留空保留已存凭据；取消保存只解除关联，持久配置成功后再清理旧凭据。</summary>
    /// <param name="current">当前配置的本应用引用。</param>
    /// <param name="password">本次新输入的密码，不写入 JSON 或日志。</param>
    /// <param name="remember">用户是否明确选择保存。</param>
    internal static string Prepare(string current, string password, bool remember)
    {
        if (!remember) return "";
        if (password.Length == 0) return current;
        if (password.Length > 2560 || password.Any(char.IsControl)) throw new ArgumentException("密码长度或字符无效。");
        var key = Prefix + Guid.NewGuid().ToString("N"); var bytes = Encoding.Unicode.GetBytes(password);
        var memory = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, memory, bytes.Length);
            var credential = new Credential { Type = 1, TargetName = key, Comment = "PMLTWIN 本机登录凭据", BlobSize = (uint)bytes.Length, Blob = memory, Persist = 2, UserName = "PMLTWIN", TargetAlias = "" };
            if (!CredWriteW(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows 凭据保存失败，原关联保留。");
            return key;
        }
        finally { Array.Clear(bytes); for (var i = 0; i < bytes.Length; i++) Marshal.WriteByte(memory, i, 0); Marshal.FreeCoTaskMem(memory); }
    }
    /// <summary>仅在启动边界读取保存的凭据，不回填到密码框或日志。</summary>
    /// <param name="key">经本应用前缀和随机 ID 校验的引用。</param>
    internal static string Read(string key)
    {
        Validate(key);
        if (!CredReadW(key, 1, 0, out var pointer)) throw new InvalidOperationException("未找到此配置的本机凭据，请重新输入并保存密码。");
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            if (credential.BlobSize > 5120 || credential.BlobSize % 2 != 0) throw new InvalidDataException("本机凭据格式无效。");
            var bytes = new byte[credential.BlobSize];
            try { Marshal.Copy(credential.Blob, bytes, 0, bytes.Length); return Encoding.Unicode.GetString(bytes); }
            finally { Array.Clear(bytes); }
        }
        finally { CredFree(pointer); }
    }
    /// <summary>清理已解除关联的本应用凭据，不允许操作 LaunchDesk 等其他应用的引用。</summary>
    /// <param name="key">持久配置已不再引用的本应用凭据。</param>
    internal static void Delete(string key)
    {
        if (key.Length == 0) return; Validate(key);
        if (!CredDeleteW(key, 1, 0) && Marshal.GetLastWin32Error() != 1168) throw new Win32Exception(Marshal.GetLastWin32Error(), "旧凭据清理失败。");
    }
    private static void Validate(string key)
    {
        if (!key.StartsWith(Prefix, StringComparison.Ordinal) || !Guid.TryParseExact(key[Prefix.Length..], "N", out _)) throw new ArgumentException("不是本应用的有效凭据引用。");
    }
    /// <summary>Windows CREDENTIALW 的本机互操作布局。</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        /// <summary>Windows 标志与通用凭据类型。</summary>
        public uint Flags, Type;
        /// <summary>本机引用名称与用途说明，不含密码。</summary>
        public string TargetName, Comment;
        /// <summary>系统记录的最后写入时间。</summary>
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        /// <summary>秘密缓冲区的字节长度。</summary>
        public uint BlobSize;
        /// <summary>调用期间持有的秘密缓冲区指针。</summary>
        public IntPtr Blob;
        /// <summary>系统持久范围和附加属性数量。</summary>
        public uint Persist, AttributeCount;
        /// <summary>未使用的附加属性。</summary>
        public IntPtr Attributes;
        /// <summary>凭据别名与本应用所有者标记。</summary>
        public string TargetAlias, UserName;
    }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredWriteW(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredDeleteW(string target, uint type, uint flags);
    [DllImport("advapi32.dll")] private static extern void CredFree(IntPtr pointer);
}
