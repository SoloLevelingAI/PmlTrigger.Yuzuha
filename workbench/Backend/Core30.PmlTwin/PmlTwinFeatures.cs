using System.Xml.Linq;
using Core30.Core;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    public static IEnumerable<IFeature> All() => CreateFeatures(PmlTwinComposition.CreateDefault());

    internal static IEnumerable<IFeature> All(ILaunchProjectMetadataReader projectReader) =>
        CreateFeatures(PmlTwinComposition.CreateDefault(projectReader));

    internal static IEnumerable<IFeature> CreateFeatures(PmlTwinComposition composition) =>
        FeatureCatalog.Bind(
            listProfiles: ListWorkspaceProfiles,
            shareProfiles: ShareWorkspaceProfiles,
            manageProfiles: ManageLaunchProfiles,
            readProjectCatalog: ReadLaunchCatalog,
            readProjectAccess: (context, request) => ReadProjectAccess(context, request, composition.ProjectReader),
            inspectSession: (_, request) => InspectAvevaSession(request, composition.Sessions),
            scanEnvironment: (_, request) => ScanLocalAvevaEnvironment(request, composition.Environment),
            readHostStatus: (_, request) => ReadAvevaHostStatus(request, composition.Host),
            readLoginSession: (context, request) => new AvevaLoginSession(composition.OpenPhaytonClient(context)).Read(request),
            registerFunction: RegisterPmlFunction,
            deployPml: composition.Deployment.Run,
            deployPackage: composition.Deployment.RunPackage,
            queryFunctions: QueryPmlFunctionCatalog,
            runInstalledTools: (context, request) => RunInstalledPmlTools(context, request, composition.Evars),
            deployLocalFiles: composition.Deployment.RunLocalFiles,
            runPhayton: (context, request) => RunPhaytonFunctions(context, request, composition.OpenPhaytonClient(context)),
            verifyLocalFiles: composition.Deployment.RunFileVerification,
            readArrayWindow: ReadArrayWindow,
            filterActivity: FilterActivityRecords,
            readActivity: ReadWorkspaceActivity);

    private static string Value(CoreData input, string name, string fallback = "") =>
        (string?)input.Field(name) ?? fallback;

    private static string Value(XElement input, string name) => XmlValues.AttributeText(input, name);

}
