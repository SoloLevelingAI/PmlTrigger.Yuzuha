using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using LaunchDesk.Next.Domain.Security;
namespace LaunchDesk.Next.Application.Features.LoginLaunch;

// One editable object. Neutral change notifications let any editor observe backend patches.
// Strings preserve unfinished/invalid port input; conversion is an execution-boundary concern.
public sealed class LaunchModel : IJsonOnDeserializing, IJsonOnDeserialized
{
    public int SchemaVersion { get; set; } = 1;
    public string ConfigurationId { get; set; } = Guid.NewGuid().ToString("N");
    public int Version { get; set; }
    public long Revision { get; set; }
    public Dictionary<string,string> Environment { get; set; } = new();
    public Dictionary<string,string> Sources { get; set; } = new();
    public HashSet<string> EditedFields { get; set; } = new();
    public event Action<string>? Changed;
    private bool loading;
    void IJsonOnDeserializing.OnDeserializing() => loading=true;
    void IJsonOnDeserialized.OnDeserialized() => loading=false;
    private static readonly string[] Editable = [nameof(DisplayName), nameof(ExecutablePath), nameof(WorkingDirectory), nameof(Product), nameof(InitializationFile), nameof(Project), nameof(User), nameof(Mdb), nameof(Module), nameof(LicenceServer), nameof(CredentialReference), nameof(LicencePort), nameof(RulesPort), nameof(LocalRmsServiceName), nameof(Protocol), nameof(PasswordPolicy), nameof(CheckLicence), nameof(RequireRules), nameof(UseLocalRmsService)];
    private string displayName = "";
    public string DisplayName { get => displayName; set => Set(ref displayName, value); }
    private string executablePath = "";
    public string ExecutablePath { get => executablePath; set => Set(ref executablePath, value); }
    private string workingDirectory = "";
    public string WorkingDirectory { get => workingDirectory; set => Set(ref workingDirectory, value); }
    private string product = "";
    public string Product { get => product; set => Set(ref product, value); }
    private string initializationFile = "";
    public string InitializationFile { get => initializationFile; set => Set(ref initializationFile, value); }
    private string project = "";
    public string Project { get => project; set => Set(ref project, value); }
    private string user = "";
    public string User { get => user; set => Set(ref user, value); }
    private string mdb = "";
    public string Mdb { get => mdb; set => Set(ref mdb, value); }
    private string module = "";
    public string Module { get => module; set => Set(ref module, value); }
    private string licenceServer = "";
    public string LicenceServer { get => licenceServer; set => Set(ref licenceServer, value); }
    private string credentialReference = "";
    public string CredentialReference { get => credentialReference; set => Set(ref credentialReference, value); }
    private string licencePort = "0";
    public string LicencePort { get => licencePort; set => Set(ref licencePort, value); }
    private string rulesPort = "5054";
    public string RulesPort { get => rulesPort; set => Set(ref rulesPort, value); }
    private string localRmsServiceName = "Sentinel RMS License Manager_AVEVA";
    public string LocalRmsServiceName { get => localRmsServiceName; set => Set(ref localRmsServiceName, value); }
    private EntryProtocol protocol = EntryProtocol.Monitor;
    public EntryProtocol Protocol { get => protocol; set => Set(ref protocol, value); }
    private PasswordPolicy passwordPolicy = PasswordPolicy.None;
    public PasswordPolicy PasswordPolicy { get => passwordPolicy; set => Set(ref passwordPolicy, value); }
    private bool checkLicence = true;
    public bool CheckLicence { get => checkLicence; set => Set(ref checkLicence, value); }
    private bool requireRules = false;
    public bool RequireRules { get => requireRules; set => Set(ref requireRules, value); }
    private bool useLocalRmsService = false;
    public bool UseLocalRmsService { get => useLocalRmsService; set => Set(ref useLocalRmsService, value); }
    private void Set<T>(ref T field, T value, [CallerMemberName] string name = "")
    {
        if (EqualityComparer<T>.Default.Equals(field,value)) return;
        field=value;
        if (loading) return;
        Revision++; EditedFields.Add(name); Sources[name]="Manual"; Changed?.Invoke(name);
    }
    public LaunchModel Freeze() => JsonSerializer.Deserialize<LaunchModel>(JsonSerializer.Serialize(this))!;
    public void Apply(LaunchModel incoming, bool preserveEdits = false)
    {
        var edited = new HashSet<string>(EditedFields); var previousSources = new Dictionary<string,string>(Sources);
        loading=true;
        try {
            foreach(var name in Editable)
                if (!preserveEdits || !edited.Contains(name))
                    typeof(LaunchModel).GetProperty(name)!.SetValue(this,typeof(LaunchModel).GetProperty(name)!.GetValue(incoming));
            SchemaVersion=incoming.SchemaVersion;ConfigurationId=incoming.ConfigurationId; Version=incoming.Version;
            Environment=new(incoming.Environment); Sources=new(incoming.Sources);
            EditedFields=preserveEdits ? edited : new(incoming.EditedFields);
            if(preserveEdits) foreach(var name in edited) Sources[name]=previousSources.GetValueOrDefault(name,"Manual");
            Revision=Math.Max(Revision,incoming.Revision)+1;
        } finally { loading=false; }
        Changed?.Invoke("");
    }
    public static LaunchModel FromRequest(LaunchRequest r)
    {
        var model=new LaunchModel(); model.loading=true;
        try {
            model.ApplyValues(new LaunchModel { DisplayName = r.DisplayName, ExecutablePath = r.ExecutablePath, WorkingDirectory = r.WorkingDirectory, Product = r.Product, InitializationFile = r.InitializationFile, Project = r.Project, User = r.User, Mdb = r.Mdb, Module = r.Module, LicenceServer = r.LicenceServer, CredentialReference = r.CredentialReference, LicencePort = r.LicencePort.ToString(System.Globalization.CultureInfo.InvariantCulture), RulesPort = r.RulesPort.ToString(System.Globalization.CultureInfo.InvariantCulture), LocalRmsServiceName = r.LocalRmsServiceName, Protocol = r.Protocol, PasswordPolicy = r.PasswordPolicy, CheckLicence = r.CheckLicence, RequireRules = r.RequireRules, UseLocalRmsService = r.UseLocalRmsService });
            model.ConfigurationId=r.ConfigurationId;model.Version=r.Version;
            model.Environment=new(r.Environment);model.Sources=new(r.Sources);model.EditedFields.Clear();model.Revision=0;
        } finally { model.loading=false; }
        return model;
    }
    private void ApplyValues(LaunchModel value)
    { foreach(var name in Editable) typeof(LaunchModel).GetProperty(name)!.SetValue(this,typeof(LaunchModel).GetProperty(name)!.GetValue(value)); }
    public LaunchInput ToInput() => new()
    {
        ConfigurationId=ConfigurationId, Version=Version,
        Environment=new(Environment), Sources=new(Sources),
        DisplayName = DisplayName,
            ExecutablePath = ExecutablePath,
            WorkingDirectory = WorkingDirectory,
            Product = Product,
            InitializationFile = InitializationFile,
            Project = Project,
            User = User,
            Mdb = Mdb,
            Module = Module,
            LicenceServer = LicenceServer,
            CredentialReference = CredentialReference,
            LicencePort = ParsePort(LicencePort, "LicencePort"),
            RulesPort = ParsePort(RulesPort, "RulesPort"),
            LocalRmsServiceName = LocalRmsServiceName,
            Protocol = Protocol,
            PasswordPolicy = PasswordPolicy,
            CheckLicence = CheckLicence,
            RequireRules = RequireRules,
            UseLocalRmsService = UseLocalRmsService
    };
    public static int ParsePort(string text,string field)
    { if(!int.TryParse(text,out var port) || port is < 0 or > 65535) throw new ArgumentException(field+"端口须为0–65535"); return port; }
    public void EnsureNoSecretFields()
    {
        if(SchemaVersion!=1 || Revision<0 || Revision==long.MaxValue || Environment is null || Sources is null || EditedFields is null)
            throw new ArgumentException("Invalid model schema, revision or collections.");
        if(Environment.Keys.Any(SensitiveFields.ContainsSecret)) throw new ArgumentException("Secret environment fields cannot be persisted; use a local credential reference.");
    }
}

public static class LaunchModelRules
{
    public static string EditorError(LaunchModel model)
    {
        if(string.IsNullOrWhiteSpace(model.DisplayName)) return "请填写配置名称";
        if(string.IsNullOrWhiteSpace(model.ExecutablePath)) return "请填写程序路径";
        if(string.IsNullOrWhiteSpace(model.WorkingDirectory)) return "请填写工作目录";
        try { LaunchModel.ParsePort(model.LicencePort,nameof(model.LicencePort)); LaunchModel.ParsePort(model.RulesPort,nameof(model.RulesPort)); }
        catch(ArgumentException e) { return e.Message; }
        return "";
    }
}
