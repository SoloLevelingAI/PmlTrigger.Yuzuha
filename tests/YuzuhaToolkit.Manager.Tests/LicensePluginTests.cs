using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Plugins.License;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class LicensePluginTests
{
    [Fact]
    public void NativeServiceProbeOnlyQueriesAnInventedMissingName()
    {
        var evidence = LicensePlugin.Service("Yuzuha_Fixture_Missing_" + Guid.NewGuid().ToString("N"));
        Assert.Equal(OperatingSystem.IsWindows() ? "missing" : "unsupported", evidence.State);
    }
    [Fact]
    public async Task ActualLoopbackReachabilityAndServiceEvidenceNeverClaimCheckout()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var executor = new OperationExecutor(new OperationRegistry(LicensePlugin.Registrations(service: name => new(name, "running", "synthetic service"))));
            var result = await executor.InvokeAsync("license.diagnose", JsonSerializer.SerializeToElement(new { host = "127.0.0.1", rmsPort = port, rulesPort = port, serviceName = "synthetic-service", timeoutMilliseconds = 1000 }), new());
            Assert.True(result.Success, result.Error?.Message); var data = result.Data!.Value;
            Assert.Equal("reachable", data.GetProperty("rms").GetProperty("state").GetString());
            Assert.Equal("reachable", data.GetProperty("rules").GetProperty("state").GetString());
            Assert.Equal("running", data.GetProperty("service").GetProperty("state").GetString());
            Assert.Equal("unverified", data.GetProperty("checkoutState").GetString());
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task UnreachableAndTimeoutRemainSeparateFromMissingServiceAndCheckout()
    {
        var executor = new OperationExecutor(new OperationRegistry(LicensePlugin.Registrations((host, port, _, _) => Task.FromResult(
            new LicensePortEvidence(host, port, port == 4545 ? "unreachable" : "timeout", "synthetic port evidence")),
            name => new(name, "missing", "synthetic missing service"))));
        var result = await executor.InvokeAsync("license.diagnose", JsonSerializer.SerializeToElement(new { host = "fixture", rmsPort = 4545, rulesPort = 5054, serviceName = "fixture" }), new());
        Assert.True(result.Success); var data = result.Data!.Value;
        Assert.Equal("unreachable", data.GetProperty("rms").GetProperty("state").GetString());
        Assert.Equal("timeout", data.GetProperty("rules").GetProperty("state").GetString());
        Assert.Equal("missing", data.GetProperty("service").GetProperty("state").GetString());
        Assert.Equal("unverified", data.GetProperty("checkoutState").GetString());
    }

    [Fact]
    public async Task BadInputAndCredentialFieldsAreRejectedBeforeAnyProbeAndCancellationDoesNotRetry()
    {
        var calls = 0;
        var executor = new OperationExecutor(new OperationRegistry(LicensePlugin.Registrations((host, port, _, token) => {
            calls++; token.ThrowIfCancellationRequested(); return Task.FromResult(new LicensePortEvidence(host, port, "reachable", "fixture")); })));
        var invalid = await executor.InvokeAsync("license.tcp.test", JsonSerializer.SerializeToElement(new { host = "fixture", port = 0, password = "SYNTHETIC_SECRET" }), new());
        Assert.Equal(2, invalid.ExitCode); Assert.Equal(0, calls); Assert.DoesNotContain("SYNTHETIC_SECRET", invalid.Error?.Message ?? "");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var result = await executor.InvokeAsync("license.tcp.test", JsonSerializer.SerializeToElement(new { host = "fixture", port = 4545 }), new(), cancelled.Token);
        Assert.Equal(8, result.ExitCode); Assert.Equal(0, calls);
        Assert.DoesNotContain(typeof(LicensePlugin).Assembly.GetReferencedAssemblies(), a => (a.Name ?? "").Contains("Mcp") || (a.Name ?? "").Contains("Vmware") || (a.Name ?? "").Contains("Manager.Core"));
    }
}
