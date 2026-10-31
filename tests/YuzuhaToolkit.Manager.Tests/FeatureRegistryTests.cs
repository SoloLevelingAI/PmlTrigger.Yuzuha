using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Manager;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class FeatureRegistryTests
{
    private static FeatureRegistration Feature(string id, bool enabled = true, int version = 1, Action? compose = null) =>
        new(new(id, "1.0", version, "test", [], [id + ".read"], "compiled", enabled, "implemented"), () => {
            compose?.Invoke();
            return [new(new(id + ".read", 1, "test", JsonSerializer.Deserialize<JsonElement>("{}")),
                new DelegateOperationHandler((operation, _, _, _) => Task.FromResult(OperationResult.Ok(operation, JsonSerializer.Deserialize<JsonElement>("{}")))))];
        });

    [Fact]
    public void MetadataReadAndDisabledFeatureNeverInvokeFactory()
    {
        var calls = 0;
        var registry = new FeatureRegistry([Feature("optional", false, compose: () => calls++)]);
        Assert.False(Assert.Single(registry.Describe()).Enabled);
        Assert.Empty(registry.Compose());
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ActualManagerCompositionDisablesFunctionsAndWorkersWithoutChangingBaseOperations()
    {
        var calls = 0;
        var executor = ManagerOperationCatalog.Create(communication: () => { calls++; throw new IOException(); },
            featuresOptions: new(["functions", "plugin"], ProductProfile: "E3D2.1"));
        Assert.False(executor.Registry.TryGet("pml.function.invoke", out _));
        Assert.False(executor.Registry.TryGet("plugin.invoke", out _));
        var list = await executor.InvokeAsync("feature.list", JsonSerializer.Deserialize<JsonElement>("{}"), new());
        var descriptors = JsonSerializer.Deserialize(list.Data!.Value, FeatureJsonContext.Default.FeatureDescriptorArray)!;
        Assert.False(descriptors.Single(d => d.Id == "functions").Enabled);
        Assert.False(descriptors.Single(d => d.Id == "plugin").Enabled);
        Assert.Contains("E3D2.1", descriptors.Single(d => d.Id == "functions").SupportedProductProfiles!);
        Assert.Contains("Host-unverified", descriptors.Single(d => d.Id == "functions").CompatibilityStatus);
        Assert.True((await executor.InvokeAsync("manager.status", JsonSerializer.Deserialize<JsonElement>("{}"), new())).Success);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void ActualManagerRejectsUnsupportedRuntimeProductAndUnknownDisableSelection()
    {
        Assert.Throws<NotSupportedException>(() => ManagerOperationCatalog.Create(featuresOptions: new(Runtime: "CAF-net48")));
        Assert.Throws<NotSupportedException>(() => ManagerOperationCatalog.Create(featuresOptions: new(ProductProfile: "E3D9")));
        Assert.Throws<ArgumentException>(() => ManagerOperationCatalog.Create(featuresOptions: new(["functons"])));
        var feature = Feature("future");
        Assert.Throws<NotSupportedException>(() => new FeatureRegistry([feature with { Descriptor = feature.Descriptor with { Version = "2.0" } }]));
    }

    [Fact]
    public void DuplicateAndUnsupportedContractsFailBeforeAssembly()
    {
        var calls = 0;
        Assert.Throws<ArgumentException>(() => new FeatureRegistry([Feature("one", compose: () => calls++), Feature("one")]));
        Assert.Throws<NotSupportedException>(() => new FeatureRegistry([Feature("future", version: 2, compose: () => calls++)]));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void CyclesDisabledDependenciesAndMismatchedFactoriesAreRejected()
    {
        var first = Feature("first"); var second = Feature("second");
        Assert.Throws<ArgumentException>(() => new FeatureRegistry([
            first with { Descriptor = first.Descriptor with { Requires = ["second"] } },
            second with { Descriptor = second.Descriptor with { Requires = ["first"] } }]));
        var disabled = new FeatureRegistry([first with { Descriptor = first.Descriptor with { Requires = ["second"] } },
            second with { Descriptor = second.Descriptor with { Enabled = false } }]);
        Assert.Throws<InvalidOperationException>(() => disabled.Compose());
        Assert.Throws<InvalidOperationException>(() => new FeatureRegistry([
            first with { Descriptor = first.Descriptor with { Operations = ["wrong"] } }]).Compose());
    }

    [Fact]
    public async Task BaseCatalogHasUniqueFeatureOwnershipAndFunctionIdsWithoutCreatingCommunication()
    {
        var calls = 0;
        var executor = ManagerOperationCatalog.Create(communication: () => { calls++; throw new IOException("not installed"); });
        var result = await executor.InvokeAsync("feature.list", JsonSerializer.Deserialize<JsonElement>("{}"), new());
        Assert.True(result.Success);
        var descriptors = JsonSerializer.Deserialize(result.Data!.Value, FeatureJsonContext.Default.FeatureDescriptorArray)!;
        var ids = descriptors.SelectMany(d => d.Operations).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Contains("pml.function.register", ids);
        Assert.Contains("pml.functions.list", ids);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task FunctionAuthorizationAndPinnedIdentityFailBeforeCommunication()
    {
        var calls = 0;
        var executor = new OperationExecutor(new OperationRegistry(FunctionManagementOperations.Registrations(() => { calls++; throw new IOException(); })));
        var register = await executor.InvokeAsync("pml.function.register", JsonSerializer.Deserialize<JsonElement>("""{"sourceFile":"x","functionName":"f"}"""), new());
        Assert.Equal("WRITE_NOT_AUTHORIZED", register.Error?.Code);
        var invoke = await executor.InvokeAsync("pml.function.invoke", JsonSerializer.Deserialize<JsonElement>("""{"functionName":"f","parameters":[]}"""), new(42, "DESIGN", AllowExecution: true));
        Assert.Equal("PROCESS_IDENTITY_REQUIRED", invoke.Error?.Code);
        Assert.Equal(0, calls);
    }
}
