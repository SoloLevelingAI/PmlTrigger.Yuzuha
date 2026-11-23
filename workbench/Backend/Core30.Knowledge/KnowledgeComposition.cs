using Core30.Core;

namespace Core30.Knowledge;

public static partial class KnowledgeFeatures
{
    // Each supported suffix chooses a parser before the file is read.
    private static ImplementationCatalog<string, KnowledgeTextReader> ComposeDocumentReaders() => new([
        new(".md", KnowledgeTextExtraction.ReadPlainText),
        new(".txt", KnowledgeTextExtraction.ReadPlainText),
        new(".csv", KnowledgeTextExtraction.ReadPlainText),
        new(".json", KnowledgeTextExtraction.ReadPlainText),
        new(".xml", KnowledgeTextExtraction.ReadPlainText),
        new(".html", KnowledgeTextExtraction.ReadHtmlDocument),
        new(".htm", KnowledgeTextExtraction.ReadHtmlDocument),
        new(".docx", KnowledgeTextExtraction.ReadWordDocument)
    ], StringComparer.OrdinalIgnoreCase);

    private static readonly ISagWikiReleaseFeed SagReleaseFeed = CreateSagReleaseFeed();

    private static ISagWikiReleaseFeed CreateSagReleaseFeed()
    {
        var http = new HttpClient(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(10) })
        { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = 64 * 1024 * 1024 };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AVEVA-Toolbox/30");
        return new SagWikiReleaseFeed(http);
    }
}
