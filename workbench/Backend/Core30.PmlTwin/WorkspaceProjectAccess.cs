using System.Xml.Linq;
using Core30.Core;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    /// <summary>Looks up one scanned project and reports the source of its login candidates.</summary>
    private static CoreData ReadProjectAccess(FeatureContext context, CoreData request, ILaunchProjectMetadataReader projectReader)
    {
        context.Input?.Cancellation.ThrowIfCancellationRequested();
        var operation = request.Children().Single();
        var installation = ReadSelectedInstallation(context, operation);
        var (catalog, _) = LaunchCatalogCache.Read(context, installation.Product, installation.Release, installation.Directory, refresh: false);
        var code = Value(operation, "project");
        var project = catalog.Projects.FirstOrDefault(item => item.Code == code);
        var modules = InstalledLaunchModules.Read(installation.Product, installation.Directory);
        var sdk = new LaunchProjectSdk(Enum.Parse<AvevaProduct>(installation.Product), installation.Release, installation.Directory);
        var licenseOverride = (string?)operation.Field("licenseServers");
        var data = project is { Found: true }
            ? projectReader.Read(new(context, sdk, project, modules, (bool?)operation.Field("refresh") == true, licenseOverride))
            : new LaunchProjectMetadata("system-not-found", Detail: "所选项目没有找到本机系统库。");
        return ProjectAccessResult(installation, code, project, modules, data, licenseOverride is not null);
    }

    private static CoreData ProjectAccessResult(LaunchInstallation installation, string code, LaunchProjectSource? project,
        InstalledLaunchModule[] modules, LaunchProjectMetadata data, bool licenseOverride)
    {
        var sdk = data.Values is { } values ? CoreXml.Read(values) : null;
        var defined = sdk?.Children("module").ToDictionary(item => Value(item, "name"), item => (bool?)item.Field("defined"),
            StringComparer.OrdinalIgnoreCase);
        return new CoreData("result", new CoreField("product", installation.Product), new CoreField("project", code),
            new CoreField("installation", installation.Directory), new CoreField("systemDatabase", project?.SystemDatabase ?? ""),
            new CoreField("status", data.State), new CoreField("cache", data.Cache), new CoreField("detail", data.Detail),
            new CoreField("licenseSource", licenseOverride ? "request" : "workstation"),
            new CoreField("moduleSource", defined is { Count: > 0 } ? "project" : "installation"),
            sdk is null ? null : new CoreField("source", "AVEVA SDK Project.Open(code)"),
            data.Flow is null ? null : new CoreField("readerFlow", data.Flow),
            data.Diagnostic is { } diagnostic ? new[]
            {
                new CoreField("readerStage", diagnostic.Stage), new CoreField("readerCode", diagnostic.Code)
            } : null,
            data.Sdk is not { } reader ? null : new[]
            {
                new CoreField("readerProduct", reader.Product), new CoreField("readerRelease", reader.Release),
                new CoreField("readerInstallation", reader.Directory)
            },
            sdk?.Field("loggedIn"), sdk?.Field("windowsAuthenticated"),
            data.Diagnostic?.Missing?.Select(name => new CoreData("missingDependency", new CoreField("name", name))),
            sdk?.Children().Where(item => item.Name == "user" || item.Name == "mdb"),
            modules.Select(module => module.ToData(defined?.GetValueOrDefault(module.Name))));
    }
}
