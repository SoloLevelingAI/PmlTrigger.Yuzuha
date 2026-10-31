using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Plugins.Vm;

/// <summary>本插件独有的当前用户 DPAPI 文档，不接受其他应用的凭据引用。</summary>
public sealed record VmProtectedCredential(string Owner, string TargetKey, string ProtectedPassword);
/// <summary>秘密存储只返回位置和目标绑定，不返回秘密或密文。</summary>
public sealed record VmCredentialReceipt(string File, string TargetKey);

/// <summary>加密 VM 的密码只在工作进程内解密并传给原生 API，从不放入 argv。</summary>
public static class VmCredentials
{
    private const string Owner = "Yuzuha.Manager.Plugin.Vm/1";
    /// <summary>稳定绑定本机 VM 路径、UUID 和 MAC，与具体电源任务分离。</summary>
    public static string TargetKey(string vmPath, string uuid, string mac) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(VmPlanning.Local(vmPath).ToUpperInvariant() + "\0" + uuid.ToUpperInvariant() + "\0" + mac.ToUpperInvariant()))).ToLowerInvariant();

    /// <summary>明确写授权之后才调用；创建当前用户和目标绑定的保护文件。</summary>
    public static VmCredentialReceipt Store(string directory, string targetKey, string password)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("VM 保护凭据仅支持本机 Windows 用户。");
        ValidateKey(targetKey); targetKey = targetKey.ToLowerInvariant(); directory = VmPlanning.Local(directory); PathRules.NoLinks(directory);
        if (password.Contains('\0')) throw new ArgumentException("VM 密码不能含 NUL。");
        var plaintext = Encoding.UTF8.GetBytes(password);
        byte[] encrypted;
        try { if (plaintext.Length is < 1 or > 8192) throw new ArgumentException("VM 密码长度无效。"); encrypted = Crypt(plaintext, targetKey, true); } finally { CryptographicOperations.ZeroMemory(plaintext); }
        Directory.CreateDirectory(directory); var file = PathRules.Inside(directory, targetKey + ".json"); var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var value = new VmProtectedCredential(Owner, targetKey, Convert.ToBase64String(encrypted));
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(output, value, VmJsonContext.Default.VmProtectedCredential); output.Flush(true); }
            File.Move(temporary, file, true);
        }
        finally { CryptographicOperations.ZeroMemory(encrypted); if (File.Exists(temporary)) File.Delete(temporary); }
        return new(file, targetKey);
    }

    /// <summary>校验本插件所有者、目标绑定和文件预算后，以当前 Windows 用户解密；调用方负责清零返回的明文字节。</summary>
    internal static byte[] Read(string file, string targetKey)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("VM 保护凭据仅支持本机 Windows 用户。");
        ValidateKey(targetKey); targetKey = targetKey.ToLowerInvariant(); PathRules.NoLinks(file);
        using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > 65536) throw new IOException("VM 保护文档超过预算。");
        var value = JsonSerializer.Deserialize(input, VmJsonContext.Default.VmProtectedCredential) ?? throw new InvalidDataException("VM 保护文档为空。");
        if (value.Owner != Owner || value.TargetKey != targetKey) throw new InvalidDataException("不是本插件拥有且匹配目标的保护引用。");
        var encrypted = Convert.FromBase64String(value.ProtectedPassword);
        try { return Crypt(encrypted, targetKey, false); } finally { CryptographicOperations.ZeroMemory(encrypted); }
    }
    private static void ValidateKey(string key) { if (key.Length != 64 || !key.All(Uri.IsHexDigit)) throw new ArgumentException("VM 目标绑定无效。"); }
    private static byte[] Crypt(byte[] bytes, string key, bool protect)
    {
        var entropy = SHA256.HashData(Encoding.UTF8.GetBytes(Owner + "\0" + key));
        var input = Blob.Create(bytes); var extra = Blob.Create(entropy); var output = default(Blob);
        try
        {
            var ok = protect ? CryptProtectData(ref input, null, ref extra, IntPtr.Zero, IntPtr.Zero, 1, out output) :
                CryptUnprotectData(ref input, IntPtr.Zero, ref extra, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new IOException("本机用户保护操作失败，Win32=" + Marshal.GetLastWin32Error());
            var result = new byte[output.Length]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
        }
        finally
        {
            input.Clear(); extra.Clear(); CryptographicOperations.ZeroMemory(entropy);
            if (output.Data != IntPtr.Zero) { for (var index = 0; index < output.Length; index++) Marshal.WriteByte(output.Data, index, 0); LocalFree(output.Data); }
        }
    }
    /// <summary>与 DPAPI DATA_BLOB 对齐的长度及原生缓冲区地址；输入缓冲区由本结构创建和清除。</summary>
    [StructLayout(LayoutKind.Sequential)] private struct Blob
    {
        /// <summary>缓冲区的字节数，供 DPAPI 和清零循环使用。</summary>
        public int Length;
        /// <summary>缓冲区首地址；输入由 AllocHGlobal 分配，DPAPI 输出由 LocalFree 释放。</summary>
        public IntPtr Data;
        /// <summary>分配非托管内存并复制给定字节，返回长度和地址。</summary>
        public static Blob Create(byte[] value) { var data = Marshal.AllocHGlobal(value.Length); Marshal.Copy(value, 0, data, value.Length); return new() { Length = value.Length, Data = data }; }
        /// <summary>逐字节清零本结构创建的输入缓冲区，释放内存并将地址置零。</summary>
        public void Clear() { if (Data == IntPtr.Zero) return; for (var index = 0; index < Length; index++) Marshal.WriteByte(Data, index, 0); Marshal.FreeHGlobal(Data); Data = IntPtr.Zero; }
    }
    [DllImport("crypt32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptProtectData(ref Blob data, string? description, ref Blob entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("crypt32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptUnprotectData(ref Blob data, IntPtr description, ref Blob entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
}
