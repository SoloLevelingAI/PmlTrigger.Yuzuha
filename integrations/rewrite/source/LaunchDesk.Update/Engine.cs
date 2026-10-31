using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using File = SeaGendManager.ManagedFile;
using Directory = SeaGendManager.ManagedDirectory;

namespace SeaGendManager {
 public sealed class ArchiveInfo { public List<string> Paths = new List<string>(); public long Bytes; public bool SingleSeaGendRoot; public string RootName; }
 public static partial class Engine {
  public static string Tool {get {string bundled=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Tools","7zr.exe");return System.IO.File.Exists(bundled)?bundled:Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7zr.exe");}}
  public static string Hash(string file) { using (var s = File.OpenRead(file)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(s)).Replace("-", ""); }
  public static string Quote(string value) { return "\"" + value.Replace("\"", "").TrimEnd('\\') + "\""; }
  public static string Run7z(string arguments, Action<string> log) {
   if (!File.Exists(Tool)) throw new FileNotFoundException("内置 7zr.exe 缺失，请重新安装管理器。", Tool);
   var si = new ProcessStartInfo(Tool, arguments) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory};
   using (var p = Process.Start(si)) {
    p.StandardInput.Close(); var output = p.StandardOutput.ReadToEndAsync(); var error = p.StandardError.ReadToEndAsync();
    if (!p.WaitForExit(30 * 60 * 1000)) {p.Kill();throw new IOException("解压工具执行超时。");}
    string result = output.Result + error.Result;
    if (p.ExitCode != 0) throw new InvalidDataException("7-Zip 校验 / 解压失败（" + p.ExitCode + "）：\n" + result.Substring(Math.Max(0, result.Length - 1800)));
    if (log != null) log("7-Zip 完成，退出码 0。"); return result;
   }
  }
  public static void ValidateRelative(string name) {
   string p = name.Replace('/', '\\');
   if (string.IsNullOrWhiteSpace(p) || Path.IsPathRooted(p) || p.Contains(":")) throw new InvalidDataException("压缩包含有无效路径：" + name);
   foreach (string part in p.Split('\\')) {
    if (part == "." || part == ".." || part.Length == 0 || part.EndsWith(" ") || part.EndsWith(".") || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
     Regex.IsMatch(part, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", RegexOptions.IgnoreCase)) throw new InvalidDataException("压缩包含有不安全路径：" + name);
   }
   if (p.Split('\\').Any(s=>s.Equals(Store.Internal,StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("压缩包使用了管理器保留目录。");
  }
  public static ArchiveInfo Inspect(string archive) {
   if(Path.GetExtension(archive).Equals(".zip",StringComparison.OrdinalIgnoreCase))return InspectZip(archive);
   if(!Path.GetExtension(archive).Equals(".7z",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("压缩包支持 .7z 和 .zip；其他文件请使用普通文件导入。");
   if (!File.Exists(archive)) throw new FileNotFoundException("找不到所选压缩包。", archive);
   string output = Run7z("l -slt -sccUTF-8 -- " + Quote(Path.GetFullPath(archive)), null);
   int marker = output.IndexOf("----------", StringComparison.Ordinal); if (marker < 0) throw new InvalidDataException("无法读取压缩包目录。");
   var info = new ArchiveInfo(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach (string line in output.Substring(marker).Split(new [] {'\r','\n'}, StringSplitOptions.RemoveEmptyEntries)) {
    if (line.StartsWith("Path = ")) { string name = line.Substring(7).Replace('/', '\\'); ValidateRelative(name); if (!seen.Add(name)) throw new InvalidDataException("压缩包包含重复路径：" + name); info.Paths.Add(name); }
    if (line.StartsWith("Size = ")) {long size; if (long.TryParse(line.Substring(7),out size)) info.Bytes = checked(info.Bytes + size);}
    if (line.StartsWith("Encrypted = +") || line.StartsWith("Symbolic Link =") || line.StartsWith("Hard Link =") || (line.StartsWith("Attributes =") && Regex.IsMatch(line, @"\bl[rwx-]{9}\b"))) throw new InvalidDataException("不支持加密压缩包、符号链接或硬链接。");
   }
   if (info.Paths.Count == 0) throw new InvalidDataException("压缩包为空。");
   info.SingleSeaGendRoot = info.Paths.All(p => p.Equals("SeaGend",StringComparison.OrdinalIgnoreCase) || p.StartsWith("SeaGend\\",StringComparison.OrdinalIgnoreCase));
   string first=info.Paths[0].Split('\\')[0];if(info.Paths.All(p=>p.Equals(first,StringComparison.OrdinalIgnoreCase)||p.StartsWith(first+"\\",StringComparison.OrdinalIgnoreCase))&&info.Paths.Any(p=>p.Contains('\\')))info.RootName=first;
   return info;
  }
  public static string NormalizeTarget(string input) {
   if (string.IsNullOrWhiteSpace(input) || !Path.IsPathRooted(input)) throw new InvalidDataException("请选择完整的目标目录路径。");
   string p = Path.GetFullPath(input).TrimEnd('\\');
   if (p.StartsWith("\\\\") || p.Equals(Path.GetPathRoot(p).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("请选择本机磁盘中的具体目录。");
   foreach (string blocked in new [] {Environment.GetFolderPath(Environment.SpecialFolder.Windows), AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')})
    if (!string.IsNullOrEmpty(blocked) && (p.Equals(blocked,StringComparison.OrdinalIgnoreCase) || p.StartsWith(blocked + "\\",StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("目标目录不能是 Windows 或管理器自身目录。");
   NoLinks(p); return p;
  }
  public static void NoLinks(string path) {
   for (string p = Path.GetFullPath(path); !string.IsNullOrEmpty(p); p = Path.GetDirectoryName(p)) {
    try {if ((File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) throw new IOException("目标路径包含链接或联接点：" + p);} catch(FileNotFoundException) {} catch(DirectoryNotFoundException) {}
   }
  }
  static List<string> Walk(string root, bool files) {
   NoLinks(root);return WalkCore(root,files);
  }
  // Explicit extended paths also work when Windows LongPathsEnabled is disabled.
  internal static IEnumerable<string> Entries(string root) {
   string full=Path.GetFullPath(root);
   return Directory.EnumerateFileSystemEntries(@"\\?\"+full).Select(p=>p.StartsWith(@"\\?\",StringComparison.Ordinal)?p.Substring(4):p);
  }
  static List<string> WalkCore(string root,bool files) {
   var result = new List<string>(); foreach(string p in Entries(root)) {
    var attr=File.GetAttributes(p);if((attr & FileAttributes.ReparsePoint)!=0)throw new IOException("解压内容包含链接："+p);
    if ((attr & FileAttributes.Directory)!=0) { if(!files) result.Add(p); result.AddRange(WalkCore(p,files)); } else if(files) result.Add(p);
   } return result;
  }
  static IDisposable Lock(string target) {
   string key; using(var sha=SHA256.Create()) key=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(target.ToUpperInvariant()))).Replace("-", "");
   var m=new Mutex(false,"Global\\SeaGendManager_"+key); bool held=false;
   try { try {held=m.WaitOne(0);} catch(AbandonedMutexException) {held=true;} if(!held) throw new IOException("此目录已有更新任务正在运行。"); return new Unlock(m); } catch {m.Dispose();throw;}
  }
  sealed class Unlock : IDisposable {Mutex mutex; public Unlock(Mutex m){mutex=m;}public void Dispose(){mutex.ReleaseMutex();mutex.Dispose();}}
 }
}
