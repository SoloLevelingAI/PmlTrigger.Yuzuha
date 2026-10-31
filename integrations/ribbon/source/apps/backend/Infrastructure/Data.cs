using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
namespace RibbonStudio.Backend
{
    public class ApiError : Exception
    {
        public int Status; public object Issues; public ApiError(int status, string message, object issues = null) : base(message) { Status = status; Issues = issues; }
    }
    public static class Data
    {
        public static readonly object WriteGate = new object();
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true, EntryPoint = "MoveFileExW", ExactSpelling = true)] static extern bool MoveFileEx(string source, string target, int flags);
        public static Dictionary<string, object> Map(object value)
        {
            return value as Dictionary<string, object> ?? new Dictionary<string, object>();
        }
        public static object Get(object value, string key)
        {
            object result;
            return Map(value).TryGetValue(key, out result) ? result : null;
        }
        public static string Text(object value, string key, string fallback = "")
        {
            return Get(value, key) as string ?? fallback;
        }
        public static T As<T>(object value)
        {
            return Json.Read<T>(Json.Write(value));
        }
        public static List<object> Items(object value)
        {
            var array = value as System.Collections.IEnumerable;
            return array == null || value is string ? new List<object>() : array.Cast<object>().ToList();
        }
        public static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        public static string Hash(string text)
        {
            return Hash(Encoding.UTF8.GetBytes(text));
        }
        public static object Canonical(object value)
        {
            var map = value as Dictionary<string, object>;
            if (map != null)
            {
                var result = new SortedDictionary<string, object>(StringComparer.Ordinal);
                foreach (var pair in map)
                    result[pair.Key] = Canonical(pair.Value);
                return result;
            }
            var array = value as object[];
            return array == null ? value : array.Select(Canonical).ToArray();
        }
        public static string CanonicalJson(object value)
        {
            return Json.Serializer().Serialize(Canonical(Json.Read<object>(Json.Write(value))));
        }
        public static string Now()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }
        public static bool Same(string a, string b)
        {
            return !String.IsNullOrEmpty(a) && !String.IsNullOrEmpty(b) && String.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        public static byte[] Read(string path)
        {
            if (!File.Exists(path))
                return null;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new ApiError(422, "目标不能是符号链接");
            return File.ReadAllBytes(path);
        }
        // Extended absolute paths are required at the Win32 boundary independently of
        // .NET runtime switches and the machine-wide LongPathsEnabled policy.
        internal static string NativePath(string path)
        {
            string full = Path.GetFullPath(path);
            if (full.StartsWith(@"\\?\", StringComparison.Ordinal)) return full;
            return full.StartsWith(@"\\", StringComparison.Ordinal)
                ? @"\\?\UNC\" + full.Substring(2)
                : @"\\?\" + full;
        }
        public static void Atomic(string path, byte[] value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Keep the atomic sibling name short when the integrated checkout is deeply nested.
            string temp = Path.Combine(Path.GetDirectoryName(path), Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew))
                    stream.Write(value, 0, value.Length);
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        if (!MoveFileEx(NativePath(temp), NativePath(path), 9))
                        {
                            int code = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                            if (code == 5 && attempt == 5)
                                throw new UnauthorizedAccessException("无法写入目标文件");
                            throw new IOException(new System.ComponentModel.Win32Exception(code).Message);
                        }
                        break;
                    }
                    catch (IOException) { if (attempt == 5) throw; System.Threading.Thread.Sleep(30 * (attempt + 1)); }
                }
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static void AtomicJson(string path, object value)
        {
            Atomic(path, Encoding.UTF8.GetBytes(Json.Write(value)));
        }
    }
}
