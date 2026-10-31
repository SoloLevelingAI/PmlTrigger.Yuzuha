using System.Diagnostics;
using System.Text.RegularExpressions;

namespace LaunchDesk.Core;

public enum ToolSourceKind { Archive, Folder, Files }
public enum VariableScope { Process, User, Machine }
public sealed class ToolFile
{
 public string SourcePath { get; set; } = "";
 public string RelativePath { get; set; } = "";
}
public sealed class ToolVariable
{
 public string Id { get; set; } = Guid.NewGuid().ToString("N");
 public string Name { get; set; } = "ToolRoot";
 public string Value { get; set; } = "{Target}";
 public VariableScope Scope { get; set; }
 public string AppliedName { get; set; } = "";
 public string AppliedValue { get; set; } = "";
 public VariableScope AppliedScope { get; set; }
 public bool WasApplied { get; set; }
}
public sealed class ToolEntry
{
 public string Id { get; set; } = Guid.NewGuid().ToString("N");
 public string Name { get; set; } = "新工具";
 public string Group { get; set; } = "自定义工具";
 public string TargetDirectory { get; set; } = "";
 public ToolSourceKind SourceKind { get; set; } = ToolSourceKind.Folder;
 public string SourcePath { get; set; } = "";
 public string ArchiveSearchDirectory { get; set; } = "";
 public List<ToolFile> Files { get; set; } = [];
 public List<ToolVariable> Variables { get; set; } = [];
 public bool FlattenRoot { get; set; } = true;
 public bool KeepBackup { get; set; }
 public string RecordsDirectory { get; set; } = "";
 public override string ToString() => Name;
}
public static class ToolEnvironment
{
 public static void ValidateVariable(ToolVariable v)
 {
  if (v is null || string.IsNullOrWhiteSpace(v.Id) || string.IsNullOrWhiteSpace(v.Name) || !Regex.IsMatch(v.Name, @"^[A-Za-z_][A-Za-z0-9_]{0,127}$") || v.Value is null || v.Value.Contains('\0') || v.Value.Length > 32760 || !Enum.IsDefined(v.Scope) || !Enum.IsDefined(v.AppliedScope)) throw new InvalidDataException("变量名请使用字母、数字和下划线，且以字母或下划线开头。");
  if (v.Name.Equals("AVEVA_LICENSE_SERVER_LIST", StringComparison.OrdinalIgnoreCase) || v.Name.StartsWith("LAUNCHDESK_", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("此变量名由启动器管理，请在许可连接中设置。");
 }
 public static void ValidateTool(ToolEntry t)
 {
  if (t is null || string.IsNullOrWhiteSpace(t.Name) || t.Name.Length>200 || t.Group is null || t.SourcePath is null || t.ArchiveSearchDirectory is null || t.TargetDirectory is null || t.RecordsDirectory is null || !Enum.IsDefined(t.SourceKind) || t.Variables is null || t.Files is null || t.Variables.Count>100 || t.Files.Count>10000) throw new InvalidDataException("工具配置不完整或条目过多。");
  var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase); var ids=new HashSet<string>(StringComparer.Ordinal);
  foreach(var v in t.Variables) { ValidateVariable(v); if(!names.Add(v.Name)||!ids.Add(v.Id))throw new InvalidDataException("同一工具中包含重复的环境变量名称或标识。"); }
  if(t.Files.Any(f=>f is null || f.SourcePath is null || f.RelativePath is null))throw new InvalidDataException("文件映射不完整。");
 }
 public static Dictionary<string,string> Resolve(IEnumerable<ToolEntry> tools)
 {
  var raw=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  foreach(var t in tools) { ValidateTool(t);foreach(var v in t.Variables){var value=v.Value.Replace("{Target}",t.TargetDirectory,StringComparison.OrdinalIgnoreCase);if(raw.TryGetValue(v.Name,out var old)&&old!=value)throw new InvalidDataException($"关联工具对变量 {v.Name} 设置了不同的值，请先协调。");raw[v.Name]=value;} }
  var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  string Expand(string name,HashSet<string> chain){if(result.TryGetValue(name,out var cached))return cached;if(!chain.Add(name))throw new InvalidDataException("环境变量存在循环引用："+name);var value=Regex.Replace(raw[name],@"%([^%]+)%",m=>raw.ContainsKey(m.Groups[1].Value)?Expand(m.Groups[1].Value,chain):Environment.GetEnvironmentVariable(m.Groups[1].Value)??m.Value);chain.Remove(name);if(value.Length>32760)throw new InvalidDataException("展开后的环境变量值过长。");return result[name]=value;}
  foreach(var key in raw.Keys)Expand(key,new(StringComparer.OrdinalIgnoreCase));return result;
 }
 public static void ApplyToStart(ProcessStartInfo start,IEnumerable<ToolEntry> tools)
 {
  foreach(var pair in Resolve(tools))start.Environment[pair.Key]=pair.Value;
 }
 public static void StripMachineState(ToolEntry tool)
 {
  tool.RecordsDirectory="";
  foreach(var v in tool.Variables){v.AppliedName="";v.AppliedValue="";v.WasApplied=false;v.AppliedScope=VariableScope.Process;}
 }
}
