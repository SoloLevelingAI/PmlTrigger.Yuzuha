using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Manager;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class ManagerContractCatalogTests
{
    [Fact]
    public async Task CompleteJourneyReferencesOnlyActualReadOnlyExportAndPreservesExplicitGrants()
    {
        var executor = ManagerOperationCatalog.Create();
        var result = await executor.InvokeAsync("manager.contracts", JsonSerializer.SerializeToElement(new { }), new());
        Assert.True(result.Success, result.Error?.Message);
        var export = ManagerContractCatalog.Export(executor.Registry);
        Assert.Equal(new[] { "install", "environment", "ownership", "preview", "apply", "recover", "launch", "verify" }, export.Journey.Stages.Select(s => s.Id));
        foreach (var stage in export.Journey.Stages)
        {
            foreach (var text in new[] { stage.Input, stage.Precondition, stage.Method, stage.Effect, stage.Validation, stage.Receipt, stage.Recovery, stage.EvidenceBoundary }) Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.All(stage.OperationIds, id => Assert.True(executor.Registry.TryGet(id, out _), id));
            Assert.All(stage.Scripts, script => { Assert.StartsWith("src/", script.Path); Assert.StartsWith("api/manager/", script.DocFxPath); Assert.NotEmpty(script.Explanation); });
        }
        Assert.True(executor.Registry.TryGet("evars.apply", out var apply)); Assert.True(apply!.Descriptor.RequiresWrite);
        Assert.True(executor.Registry.TryGet("launch.execute", out var launch)); Assert.True(launch!.Descriptor.RequiresLaunch);
        Assert.True(executor.Registry.TryGet("manager.contracts", out var read)); Assert.False(read!.Descriptor.RequiresWrite || read.Descriptor.RequiresLaunch || read.Descriptor.RequiresExecution || read.Descriptor.RequiresPid);
        var emptyRegistry = new OperationRegistry(Array.Empty<OperationRegistration>());
        Assert.Throws<InvalidOperationException>(() => ManagerContractCatalog.Export(emptyRegistry));
        Assert.Contains("unknown JSON", export.OutputBoundary);
        var copy = ManagerContractCatalog.Describe(); copy.Stages[0].OperationIds[0] = "forged.operation";
        Assert.Equal("setup.handoff.inspect", ManagerContractCatalog.Describe().Stages[0].OperationIds[0]);
    }

    [Fact]
    public async Task ContractCliUsesTheExistingProtocolWithoutLaunchOrWriteAuthorization()
    {
        using var output = new StringWriter();
        var code = await ManagerCliHost.RunAsync(["op", "invoke", "manager.contracts", "--input-stdin"], ManagerOperationCatalog.Create(), output, inputReader: new StringReader("{}"));
        Assert.Equal(0, code);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal("manager.contracts", json.RootElement.GetProperty("operationId").GetString());
        Assert.Equal(8, json.RootElement.GetProperty("data").GetProperty("journey").GetProperty("stages").GetArrayLength());
    }
}
