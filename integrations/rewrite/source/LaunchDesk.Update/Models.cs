using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using LaunchDesk.Core;

using File = SeaGendManager.ManagedFile;
using Directory = SeaGendManager.ManagedDirectory;

namespace SeaGendManager {
 public sealed class Plan { public string Source {get;set;} public string Target {get;set;} public string Id {get;set;} public bool Flatten {get;set;} public bool KeepBackup {get;set;} public string DataRoot {get;set;} public ToolSourceKind SourceKind {get;set;} public List<ToolFile> SourceFiles {get;set;} = new List<ToolFile>(); public string ToolId {get;set;} public string ToolName {get;set;} }
 public sealed class FileChange {
  public string Path {get;set;} public bool Existed {get;set;} public bool Touched {get;set;} public string Hash {get;set;}
  public string BeforeHash {get;set;} public string BeforeWriteTimeUtc {get;set;} public long BeforeBytes {get;set;}
 }
 public sealed class Receipt {
  public string Id {get;set;} public string Archive {get;set;} public string Target {get;set;} public string Started {get;set;} public string Finished {get;set;}
  public string Status {get;set;} public string Message {get;set;} public string ArchiveHash {get;set;} public bool Flattened {get;set;}
  public List<FileChange> Files {get;set;} = new List<FileChange>(); public List<string> CreatedDirectories {get;set;} = new List<string>();
  public int Schema {get;set;} public bool KeepBackup {get;set;} public bool CanRollback {get;set;}
  public string BackupArchive {get;set;} public string BackupHash {get;set;} public long BackupBytes {get;set;} public long BeforeBytes {get;set;}
  public int BackupFiles {get;set;} public int SkippedFiles {get;set;} public int PackageFiles {get;set;}
  public string MigratedFromHash {get;set;}
  public string ToolId {get;set;} public string ToolName {get;set;} public ToolSourceKind SourceKind {get;set;}
 }
 public sealed class Package {
  public string Path {get;set;} public string Name {get {return System.IO.Path.GetFileName(Path);}} public long Bytes {get;set;} public DateTime Modified {get;set;}
  public override string ToString() { return Name + "   ·   " + (Bytes / 1048576.0).ToString("0.0") + " MB   ·   " + Modified.ToString("MM-dd HH:mm"); }
 }
 public static partial class Store {
  public const string Internal = ".seagend-manager";
  public static readonly JsonSerializerOptions Json = new JsonSerializerOptions {PropertyNameCaseInsensitive=true,WriteIndented=true,MaxDepth=64};
  public static string Serialize(object value) { return JsonSerializer.Serialize(value,Json); }
  public static T Read<T>(string file) { if(new FileInfo(LongPaths.Io(file)).Length>32*1024*1024)throw new InvalidDataException("记录文件过大。");return JsonSerializer.Deserialize<T>(File.ReadAllText(file, Encoding.UTF8),Json) ?? throw new InvalidDataException("记录为空。"); }
  public static Receipt LoadReceipt(string file) {
   var r=Read<Receipt>(file);string journal=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file),"touch.journal");
   if(File.Exists(journal)) foreach(string line in File.ReadAllLines(journal)) {int index;if(int.TryParse(line,out index) && index>=0 && index<r.Files.Count)r.Files[index].Touched=true;}
   return r;
  }
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool MoveFileEx(string existing, string destination, uint flags);
  public static void Save(string file, object value) {
   string temp = file + ".tmp"; byte[] bytes=Encoding.UTF8.GetBytes(Serialize(value));
   using(var stream=new FileStream(LongPaths.Io(temp),FileMode.Create,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
   if(!MoveFileEx(LongPaths.Io(temp),LongPaths.Io(file),0x9)) throw new Win32Exception(Marshal.GetLastWin32Error(),"无法保存更新记录。");
  }
  public static string Transaction(string target, string id) {
   Guid parsed; if (!Guid.TryParseExact(id, "N", out parsed)) throw new InvalidDataException("更新记录编号无效。");
   return System.IO.Path.Combine(RecordsRoot(target), id);
  }
  [DllImport("shell32.dll")] static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr path);
  public static string Downloads() {
   Guid id = new Guid("374DE290-123F-4565-9164-39C4925E467B"); IntPtr p;
   if (SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out p) == 0) { try { return Marshal.PtrToStringUni(p); } finally { Marshal.FreeCoTaskMem(p); } }
   return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
  }
  public static string DefaultTarget() {
   foreach (EnvironmentVariableTarget scope in new [] {EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine}) {
    string value = Environment.GetEnvironmentVariable("SeagendSetup", scope);
    if (!string.IsNullOrWhiteSpace(value)) return Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
   } return "";
  }
  public static List<Package> Scan(string directory) {
   if (!Directory.Exists(directory)) return new List<Package>();
   return Directory.EnumerateFiles(directory, "*.7z").Where(p => Regex.IsMatch(System.IO.Path.GetFileName(p), @"^SeaGend(?:\(\d+\))?\.7z$", RegexOptions.IgnoreCase))
    .Select(p => new FileInfo(LongPaths.Io(p))).OrderByDescending(f => f.LastWriteTimeUtc).ThenByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase)
    .Select(f => new Package {Path=LongPaths.Normal(f.FullName),Bytes=f.Length,Modified=f.LastWriteTime}).ToList();
  }
  public static List<Receipt> History(string target) {
   var records = new List<Receipt>(); if (string.IsNullOrWhiteSpace(target)) return records;
   var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(string root in new [] {RecordsRoot(target),System.IO.Path.Combine(target,Internal)}) {
    if(!Directory.Exists(root))continue;
    foreach(string d in Engine.Entries(root).Where(Directory.Exists)) {
     try {
      var r=LoadReceipt(System.IO.Path.Combine(d,"receipt.json"));
      if(r!=null && ids.Add(r.Id))records.Add(r);
     } catch(JsonException ex) {throw new InvalidDataException("更新记录 JSON 损坏，已停止读取；请保留记录并检查："+d,ex);} catch(IOException) {} catch(ArgumentException) {} catch(UnauthorizedAccessException) {}
    }
   }
   return records.OrderByDescending(r => r.Started, StringComparer.Ordinal).ToList();
  }
 }
}
