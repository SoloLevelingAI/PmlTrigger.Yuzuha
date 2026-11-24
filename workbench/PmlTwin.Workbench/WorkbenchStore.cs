using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core30.Knowledge;
using Core30.PmlTwin;

namespace PmlTwin.Workbench;

internal sealed class WorkbenchStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string DirectoryPath { get; }
    public WorkbenchData Data { get; }
    public string LoadWarning { get; }
    private string CatalogPath => Path.Combine(DirectoryPath, "pmltwin-workbench.json");
    private string SagPath => Path.Combine(DirectoryPath, "pmltwin-sag.json");
    private string PmlPath => Path.Combine(DirectoryPath, "pmltwin-pml.json");
    private string PmlScanPath => Path.Combine(DirectoryPath, "pmltwin-pml-scan.json");
    public SagPreferences Sag { get; private set; } = new();
    public PmlPreferences Pml { get; private set; } = new();
    public PmlScanState PmlScan { get; private set; } = new();
    public string SagLoadWarning { get; } = "";

    private WorkbenchStore(string path, WorkbenchData data, string warning = "")
    {
        DirectoryPath = path;
        Data = data;
        LoadWarning = warning;
        try { Sag = ReadJson<SagPreferences>(SagPath); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        { SagLoadWarning = "SAG 配置无法读取，请检查工作区。"; }
        try
        {
            Pml = ReadJson<PmlPreferences>(PmlPath);
            PmlScan = ReadJson<PmlScanState>(PmlScanPath);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        { PmlScan = new() { Status = "PML 目录缓存无法读取，可手动刷新。", Warning = true }; }
    }

    public static WorkbenchStore Open(string path)
    {
        path = Path.GetFullPath(path);
        var catalog = Path.Combine(path, "pmltwin-workbench.json");
        if (!File.Exists(catalog)) return new(path, new());
        try
        {
            return new(path, JsonSerializer.Deserialize<WorkbenchData>(File.ReadAllText(catalog)) ?? new());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return new(path, new(), "工作区无法读取，请核对资料和日志目录。");
        }
    }

    public void Save()
    {
        if (LoadWarning.Length > 0) throw new IOException("原工作区文件无法读取，修改暂存在本次运行。");
        WriteJson(CatalogPath, Data);
    }

    public void SelectSagCli(string path) => ChangeSag(current => current.CliPath = path);

    public void SaveSagSource(SagSource source) => ChangeSag(current =>
    {
        if (!current.Sources.Contains(source)) current.Sources.Add(source);
    });

    private void ChangeSag(Action<SagPreferences> change)
    {
        Directory.CreateDirectory(DirectoryPath);
        using var gate = File.Open(SagPath + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        var current = ReadJson<SagPreferences>(SagPath);
        change(current);
        WriteJson(SagPath, current);
        Sag = current;
    }

    public void UpdatePmlPreferences(Action<PmlPreferences> change)
    {
        Directory.CreateDirectory(DirectoryPath);
        using var gate = File.Open(PmlPath + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        var current = ReadJson<PmlPreferences>(PmlPath);
        change(current);
        WriteJson(PmlPath, current);
        Pml = current;
    }

    public void SavePmlScan(PmlScanState state)
    {
        WriteJson(PmlScanPath, state);
        PmlScan = state;
    }

    private static T ReadJson<T>(string path) where T : new() =>
        File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? new() : new();

    private void WriteJson<T>(string path, T data)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(data, JsonOptions));
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public string StoreDocument(string source, string id, string extension)
    {
        var dir = Path.Combine(DirectoryPath, "Documents");
        Directory.CreateDirectory(dir);
        var name = id + extension;
        File.Copy(source, Path.Combine(dir, name));
        return name;
    }

    public string StoreExperience(string id, string html)
    {
        var dir = Path.Combine(DirectoryPath, "Documents");
        Directory.CreateDirectory(dir);
        var name = id + ".html";
        File.WriteAllText(Path.Combine(dir, name), html);
        return name;
    }

    public string DocumentPath(DocumentEntry item) => Path.Combine(DirectoryPath, "Documents", Path.GetFileName(item.StoredName));
}

internal sealed class WorkbenchData
{
    public HashSet<string> DismissedLoginChecks { get; set; } = new(StringComparer.Ordinal);
    [JsonIgnore]
    public List<LoginProfile> Profiles { get; set; } = [];
    public List<string> Recent { get; set; } = [];
    public List<LoginProfile> ArchivedProfiles { get; set; } = [];
    public List<PmlFunction> Functions { get; set; } = [];
    public List<PmlLog> Logs { get; set; } = [];
    public List<DocumentEntry> Documents { get; set; } = [];
    public List<RecordedMacro> Macros { get; set; } = [];
    public List<FolderProfile> Folders { get; set; } = [];
    public string ExtraInstallDirectory { get; set; } = "";
    public string DefaultLibrary { get; set; } = "工程资料";
    public string Author { get; set; } = "SoloLevelingAI";
}

internal sealed class SagPreferences
{
    public string CliPath { get; set; } = "";
    public List<SagSource> Sources { get; set; } = [];
}

internal sealed class PmlPreferences
{
    public List<string> SuppliedFolders { get; set; } = [];
    public Dictionary<string, string> EvarsFiles { get; set; } = [];
    public Dictionary<string, string> ReviewedReadOnly { get; set; } = [];
    public PmlResourceDeployment? PhaytonDeployment { get; set; }
}

internal sealed class PmlScanState
{
    public string Stamp { get; set; } = "";
    public string Status { get; set; } = "";
    public bool Warning { get; set; }
    public PmlCatalogFunction[] Functions { get; set; } = [];
    public string[] Notes { get; set; } = [];
}

public sealed record LoginProfile(string Id, string Name, string Install, string Project, string Module, string User, string Mdb, string Product)
{
    public string Target => Project + " / " + Module;
}
public sealed record PmlParameter(string Name, string Type);
public sealed record PmlFunction(string Id, string Detail, string Source, bool AllowInvoke, PmlParameter[] Parameters, string ReturnType)
{
    public bool SourceCurrent { get; init; } = true;
    public string Digest { get; init; } = "";
    public string DiscoveredDeclaration { get; init; } = "";
    public bool FromScan { get; init; }
    public bool CatalogOnly { get; init; }
    public bool Official { get; init; }
    public bool CanRegister { get; init; } = true;
    public bool ReviewedReadOnly { get; init; }
    public string Key => Id.ToUpperInvariant() + "\n" + Source.ToUpperInvariant();
    public string Signature => DiscoveredDeclaration.Length > 0 ? DiscoveredDeclaration : $"{Id}({string.Join(", ", Parameters.Select(item => item.Type))})";
    public string Declaration => $"{Id}({string.Join(", ", Parameters.Select(item => $"!{item.Name} is {item.Type}"))})" +
        (ReturnType.Length > 0 ? " is " + ReturnType : "");
    public string Origin => FromScan ? (Official ? "官方" : "自选") + " · " + (CatalogOnly ? "自动扫描" : "已登记") : "手工登记";
    public string ReadOnlyLabel => ReviewedReadOnly ? "只读" : "非只读";
    public string Policy => !SourceCurrent ? "源码变更" : AllowInvoke ? "已授权" : "待授权";
}
public sealed record PmlLog(string Id, int Sequence, DateTimeOffset When, string Command, string State, int? ExitCode, string Output, string Error)
{
    public string Time => When.ToLocalTime().ToString("HH:mm:ss");
}
public sealed record DocumentEntry(string Id, string Name, string StoredName, string Format, string Source, long Size,
    string Inspection = "待检查", string Preview = "", string Target = "", DateTimeOffset? DeployedAt = null)
{
    public string Status => DeployedAt.HasValue ? "已部署" : Inspection;
}
public sealed record FolderProfile(string Id, string Name, string Source, string Target, string[] Groups, bool Overwrite)
{
    public string Scope => string.Join("、", Groups.Select(group => group.Length > 0 ? group : "根目录文件"));
}
public sealed record MacroReview(string Check, bool Passed, string Advice)
{
    public string Result => Passed ? "通过" : "待完善";
}
public sealed record RecordedMacro(string Id, string Name, DateTime Date, string Form, string Program, string Code,
    MacroReview[]? Review = null)
{
    public string DateText => Date.ToString("yyyy-MM-dd");
    public string ReviewStatus => Review is null ? "待评审" : Review.All(item => item.Passed) ? "检查完整" : "待完善";
}
