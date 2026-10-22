#nullable disable
using System;
using System.IO;
using System.Security.Cryptography;

namespace YuzuhaToolkit.Pml
{
    /// <summary>Host 在执行前核验源码快照；兼容 NET35，不加载或保存工程。</summary>
    public static class PmlSourceIdentity
    {
        /// <summary>核验最多 1 MiB 的本机源码，防止检查后被替换的文件继续执行。</summary>
        public static void Verify(string path, string expectedSha256)
        {
            if (String.IsNullOrEmpty(path) || String.IsNullOrEmpty(expectedSha256) || expectedSha256.Length != 64 || !Path.IsPathRooted(path))
                throw new InvalidDataException("PML_SOURCE_IDENTITY_REQUIRED");
            if (new FileInfo(path).Length > 1048576) throw new InvalidDataException("PML_SOURCE_TOO_LARGE");
            using (FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 hash = SHA256.Create())
            {
                string actual = BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                if (!String.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("PML_SOURCE_CHANGED");
            }
        }
        /// <summary>生成带引号的宏调用，拒绝会被 PML 展开或截断的路径字符。</summary>
        public static string MacroCommand(string path)
        {
            if (path.IndexOfAny(new char[] { '"', '$', '%', '\r', '\n' }) >= 0) throw new ArgumentException("PML_MACRO_PATH_UNSUPPORTED");
            return "$M\"" + path + "\"";
        }
    }
}
