using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Manager;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class FunctionManagementProcessTests
{
    [Theory]
    [InlineData("pml.function.register", "{\"sourceFile\":\"C:/synthetic/中文.pmlfnc\",\"functionName\":\"Sample\"}")]
    [InlineData("pml.functions.list", "{}")]
    [InlineData("pml.function.permission", "{\"functionName\":\"Sample\",\"allowInvoke\":false}")]
    [InlineData("pml.function.invoke", "{\"functionName\":\"Sample\",\"parameters\":[{\"type\":\"string\",\"value\":\"O'Brien 中文\"}]}")]
    public async Task OriginalFunctionIdsInputAndContextCrossExistingProcessBoundary(string id, string json)
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "ProcessFixture", "YuzuhaToolkit.Manager.ProcessFixture.dll");
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet.exe");
        var executor = new OperationExecutor(new OperationRegistry(FunctionManagementOperations.Registrations(() => new(new(fixture, host)))));
        var input = JsonSerializer.Deserialize<JsonElement>(json);
        var context = new OperationContext(42, "DESIGN", AllowExecution: true, AllowWrite: true, ExpectedProcessStartTimeUtcTicks: 1234);
        var result = await executor.InvokeAsync(id, input, context);
        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(id, result.OperationId);
        var data = result.Data!.Value;
        Assert.True(JsonElement.DeepEquals(input, data.GetProperty("Input")));
        Assert.Equal(1234, data.GetProperty("Context").GetProperty("ExpectedProcessStartTimeUtcTicks").GetInt64());
        Assert.Equal("DESIGN", data.GetProperty("Context").GetProperty("ExpectedModel").GetString());
    }
}
