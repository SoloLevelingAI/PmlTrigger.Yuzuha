using System.Text.Json.Serialization;

namespace LaunchDesk.Core;

public enum EntryKind { Monitor, MarineBatch }
public enum RmsCheckKind { Tcp, LocalService }
public sealed class AppEntry
{
 public string Id { get; set; } = Guid.NewGuid().ToString("N");
 public string Name { get; set; } = "新应用";
 public string Version { get; set; } = "";
 public string FileVersion { get; set; } = "";
 public EntryKind Kind { get; set; }
 public string ExecutablePath { get; set; } = "";
 public string InitializationPath { get; set; } = "";
 public string WorkingDirectory { get; set; } = "";
 public string ProductCode { get; set; } = "E3D";
 [JsonIgnore] public string KindLabel => Kind == EntryKind.Monitor ? "mon.exe · PROD / INIT" : "Marine · 批处理入口";
 [JsonIgnore] public string PathStatus => File.Exists(ExecutablePath) ? "路径有效" : "路径待修复";
 public override string ToString() => Name;
}
public sealed class LicenseEntry
{
 public string Id { get; set; } = Guid.NewGuid().ToString("N");
 public string Name { get; set; } = "新许可连接";
 public string Host { get; set; } = "localhost";
 public int RmsPort { get; set; } = 4545;
 public bool RequireRules { get; set; } = true;
 public int RulesPort { get; set; } = 4546;
 public RmsCheckKind RmsCheck { get; set; }
 public string LocalServiceName { get; set; } = "Sentinel RMS License Manager_AVEVA";
 public string VmPath { get; set; } = "";
 public string VmwarePath { get; set; } = "";
 public int PollSeconds { get; set; } = 5;
 public bool Enabled { get; set; } = true;
 [JsonIgnore] public string Address => $"{RmsPort}@{Host}";
 public override string ToString() => $"{Name}  ·  {Address}";
}
public sealed class LoginProfile
{
 public string Id { get; set; } = Guid.NewGuid().ToString("N");
 public string Name { get; set; } = "新登录配置";
 public string VersionGroup { get; set; } = "E3D";
 public string AppId { get; set; } = "";
 public string LicenseId { get; set; } = "";
 public string Project { get; set; } = "mar";
 public string Username { get; set; } = "SYSTEM";
 public string Mdb { get; set; } = "/ALL_NO_MDS";
 public string Module { get; set; } = "design";
 public string CredentialKey { get; set; } = "";
 public bool AskForPassword { get; set; } = true;
 public List<string> ToolIds { get; set; } = [];
 [JsonIgnore] public bool ExportSelected { get; set; } = true;
 [JsonIgnore] public string LoginSummary => $"{Project}  /  {Username}  /  {Mdb.TrimStart('/')}  /  {Module}";
}
public sealed class Workspace
{
 public int SchemaVersion { get; set; } = 1;
 public List<AppEntry> Applications { get; set; } = [];
 public List<LicenseEntry> Licenses { get; set; } = [];
 public List<LoginProfile> Profiles { get; set; } = [];
 public List<string> SearchRoots { get; set; } = [];
 public List<ToolEntry> Tools { get; set; } = [];
 public bool NotifyOnDisconnect { get; set; } = true;
}
public sealed record ProbeResult(string LicenseId, bool RmsOk, bool RulesOk, string RmsMessage, string RulesMessage, long Milliseconds, DateTimeOffset CheckedAt)
{
 public bool Ready => RmsOk && RulesOk;
 public int Attempts {get;init;}=1;
 public int ConsecutiveFailures {get;init;}
 public bool Rechecking=>!Ready&&ConsecutiveFailures is >0 and <3;
 public string Summary => Ready ? "连接就绪" : Rechecking?"连接复查中":"连接未就绪";
}
public sealed record ImportPreview(Workspace Data, IReadOnlyList<string> Notes);
