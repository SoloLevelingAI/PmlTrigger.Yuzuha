using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
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
        Assert.Equal(4, paths.EnumerateObject().Count());
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
}
