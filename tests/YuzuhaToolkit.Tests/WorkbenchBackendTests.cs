using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Workspace;
using Xunit;

namespace YuzuhaToolkit.Tests;

/// <summary>后端功能可脱离桌面程序集使用；保留既有回执兼容性。</summary>
public sealed class WorkbenchBackendTests
{
    [Fact]
    public void QueryAndSessionAdaptersDoNotReferencePresentationAssemblies()
    {
        var types = new[] { typeof(PmlObjectQuery), typeof(WorkbenchHost), typeof(PmlDevelopmentHost), typeof(WorkbenchReply), typeof(LocalDocumentationHost) };
        foreach (var type in types)
        {
            Assert.DoesNotContain(type.Assembly.GetReferencedAssemblies(), a => a.Name is "PresentationFramework" or "PresentationCore" or "YuzuhaToolkit.Desktop");
            Assert.NotEqual("YuzuhaToolkit.Desktop", type.Assembly.GetName().Name);
        }
        Assert.Contains("YuzuhaReadGlobal", PmlObjectQuery.Build("global", "!!fmsys", "forms.1", 30, 2).Expression);
    }

    [Theory]
    [InlineData("{\"Success\":true,\"Items\":null,\"Unparsed\":null}")]
    [InlineData("{\"Success\":false}")]
    public void OptionalListValuesRemainCompatibleWithExistingReplies(string json)
    {
        var reply = PmlObjectReadReply.Parse(json);
        Assert.Empty(reply.Items); Assert.Empty(reply.Unparsed); Assert.Equal(json, reply.Raw);
    }

    [Fact]
    public void MissingReceiptOrSuccessfulListIsUnknownRatherThanSuccess()
    {
        Assert.Throws<IOException>(() => WorkbenchReply.ParseRegisteredCall("{}"));
        Assert.Throws<IOException>(() => PmlObjectReadReply.Parse("{\"Success\":true}"));
    }

    [Fact]
    public async Task IncompleteDocumentationInstallationFailsBeforeStartingAService()
    {
        using var host = new LocalDocumentationHost(Path.Combine(Path.GetTempPath(), "phayton-no-install-" + Guid.NewGuid().ToString("N")));
        await Assert.ThrowsAsync<FileNotFoundException>(() => host.GetAddressAsync());
    }
}
