using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using YuzuhaToolkit.Manager;
using YuzuhaToolkit.Mcp;

namespace YuzuhaToolkit.Tests;

public sealed class ManagerFunctionContractTests
{
    [Fact]
    public void ManagementFunctionSchemasAndAuthorityMatchExistingCommunicationContracts()
    {
        using var bridge = new YuzuhaRpcBridge(NullLogger<YuzuhaRpcBridge>.Instance, new AvevaSessionDiscovery());
        var communication = CommunicationOperationCatalog.Create(bridge, new PmlFunctionTrustStore(), bridge.CreateIndependent);
        foreach (var entry in FunctionManagementOperations.Registrations())
        {
            var actual = entry.Descriptor;
            Assert.True(communication.Registry.TryGet(actual.Id, out var original));
            var expected = original!.Descriptor;
            Assert.Equal(expected.ContractVersion, actual.ContractVersion);
            Assert.True(JsonElement.DeepEquals(expected.InputSchema, actual.InputSchema));
            Assert.Equal(expected.RequiresPid, actual.RequiresPid);
            Assert.Equal(expected.RequiresWrite, actual.RequiresWrite);
            Assert.Equal(expected.RequiresExecution, actual.RequiresExecution);
            Assert.Equal(expected.LegacyTools, actual.LegacyTools);
        }
    }
}
