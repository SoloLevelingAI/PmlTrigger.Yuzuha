using System.Collections.ObjectModel;
namespace LaunchDesk.Next.Application.Features.LoginLaunch;

public enum EntryProtocol { Monitor, Marine, Direct }
public enum PasswordPolicy { None, Prompt, Stored }
public enum LaunchStatus { Checking, Starting, Created, WindowObserved, Unconfirmed, Exited, Rejected, Failed, Cancelled, Busy, NeedsVerification }
public sealed class LaunchInput
{
    public string ConfigurationId { get; set; } = Guid.NewGuid().ToString("N");
    public int Version { get; set; }
    public string DisplayName { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public EntryProtocol Protocol { get; set; }
    public string Product { get; set; } = "";
    public string InitializationFile { get; set; } = "";
    public string Project { get; set; } = "";
    public string User { get; set; } = "";
    public string Mdb { get; set; } = "";
    public string Module { get; set; } = "";
    public string LicenceServer { get; set; } = "";
    public int LicencePort { get; set; }
    public bool CheckLicence { get; set; } = true;
    public bool RequireRules { get; set; }
    public int RulesPort { get; set; } = 5054;
    public bool UseLocalRmsService { get; set; }
    public string LocalRmsServiceName { get; set; } = "Sentinel RMS License Manager_AVEVA";
    public PasswordPolicy PasswordPolicy { get; set; }
    public string CredentialReference { get; set; } = "";
    public Dictionary<string, string> Environment { get; set; } = new();
    public Dictionary<string, string> Sources { get; set; } = new();
    public LaunchRequest CreateSnapshot(Guid attemptId) => new(attemptId, ConfigurationId, Version, DisplayName, ExecutablePath, WorkingDirectory, Protocol, Product, InitializationFile, Project, User, Mdb, Module, LicenceServer, LicencePort, CheckLicence, PasswordPolicy, CredentialReference, new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(Environment)), new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(Sources)), RequireRules, RulesPort, UseLocalRmsService, LocalRmsServiceName);
    public static LaunchInput FromRequest(LaunchRequest r) => new() { ConfigurationId = r.ConfigurationId, Version = r.Version, DisplayName = r.DisplayName, ExecutablePath = r.ExecutablePath, WorkingDirectory = r.WorkingDirectory, Protocol = r.Protocol, Product = r.Product, InitializationFile = r.InitializationFile, Project = r.Project, User = r.User, Mdb = r.Mdb, Module = r.Module, LicenceServer = r.LicenceServer, LicencePort = r.LicencePort, CheckLicence = r.CheckLicence, RequireRules = r.RequireRules, RulesPort = r.RulesPort, UseLocalRmsService = r.UseLocalRmsService, LocalRmsServiceName = r.LocalRmsServiceName, PasswordPolicy = r.PasswordPolicy, CredentialReference = r.CredentialReference, Environment = new(r.Environment), Sources = new(r.Sources) };
}
public sealed record LaunchRequest(Guid AttemptId, string ConfigurationId, int Version, string DisplayName, string ExecutablePath, string WorkingDirectory, EntryProtocol Protocol, string Product, string InitializationFile, string Project, string User, string Mdb, string Module, string LicenceServer, int LicencePort, bool CheckLicence, PasswordPolicy PasswordPolicy, string CredentialReference, IReadOnlyDictionary<string, string> Environment, IReadOnlyDictionary<string, string> Sources, bool RequireRules = false, int RulesPort = 5054, bool UseLocalRmsService = false, string LocalRmsServiceName = "Sentinel RMS License Manager_AVEVA");
public sealed record LaunchResult(Guid AttemptId, Guid? RunId, LaunchStatus Status, string Message, int? ProcessId = null);
public sealed record LaunchEvent(long Sequence, LaunchResult Result);
public sealed record SaveResult(bool Success, LaunchRequest? Configuration, string Message);
