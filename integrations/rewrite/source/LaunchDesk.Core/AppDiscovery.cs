using System.Diagnostics;
using System.Text.RegularExpressions;

namespace LaunchDesk.Core;

public static partial class AppDiscovery
{
 public static readonly string[] DefaultRoots = [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "AVEVA"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AVEVA"), @"C:\AVEVA", @"D:\AVEVA"];
 public static Task<List<AppEntry>> FindAsync(IEnumerable<string> roots, CancellationToken token = default) => Task.Run(() => {
  var found = new List<AppEntry>(); var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var queue = new Queue<(string, int)>();
  foreach (var root in roots.Where(Directory.Exists)) queue.Enqueue((root, 0));
  while (queue.Count > 0 && visited.Count < 10000) {
   token.ThrowIfCancellationRequested(); var (dir, depth) = queue.Dequeue(); if (!visited.Add(Path.GetFullPath(dir))) continue;
   try {
    foreach (var file in Directory.EnumerateFiles(dir)) {
     var name = Path.GetFileName(file);
     if (name.Equals("mon.exe", StringComparison.OrdinalIgnoreCase) && dir.Contains("Everything3D", StringComparison.OrdinalIgnoreCase)) found.Add(FromPath(file));
     else if (name.Equals("marine.bat", StringComparison.OrdinalIgnoreCase)) found.Add(FromPath(file));
    }
    if (depth < 4) foreach (var child in Directory.EnumerateDirectories(dir)) if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) queue.Enqueue((child, depth + 1));
   } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
  }
  return found.DistinctBy(a => a.ExecutablePath, StringComparer.OrdinalIgnoreCase).OrderByDescending(a => a.Version).ToList();
 }, token);
 public static AppEntry FromPath(string path)
 {
  path = Path.GetFullPath(path); var dir = Path.GetDirectoryName(path)!;
  var marine = Path.GetExtension(path).Equals(".bat", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".cmd", StringComparison.OrdinalIgnoreCase);
  var info = marine ? null : FileVersionInfo.GetVersionInfo(path);
  var match = VersionPattern().Match(dir);
  var version = marine ? MarineVersionPattern().Match(dir).Value : InstallationVersion(path);
  if (!marine && (string.IsNullOrWhiteSpace(version) || version == "0.0.0.0")) version = match.Success ? match.Groups[1].Value : "未识别";
  var userDir = marine ? @"C:\AVEVA\Marine\Data12.1.SP4\pdmsuser" : @"C:\Users\Public\Documents\AVEVA\USERDATA";
  return new AppEntry { Name = marine ? "AM " + version : "E3D " + version, Version = version, FileVersion=info?.ProductVersion??"", Kind = marine ? EntryKind.MarineBatch : EntryKind.Monitor, ExecutablePath = path, InitializationPath = marine ? "" : Path.Combine(dir, "launch.init"), WorkingDirectory = Directory.Exists(userDir) ? userDir : dir, ProductCode = "E3D" };
 }
 public static string InstallationVersion(string path){var match=VersionPattern().Match(Path.GetDirectoryName(path)??"");if(!match.Success)return "未识别";return match.Groups[1].Value=="2.10"?"2.1":match.Groups[1].Value;}
 [GeneratedRegex(@"Everything3D([\d.]+)", RegexOptions.IgnoreCase)] private static partial Regex VersionPattern();
 [GeneratedRegex(@"\d+\.\d+(?:\.SP\d+)?", RegexOptions.IgnoreCase)] private static partial Regex MarineVersionPattern();
}
