using LaunchDesk.Next.Domain.Security;
using LaunchDesk.Next.Application.Features.LoginLaunch.Scripts;
namespace LaunchDesk.Next.Application.Features.LoginLaunch;

public static class LaunchValidation
{
    public static string? Validate(LaunchRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.ConfigurationId) || string.IsNullOrWhiteSpace(r.DisplayName) || string.IsNullOrWhiteSpace(r.ExecutablePath) || string.IsNullOrWhiteSpace(r.WorkingDirectory)) return "Name, executable and working directory are required.";
        if (r.Protocol != EntryProtocol.Direct && new[] { r.Project, r.User, r.Mdb, r.Module }.Any(string.IsNullOrWhiteSpace)) return "Project, user, MDB and module are required.";
        if (r.CheckLicence && (string.IsNullOrWhiteSpace(r.LicenceServer) || r.LicencePort is < 1 or > 65535)) return "Licence server and TCP port are required.";
        if (r.CheckLicence && r.RequireRules && r.RulesPort is < 1 or > 65535) return "RULES TCP port must be between 1 and 65535.";
        if (r.CheckLicence && r.UseLocalRmsService && string.IsNullOrWhiteSpace(r.LocalRmsServiceName)) return "Local RMS service name is required.";
        if (r.PasswordPolicy == PasswordPolicy.Stored && string.IsNullOrWhiteSpace(r.CredentialReference)) return "Stored password requires a credential reference.";
        if (r.Environment.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Key.Contains('=') || p.Key.Any(char.IsControl) || p.Value.Any(char.IsControl) || SensitiveFields.ContainsSecret(p.Key) || p.Key.Equals("AVEVA_LICENSE_SERVER_LIST", StringComparison.OrdinalIgnoreCase))) return "Tool environment contains invalid, reserved or secret fields.";
        try { BuildArguments(r); } catch (ArgumentException ex) { return ex.Message; }
        return null;
    }
    public static string[] BuildArguments(LaunchRequest r) => r.Protocol switch { EntryProtocol.Monitor => StartMonitor.BuildArguments(r), EntryProtocol.Marine => StartMarineBatch.BuildArguments(r), _ => [] };
}
