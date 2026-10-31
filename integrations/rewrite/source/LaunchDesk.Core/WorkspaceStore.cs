using System.Text.Json;
using System.Text.Json.Serialization;

namespace LaunchDesk.Core;

public sealed class WorkspaceStore(string path)
{
 public string Path { get; } = System.IO.Path.GetFullPath(path);
 public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() }, MaxDepth = 32 };
 private readonly SemaphoreSlim gate = new(1, 1);
 public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json), Json)!;
 public async Task<Workspace?> LoadAsync()
 {
  if (!File.Exists(Path)) return null;
  var data = await ReadFileAsync(Path);
  Validate(data); return data;
 }
 public async Task SaveAsync(Workspace workspace)
 {
  Validate(workspace);
  await gate.WaitAsync();
  try {
   Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
   string temp = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
   try {
    await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(workspace, Json));
    if (File.Exists(Path)) File.Copy(Path, Path + ".backup", true);
    File.Move(temp, Path, true);
   } finally { if (File.Exists(temp)) File.Delete(temp); }
  } finally { gate.Release(); }
 }
 public static async Task<Workspace> ReadFileAsync(string path)
 {
  if (new FileInfo(path).Length > 5 * 1024 * 1024) throw new InvalidDataException("配置文件不能大于 5 MB。");
  await using var stream = File.OpenRead(path);
  return await JsonSerializer.DeserializeAsync<Workspace>(stream, Json) ?? throw new InvalidDataException("配置文件为空。");
 }
 public static void Validate(Workspace value)
 {
  if (value.SchemaVersion != 1) throw new InvalidDataException("配置版本不受支持，请使用版本 1 的配置文件。");
  if (value.Applications is null || value.Licenses is null || value.Profiles is null || value.SearchRoots is null || value.Tools is null) throw new InvalidDataException("配置缺少必要的列表。");
  if (value.Applications.Count > 500 || value.Licenses.Count > 500 || value.Profiles.Count > 2000 || value.SearchRoots.Count > 100) throw new InvalidDataException("配置条目过多。");
  CheckIds(value.Applications.Select(a => a?.Id)); CheckIds(value.Licenses.Select(l => l?.Id)); CheckIds(value.Profiles.Select(p => p?.Id));
  if(value.Tools.Count>500)throw new InvalidDataException("工具条目过多。");CheckIds(value.Tools.Select(t=>t?.Id));foreach(var tool in value.Tools)ToolEnvironment.ValidateTool(tool);
  foreach (var a in value.Applications) {
   if (string.IsNullOrWhiteSpace(a.Name) || !Enum.IsDefined(a.Kind) || a.ExecutablePath is null || a.InitializationPath is null || a.WorkingDirectory is null || a.ProductCode is null) throw new InvalidDataException("应用记录不完整。");
  }
  foreach (var l in value.Licenses) ValidateLicense(l);
  foreach (var p in value.Profiles) {
   if (string.IsNullOrWhiteSpace(p.Name) || p.Project is null || p.Username is null || p.Mdb is null || p.Module is null || p.VersionGroup is null || p.CredentialKey is null) throw new InvalidDataException("登录配置不完整。");
   if (!value.Applications.Any(a => a.Id == p.AppId) || !value.Licenses.Any(l => l.Id == p.LicenseId)) throw new InvalidDataException($"配置“{p.Name}”引用的应用或许可不存在。");
   if(p.ToolIds is null || p.ToolIds.Count>500 || p.ToolIds.Distinct().Count()!=p.ToolIds.Count || p.ToolIds.Any(id=>!value.Tools.Any(t=>t.Id==id)))throw new InvalidDataException($"配置“{p.Name}”引用的工具不存在或重复。");
  }
 }
 public static void ValidateLicense(LicenseEntry l)
 {
  if (string.IsNullOrWhiteSpace(l.Name) || string.IsNullOrWhiteSpace(l.Host) || l.Host.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or ':' or '_'))) throw new InvalidDataException("许可地址请填写主机名或 IP，不包含端口、@ 或空格。");
  if (l.RmsPort is < 1 or > 65535 || l.RulesPort is < 1 or > 65535 || l.PollSeconds is < 2 or > 300 || !Enum.IsDefined(l.RmsCheck)) throw new InvalidDataException("端口需为 1–65535，检测间隔需为 2–300 秒。");
  if (l.RmsCheck == RmsCheckKind.LocalService && l.Host is not ("localhost" or "127.0.0.1" or "::1")) throw new InvalidDataException("本机服务检测只能用于 localhost / 127.0.0.1 / ::1。");
  if (l.LocalServiceName is null || l.VmPath is null || l.VmwarePath is null) throw new InvalidDataException("许可记录不完整。");
 }
 private static void CheckIds(IEnumerable<string?> ids)
 {
  var set = new HashSet<string>(StringComparer.Ordinal);
  foreach (var id in ids) if (string.IsNullOrWhiteSpace(id) || id.Length > 100 || !set.Add(id)) throw new InvalidDataException("配置包含空白或重复的标识。");
 }
 public static async Task ExportAsync(Workspace source, IEnumerable<string> profileIds, string path, bool includeTools = true)
 {
  var selected = profileIds.ToHashSet(StringComparer.Ordinal);
  var export = Clone(source);
  export.Profiles.RemoveAll(p => !selected.Contains(p.Id));
  if (export.Profiles.Count == 0) throw new InvalidDataException("请至少选择一个登录配置。");
  export.Applications.RemoveAll(a => !export.Profiles.Any(p => p.AppId == a.Id));
  export.Licenses.RemoveAll(l => !export.Profiles.Any(p => p.LicenseId == l.Id));
  foreach (var p in export.Profiles) { p.CredentialKey = ""; p.AskForPassword = true; }
  export.Tools.RemoveAll(t=>!includeTools || !export.Profiles.Any(p=>p.ToolIds.Contains(t.Id)));
  foreach(var t in export.Tools)ToolEnvironment.StripMachineState(t);
  if(!includeTools)foreach(var p in export.Profiles)p.ToolIds.Clear();
  export.SearchRoots.Clear();
  Validate(export);
  await File.WriteAllTextAsync(path, JsonSerializer.Serialize(export, Json));
 }
 public static async Task<ImportPreview> PreviewAsync(string path, IReadOnlyList<AppEntry> localApps)
 {
  var data = await ReadFileAsync(path); Validate(data);
  var notes = new List<string>();
  foreach (var app in data.Applications) {
   if (File.Exists(app.ExecutablePath)) { notes.Add($"{app.Name}：原程序路径在本机存在。"); continue; }
   var matches = localApps.Where(a => a.Kind == app.Kind && a.Version == app.Version && File.Exists(a.ExecutablePath)).ToList();
   if (matches.Count == 1) {
    app.ExecutablePath = matches[0].ExecutablePath; app.InitializationPath = matches[0].InitializationPath; app.WorkingDirectory = matches[0].WorkingDirectory;
    notes.Add($"{app.Name}：已匹配同版本本机入口。");
   } else notes.Add($"{app.Name}：路径待修复，请在导入后手动指定程序。");
  }
  foreach (var p in data.Profiles) { p.CredentialKey = ""; p.AskForPassword = true; notes.Add($"{p.Name}：登录参数将自动填入，密码需在本机补充。"); }
  foreach(var t in data.Tools){ToolEnvironment.StripMachineState(t);notes.Add($"{t.Name}：工具目录与变量名需在本机确认；导入不会部署文件或写入环境变量。");}
  return new ImportPreview(data, notes);
 }
 public static Workspace Merge(Workspace current, Workspace incoming)
 {
  Validate(current); Validate(incoming); var merged = Clone(current); var data = Clone(incoming);
  var apps = new Dictionary<string, string>(); var licenses = new Dictionary<string, string>(); var tools=new Dictionary<string,string>();
  foreach(var t in data.Tools){var original=t.Id;t.Id=Guid.NewGuid().ToString("N");tools[original]=t.Id;t.Name=UniqueName(t.Name,merged.Tools.Select(x=>x.Name));ToolEnvironment.StripMachineState(t);merged.Tools.Add(t);}
  foreach (var a in data.Applications) {
   var original = a.Id; var same = merged.Applications.FirstOrDefault(x => x.Kind == a.Kind && string.Equals(x.ExecutablePath, a.ExecutablePath, StringComparison.OrdinalIgnoreCase) && string.Equals(x.InitializationPath, a.InitializationPath, StringComparison.OrdinalIgnoreCase));
   if (same is not null) apps[original] = same.Id;
   else { a.Id = Guid.NewGuid().ToString("N"); apps[original] = a.Id; merged.Applications.Add(a); }
  }
  foreach (var l in data.Licenses) { var original = l.Id; l.Id = Guid.NewGuid().ToString("N"); licenses[original] = l.Id; l.Name = UniqueName(l.Name, merged.Licenses.Select(x => x.Name)); merged.Licenses.Add(l); }
  foreach (var p in data.Profiles) { p.Id = Guid.NewGuid().ToString("N"); p.AppId = apps[p.AppId]; p.LicenseId = licenses[p.LicenseId]; p.ToolIds=p.ToolIds.Select(id=>tools[id]).ToList(); p.Name = UniqueName(p.Name, merged.Profiles.Select(x => x.Name)); p.CredentialKey = ""; p.AskForPassword = true; merged.Profiles.Add(p); }
  Validate(merged); return merged;
 }
 public static string UniqueName(string name, IEnumerable<string> names)
 {
  var existing = names.ToHashSet(StringComparer.OrdinalIgnoreCase); if (!existing.Contains(name)) return name;
  for (var i = 2; ; i++) if (!existing.Contains($"{name} ({i})")) return $"{name} ({i})";
 }
}
