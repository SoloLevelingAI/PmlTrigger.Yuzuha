using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace YuzuhaToolkit.Tests;

public sealed class DevelopmentApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public DevelopmentApiTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task OpenApiContainsRealRoutesSummariesAndSchemas()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("/", document!.RootElement.GetProperty("servers")[0].GetProperty("url").GetString());
        var paths = document!.RootElement.GetProperty("paths");
        Assert.Equal(7, paths.EnumerateObject().Count());
        var preview = paths.GetProperty("/api/pml/preview").GetProperty("post");
        Assert.Equal("PreviewPmlCall", preview.GetProperty("operationId").GetString());
        Assert.Contains("PML", preview.GetProperty("summary").GetString());
        Assert.True(preview.GetProperty("responses").TryGetProperty("400", out _));
        Assert.Contains("PmlPreviewRequest", document.RootElement.GetProperty("components").GetProperty("schemas").GetRawText());
    }

    [Fact]
    public async Task ScalarUsesTheLocalOpenApiDocument()
    {
        var html = await _client.GetStringAsync("/scalar/v1");
        Assert.Contains("openapi/v1.json", html);
        Assert.Contains("scalar", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/scalar/scalar.js")).StatusCode);
    }

    [Fact]
    public async Task GuidesContainCompleteBuiltinContent()
    {
        using var document = await _client.GetFromJsonAsync<JsonDocument>("/api/guides?query=read-current-element");
        var guide = Assert.Single(document!.RootElement.EnumerateArray());
        Assert.Equal("read-current-element", guide.GetProperty("id").GetString());
        Assert.Contains("YuzuhaReadCurrentElement", guide.GetProperty("content").GetString());
    }

    [Fact]
    public async Task PreviewFormatsPmlWithoutExecutingIt()
    {
        var response = await _client.PostAsJsonAsync("/api/pml/preview", new
        {
            methodName = "Test", parameters = new object[] { new { type = "bool", value = (object)true }, new { type = "string", value = (object)"O'Brien" } }
        });
        response.EnsureSuccessStatusCode();
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("!!Test(TRUE,'O''Brien')", document!.RootElement.GetProperty("command").GetString());
    }

    [Theory]
    [InlineData("!!Test")]
    [InlineData("Test()")]
    [InlineData("Test\nNEW BOX")]
    public async Task PreviewRejectsCommandSyntaxAsMethodName(string methodName)
    {
        var response = await _client.PostAsJsonAsync("/api/pml/preview", new { methodName, parameters = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("{\"methodName\":\"Test\",\"parameters\":null}")]
    [InlineData("{\"methodName\":\"Test\",\"parameters\":[null]}")]
    [InlineData("{\"methodName\":\"Test\",\"parameters\":[{\"type\":\"string\",\"value\":{}}]}")]
    [InlineData("{\"methodName\":\"Test\",\"parameters\":[{\"type\":\"object\",\"value\":\"x\"}]}")]
    public async Task PreviewRejectsInvalidParameters(string json)
    {
        var response = await _client.PostAsync("/api/pml/preview", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmptyKnowledgeQueryIsBadRequest() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/knowledge/query-plan?query=%20")).StatusCode);

    [Fact]
    public async Task KnowledgePlanUsesChineseDomainTerms()
    {
        var json = await _client.GetStringAsync("/api/knowledge/query-plan?query=" + Uri.EscapeDataString("创建方盒"));
        using var document = JsonDocument.Parse(json);
        Assert.Contains(document.RootElement.EnumerateArray(), v => v.GetProperty("ftsExpression").GetString() == "\"NEW\" AND \"BOX\"");
    }

    [Fact]
    public async Task DevelopmentHostDoesNotExposePmlExecution() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync("/api/pml/execute", new { command = "NEW BOX" })).StatusCode);

    [Fact]
    public async Task ManagerContractHttpOnlyDescribesActualOperationsAndKeepsUnknownOutputBoundary()
    {
        using var contract = await _client.GetFromJsonAsync<JsonDocument>("/api/manager/contracts");
        var root = contract!.RootElement;
        Assert.Equal("pmltwin.manager-journey/1", root.GetProperty("journey").GetProperty("schema").GetString());
        Assert.Equal(8, root.GetProperty("journey").GetProperty("stages").GetArrayLength());
        Assert.Contains("unknown JSON", root.GetProperty("outputBoundary").GetString());
        using var descriptor = await _client.GetFromJsonAsync<JsonDocument>("/api/manager/operations/evars.apply");
        Assert.True(descriptor!.RootElement.GetProperty("requiresWrite").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/manager/operations/not.registered")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await _client.PostAsJsonAsync("/api/manager/contracts", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync("/api/manager/invoke", new { operation = "evars.apply" })).StatusCode);
        using var openapi = await _client.GetFromJsonAsync<JsonDocument>("/openapi/v1.json");
        foreach (var route in openapi!.RootElement.GetProperty("paths").EnumerateObject().Where(p => p.Name.StartsWith("/api/manager/")))
            Assert.Equal("get", Assert.Single(route.Value.EnumerateObject()).Name);
    }

    [Fact]
    public async Task InstalledDocsOnlyModeDoesNotRegisterManagerCatalogOrOpenApiRoutes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "manager-docs-only-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); await File.WriteAllTextAsync(Path.Combine(directory, "index.html"), "<!doctype html><h1>Offline docs fixture</h1>");
        try
        {
            using var only = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("docs-only", "true").UseSetting("docs-root", directory));
            using var initialized = only.CreateClient();
            foreach (var route in new[] { "/api/manager/contracts", "/api/manager/operations", "/openapi/v1.json" })
            {
                var response = await only.Server.SendAsync(context => { context.Connection.RemoteIpAddress = IPAddress.Loopback; context.Request.Host = new HostString("127.0.0.1"); context.Request.Method = "GET"; context.Request.Path = route; });
                Assert.Equal(404, response.Response.StatusCode);
            }
        }
        finally { Directory.Delete(directory, true); }
    }
}
