using System.Text.Json;
using Xunit;
using YuzuhaToolkit.Manager;

namespace YuzuhaToolkit.Manager.Tests;

/// <summary>提案契约不得把逻辑名称登记成运行时操作。</summary>
public sealed class PlatformProposalContractTests
{
    [Fact]
    public void VersionedProposalMapsOnlyRealOperationsAndKeepsLogicalNamesUnregistered()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "openspec", "changes", "integrate-pmltwin-product-platform", "contracts.proposal.json")))
            root = root.Parent;
        Assert.NotNull(root);
        var directory = Path.Combine(root!.FullName, "openspec", "changes", "integrate-pmltwin-product-platform");
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "contracts.proposal.json")));
        var proposal = json.RootElement;
        Assert.Equal("pmltwin.platform-proposal/0.2", proposal.GetProperty("schema").GetString());
        Assert.Equal("proposal", proposal.GetProperty("status").GetString());
        Assert.True(proposal.GetProperty("notARuntimeCatalog").GetBoolean());
        Assert.Equal("pmltwin.manager-journey/1", proposal.GetProperty("runtimeSchema").GetString());
        var markdown = File.ReadAllText(Path.Combine(directory, "contracts.md"));
        Assert.Contains("pmltwin.platform-proposal/0.2", markdown);
        Assert.Contains("不是已注册", markdown);

        var registry = ManagerOperationCatalog.Create().Registry;
        foreach (var type in proposal.GetProperty("types").EnumerateArray())
        {
            Assert.True(type.GetProperty("registeredOperationId").ValueKind == JsonValueKind.Null, type.GetProperty("name").GetString());
            Assert.False(registry.TryGet(type.GetProperty("name").GetString()!, out _), type.GetProperty("name").GetString());
            if (type.GetProperty("implementationStatus").GetString() == "proposal")
            {
                var schema = type.GetProperty("schema");
                var properties = schema.GetProperty("properties");
                foreach (var required in schema.GetProperty("required").EnumerateArray())
                    Assert.True(properties.TryGetProperty(required.GetString()!, out _), required.GetString());
            }
            else Assert.False(string.IsNullOrWhiteSpace(type.GetProperty("returnedBy").GetString()));
        }
        foreach (var family in proposal.GetProperty("families").EnumerateArray())
        {
            var logical = family.GetProperty("logicalName").GetString()!;
            var ids = family.GetProperty("operationIds").EnumerateArray().Select(item => item.GetString()!).ToArray();
            if (family.GetProperty("status").GetString() == "proposal-only")
            {
                Assert.Empty(ids);
                Assert.False(registry.TryGet(logical, out _), logical);
            }
            else Assert.All(ids, id => Assert.True(registry.TryGet(id, out _), id));
        }
        foreach (var sample in proposal.GetProperty("compatibilitySamples").EnumerateArray())
        {
            var found = registry.TryGet(sample.GetProperty("operationId").GetString()!, out var registration);
            Assert.Equal(sample.GetProperty("expectRegistered").GetBoolean(), found);
            if (found && sample.TryGetProperty("allowsWrite", out var write))
                Assert.Equal(write.GetBoolean(), registration!.Descriptor.RequiresWrite);
        }
    }
}
