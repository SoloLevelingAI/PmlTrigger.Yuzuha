using Core30.Core;
using Core30.Generated;

namespace Core30.Backend;

public sealed record LaunchInstallation(string Product, string Release, string Directory, bool CanLaunch)
{
    public string Name => $"{Product} {Release}".Trim();
    public override string ToString() => $"{Name}  ·  {Directory}";
}

public sealed record LaunchInventory(CoreData Receipt, IReadOnlyList<LaunchInstallation> Installations);

/// <summary>Uses the same installation receipt for project, Reader and launch requests.</summary>
public sealed class LaunchDeskWorkflow(ICore30Backend backend, string workspace)
{
    private const string CurrentScan = "current-installation-scan";

    public LaunchInventory Scan() => Scan(directory: null);

    public LaunchInventory Scan(string? directory)
    {
        var scan = backend.Run(workspace, directory is null ? InstallationScanRequests.Scan() : InstallationScanRequests.ScanDirectory(directory));
        var receipt = CoreXml.Read(CoreXml.WriteReceipt(scan, CoreXml.Write));
        var installations = scan.Result.Child("installations")?.Children("installation").Select(row => new LaunchInstallation(
            Value(row, "product"), Value(row, "release"), Value(row, "directory"),
            (bool?)row.Field("launchFilesFound") == true && (Value(row, "product") is "E3D" or "AM"))).ToArray() ?? [];
        return new(receipt, installations);
    }

    public CoreData Catalog(LaunchInventory inventory, LaunchInstallation install, bool refresh = false) => Run(
        ProjectCatalogRequests.Read(CurrentScan, install.Product, install.Directory, refresh), inventory);

    public CoreData Access(LaunchInventory inventory, LaunchInstallation install, string project, string? license, bool refresh = false) =>
        Access(inventory, install, project, license, refresh, default);

    public CoreData Access(LaunchInventory inventory, LaunchInstallation install, string project, bool refresh, CancellationToken cancellation) =>
        Access(inventory, install, project, null, refresh, cancellation);

    public CoreData Access(LaunchInventory inventory, LaunchInstallation install, string project, string? license, bool refresh,
        CancellationToken cancellation) => Run(
        ProjectAccessRequests.Read(CurrentScan, install.Product, install.Directory, project.ToUpperInvariant(), refresh, license),
        inventory, cancellation);

    public CoreData Profiles() => Run(LoginProfileRequests.List(product: null));

    public CoreData Save(LaunchInventory inventory, LaunchInstallation install, string id, string label, string revision,
        string project, string user, string mdb, string module, string workDirectory, string licenseHost, int licensePort,
        string? uicDirectory = null) => SaveProfile(inventory, install, id, label, revision,
            LoginProfileRequests.LaunchSettings(workDirectory, project, user, mdb, module, licenseHost, licensePort, uicDirectory));

    public CoreData Save(LaunchInventory inventory, LaunchInstallation install, string id, string label, string revision,
        string project, string user, string mdb, string module, string workDirectory, string? uicDirectory = null) =>
        SaveProfile(inventory, install, id, label, revision,
            LoginProfileRequests.LaunchSettings(workDirectory, project, user, mdb, module, null, null, uicDirectory));

    private CoreData SaveProfile(LaunchInventory inventory, LaunchInstallation install, string id, string label, string revision,
        CoreData settings) => Run(LoginProfileRequests.Save(CurrentScan, install.Product, install.Directory, id, label, revision, settings), inventory);

    public CoreData Archive(string id, string revision) => Run(LoginProfileRequests.Archive(id, revision));

    public CoreData Prepare(LaunchInventory inventory, LaunchInstallation install, string id, string revision) =>
        Run(LoginProfileRequests.Prepare(CurrentScan, install.Product, install.Directory, id, revision), inventory);

    public CoreData Prepare(LaunchInventory inventory, LaunchInstallation install, string id, string revision, bool inheritWorkstationEnvironment)
    {
        var prepared = Prepare(inventory, install, id, revision);
        if (inheritWorkstationEnvironment && prepared.Children().SingleOrDefault() is { } request)
        {
            request.SetField("licenseHost", null);
            request.SetField("licensePort", null);
        }
        return prepared;
    }

    public CoreData Preview(CoreData prepared) => Run(PreparedLaunch.Preview(prepared));

    public CoreData Launch(CoreData prepared, Func<string?> readPassword) =>
        backend.Run(workspace, PreparedLaunch.Confirm(prepared), new RunInput(ReadPassword: readPassword)).Result;

    private CoreData Run(CoreRequest request, LaunchInventory? inventory = null, CancellationToken cancellation = default) =>
        backend.Run(workspace, request, inventory is null ? null : new RunInput(InstallationScan: inventory.Receipt) { Cancellation = cancellation }).Result;

    private static string Value(CoreData data, string name) => data.Field(name)?.Value ?? "";
}
