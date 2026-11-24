using System.Xml.Linq;
using Core30.Core;

namespace Core30.Knowledge;

public static partial class KnowledgeFeatures
{
    public static IEnumerable<IFeature> All()
    {
        var documents = new LocalKnowledgeSearch(ComposeDocumentReaders());
        var sagProcess = new SagWikiProcess();
        foreach (var feature in FeatureCatalog.Bind(
            searchGuides: SearchBuiltInGuides,
            searchWorkspace: QueryWorkspaceKnowledge,
            searchLocalDocuments: documents.Run,
            runSagWiki: (workspace, request) => SagWikiIntegration.Run(workspace, request, sagProcess, SagReleaseFeed)))
        {
            yield return feature;
        }
    }

    private static string Value(CoreData element, string name) => (string?)element.Field(name) ?? "";
}
