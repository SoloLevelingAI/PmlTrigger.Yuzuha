using System.Xml.Linq;
using Core30.Core;

namespace Core30.LaunchDesk;

public static partial class LaunchDeskFeatures
{
    private delegate CoreData ReviewedOperation(FeatureContext workspace, CoreData command);

    private static readonly ImplementationCatalog<string, IVendorLaunchProtocol> VendorProtocols = new(
        [
            new("monitor", new MonitorLaunchProtocol()),
            new("marine", new MarineLaunchProtocol())
        ], StringComparer.Ordinal);

    private static readonly ImplementationCatalog<string, ReviewedOperation> DeploymentOperations = new(
        [
            new("preview", PreviewDeployment),
            new("apply", ApplyDeployment),
            new("inspect", InspectDeploymentRun),
            new("rollback", RollbackDeployment)
        ], StringComparer.Ordinal);

    private static readonly ImplementationCatalog<string, ReviewedOperation> MigrationOperations = new(
        [
            new("preview", PreviewMigration),
            new("apply", ApplyMigration),
            new("verify", VerifyMigrationRun)
        ], StringComparer.Ordinal);

    private static readonly ImplementationCatalog<int, IMigrationWorkspaceCodec> MigrationCodecs =
        new([new(1, new WorkspaceV1Codec())]);

    public static IEnumerable<IFeature> All() => FeatureCatalog.Bind(
        launch: RunF01,
        launchVendor: RunExternalLaunch,
        deploy: RunF02,
        runDeployment: RunDeploymentWorkflow,
        migrate: RunF04,
        runMigration: RunMigrationWorkflow);

    private static string Read(CoreData request, string name) => request.Field(name)!.Value;

    private static string CreateRun(FeatureContext context, string feature)
    {
        var runId = Guid.NewGuid().ToString("N");
        var directory = context.FilePath(Path.Combine("launchdesk-runs", feature, runId));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
