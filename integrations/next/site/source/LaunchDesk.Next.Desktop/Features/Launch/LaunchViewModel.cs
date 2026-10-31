using CommunityToolkit.Mvvm.ComponentModel;
using LaunchDesk.Next.Application.Features.LoginLaunch;
using System.Collections.ObjectModel;
namespace LaunchDesk.Next.Desktop.Features.Launch;

// Binding adapter only: all editable values live in Model, not in backing-field copies.
public sealed class LaunchViewModel : ObservableObject
{
    public LaunchModel Model { get; }
    public LaunchViewModel(LaunchModel? model=null)
    { Model=model??new(); Model.Changed += name => { OnPropertyChanged(name); OnPropertyChanged(nameof(Validation)); OnPropertyChanged(nameof(SourceSummary)); }; }
    public ObservableCollection<DiscoveryCandidate> Candidates { get; } = new();
    public ObservableCollection<LaunchRequest> Configurations { get; } = new();
    public ObservableCollection<EvidenceRow> Evidence { get; } = new();
    public EntryProtocol[] Protocols { get; } = Enum.GetValues<EntryProtocol>();
    public PasswordPolicy[] PasswordPolicies { get; } = Enum.GetValues<PasswordPolicy>();
    public HashSet<string> EditedFields => Model.EditedFields;
    public string Validation => LaunchModelRules.EditorError(Model);
    public string SourceSummary => string.Join(" · ",Model.Sources.Select(x=>$"{x.Key}: {(Model.EditedFields.Contains(x.Key)?"人工修改":x.Value)}"));
    public void Load(LaunchRequest request,bool preserveEdits=false) => Model.Apply(LaunchModel.FromRequest(request),preserveEdits);
    public LaunchInput Capture() => Model.ToInput();
    public string DisplayName { get => Model.DisplayName; set => Model.DisplayName=value; }
    public string ExecutablePath { get => Model.ExecutablePath; set => Model.ExecutablePath=value; }
    public string WorkingDirectory { get => Model.WorkingDirectory; set => Model.WorkingDirectory=value; }
    public string Product { get => Model.Product; set => Model.Product=value; }
    public string InitializationFile { get => Model.InitializationFile; set => Model.InitializationFile=value; }
    public string Project { get => Model.Project; set => Model.Project=value; }
    public string User { get => Model.User; set => Model.User=value; }
    public string Mdb { get => Model.Mdb; set => Model.Mdb=value; }
    public string Module { get => Model.Module; set => Model.Module=value; }
    public string LicenceServer { get => Model.LicenceServer; set => Model.LicenceServer=value; }
    public string CredentialReference { get => Model.CredentialReference; set => Model.CredentialReference=value; }
    public string LicencePort { get => Model.LicencePort; set => Model.LicencePort=value; }
    public string RulesPort { get => Model.RulesPort; set => Model.RulesPort=value; }
    public string LocalRmsServiceName { get => Model.LocalRmsServiceName; set => Model.LocalRmsServiceName=value; }
    public EntryProtocol Protocol { get => Model.Protocol; set => Model.Protocol=value; }
    public PasswordPolicy PasswordPolicy { get => Model.PasswordPolicy; set => Model.PasswordPolicy=value; }
    public bool CheckLicence { get => Model.CheckLicence; set => Model.CheckLicence=value; }
    public bool RequireRules { get => Model.RequireRules; set => Model.RequireRules=value; }
    public bool UseLocalRmsService { get => Model.UseLocalRmsService; set => Model.UseLocalRmsService=value; }
    private string status="就绪 · 自动查找本机应用，或选择已有配置";
    public string Status { get=>status;set=>SetProperty(ref status,value); }
    private bool isBusy;
    public bool IsBusy { get=>isBusy;set=>SetProperty(ref isBusy,value); }
    private EvidenceRow? selectedEvidence;
    public EvidenceRow? SelectedEvidence { get=>selectedEvidence;set=>SetProperty(ref selectedEvidence,value); }
}
public sealed record EvidenceRow(string Title,string Detail);
