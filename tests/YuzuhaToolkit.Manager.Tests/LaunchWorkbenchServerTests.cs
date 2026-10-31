using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PmlTwin.Phayton;
using YuzuhaToolkit.Manager;
using Xunit;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class LaunchWorkbenchServerTests
{
    [Fact]
    public async Task BrowserAdapterRequiresTokenAndOriginAndUsesSharedExecutor()
    {
        var root = Path.Combine(Path.GetTempPath(), "PmlTwinHttp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "index.html"), "<html><head></head><body>Test</body></html>");
        using var schema = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}");
        var calls = 0;
        var executor = new OperationExecutor(new OperationRegistry([new(new("launch.execute", 1, "fixture", schema.RootElement.Clone(), RequiresLaunch: true),
            new DelegateOperationHandler((id, _, _, _) => { calls++; return Task.FromResult(OperationResult.Ok(id, schema.RootElement.Clone())); }))]));
        using var server = new LaunchWorkbenchServer(executor, root);
        using var stop = new CancellationTokenSource();
        server.Start();
        var running = server.RunAsync(stop.Token);
        try
        {
            using var client = new HttpClient { BaseAddress = server.Address };
            var html = await client.GetStringAsync("");
            var token = Regex.Match(html, "name=\"pmltwin-token\" content=\"([A-F0-9]+)\"").Groups[1].Value;
            Assert.Equal(64, token.Length);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("api/operations")).StatusCode);
            client.DefaultRequestHeaders.Add("X-PmlTwin-Token", token);
            client.DefaultRequestHeaders.Add("Origin", "https://untrusted.invalid");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("api/operations")).StatusCode);
            client.DefaultRequestHeaders.Remove("Origin");
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/operations")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("secret.json")).StatusCode);
            var denied = await client.PostAsync("api/operations/launch.execute", new StringContent("{\"input\":{}}", Encoding.UTF8, "application/json"));
            Assert.Contains("LAUNCH_NOT_AUTHORIZED", await denied.Content.ReadAsStringAsync());
            Assert.Equal(0, calls);
            var accepted = await client.PostAsync("api/operations/launch.execute", new StringContent("{\"input\":{},\"allowLaunch\":true}", Encoding.UTF8, "application/json"));
            Assert.Contains("\"success\": true", await accepted.Content.ReadAsStringAsync());
            Assert.Equal(1, calls);
            var escape = await client.PostAsync("api/operations/plugin.invoke", new StringContent("{\"input\":{}}", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.NotFound, escape.StatusCode);
            Assert.Equal(1, calls);
            foreach (var payload in new[]
            {
                "{\"input\":{},\"allowLaunch\":false,\"allowLaunch\":true}",
                "{\"input\":{},\"input\":{},\"allowLaunch\":true}",
                "{\"input\":{\"password\":\"SYNTHETIC_SECRET\",\"password\":\"other\"},\"allowLaunch\":true}",
                "{\"input\":{\"password\":\"SYNTHETIC_SECRET\""
            })
            {
                var rejected = await client.PostAsync("api/operations/launch.execute", new StringContent(payload, Encoding.UTF8, "application/json"));
                Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
                Assert.DoesNotContain("SYNTHETIC_SECRET", await rejected.Content.ReadAsStringAsync());
            }
            var unknownSecret = await client.PostAsync("api/operations/launch.execute", new StringContent("{\"input\":{\"password\":\"SYNTHETIC_SECRET\"}}", Encoding.UTF8, "application/json"));
            Assert.DoesNotContain("SYNTHETIC_SECRET", await unknownSecret.Content.ReadAsStringAsync());
            Assert.Equal(1, calls);
        }
        finally
        {
            stop.Cancel(); await running;
            Directory.Delete(root, true);
        }
    }
}
