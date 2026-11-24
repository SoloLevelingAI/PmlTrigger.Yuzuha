using Core30.Core;

namespace Core30.PmlTwin;

/// <summary>Connects the PmlTwin Feature handlers to their installed providers.</summary>
internal sealed record PmlTwinComposition(ILaunchProjectMetadataReader ProjectReader, AvevaEnvironmentScanner Environment,
    ISessionInventory Sessions, IAvevaHostProbe Host, IEvarsIntegration Evars, PmlDeployment Deployment,
    Func<FeatureContext, IPhaytonFunctionClient> OpenPhaytonClient)
{
    internal static PmlTwinComposition CreateDefault(ILaunchProjectMetadataReader? projectReader = null)
    {
        var evars = new WindowsEvarsIntegration();
        var archives = new ImplementationCatalog<string, IToolPackageSource>([
            new(".zip", new ZipToolPackages()), new(".7z", new SevenZipToolPackages())
        ], StringComparer.OrdinalIgnoreCase);
        return new PmlTwinComposition(projectReader ?? new OptionalProjectReader(),
            new AvevaEnvironmentScanner(new WindowsAvevaEnvironmentInventory()), new WindowsSessionInventory(),
            new PhaytonHostProbe(RegisteredPhaytonCli.FindWorkbenchPrograms), evars,
            new PmlDeployment(evars, archives, new FolderToolPackages()),
            context => new PhaytonFunctionClient(context, RegisteredPhaytonCli.FindWorkbenchPrograms));
    }

    private static ILaunchProjectMetadataReader CreateProjectReader()
    {
        var readers = new ProjectReaderRegistry([
            new(AvevaProduct.AM, new MarineProjectReaderFlow()),
            new(AvevaProduct.E3D, new E3dProjectReaderFlow())
        ]);
        return new AvevaProjectMetadata(readers, CanUseInstalledSdk, AvevaProjectReaderPackage.Executable);
    }

    private sealed class OptionalProjectReader : ILaunchProjectMetadataReader
    {
        private readonly Lazy<ILaunchProjectMetadataReader> _reader = new(CreateProjectReader);

        public LaunchProjectMetadata Read(ProjectReadRequest request) => AvevaProjectReaderPackage.IsInstalled
            ? _reader.Value.Read(request)
            : new("sdk-unavailable", Detail: "当前交付包未安装客户 Reader，可手填登录参数。",
                Sdk: request.Sdk, Diagnostic: new("selection", "reader-not-installed"));
    }

    private static bool CanUseInstalledSdk(LaunchProjectSdk sdk) => OperatingSystem.IsWindows() &&
        WindowsAvevaEnvironmentInventory.IsInstalledSdkLocation(sdk.Directory, sdk.Product);
}
