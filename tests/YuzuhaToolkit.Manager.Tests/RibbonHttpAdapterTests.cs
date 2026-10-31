using System.Net;
using System.Text;
using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Manager;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class RibbonHttpAdapterTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
    private static JsonElement Input => JsonSerializer.Deserialize<JsonElement>("{\"documentId\":\"existing\"}");

    [Theory]
    [InlineData(403)] [InlineData(409)] [InlineData(423)]
    public async Task KeepsOriginalErrorsAndMakesOneRequest(int status)
    {
        var calls = 0;
        using var client = new HttpClient(new Handler(async (request, token) => {
            calls++;
            Assert.Equal("/api/v1/workspace/open", request.RequestUri!.AbsolutePath);
            Assert.Equal(Input.GetRawText(), await request.Content!.ReadAsStringAsync(token));
            return new((HttpStatusCode)status) { Content = new StringContent("{\"error\":\"original\"}", Encoding.UTF8, "application/json") };
        }));
        var result = await new RibbonHttpAdapter(new("http://127.0.0.1:4318/"), client).InvokeAsync("ribbon.workspace.open", "workspace/open", Input, default);
        Assert.False(result.Success);
        Assert.Equal("RIBBON_HTTP_" + status, result.Error!.Code);
        Assert.Equal(status, result.Data!.Value.GetProperty("httpStatus").GetInt32());
        Assert.Equal("original", result.Data.Value.GetProperty("response").GetProperty("error").GetString());
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task WriteTimeoutIsUnknownAndDoesNotReplay()
    {
        var calls = 0;
        using var client = new HttpClient(new Handler((_, _) => { calls++; throw new TaskCanceledException(); }));
        var adapter = new RibbonHttpAdapter(new("http://127.0.0.1:4318/"), client);
        var result = await adapter.InvokeAsync("ribbon.workspace.save", "workspace/save", Input, default);
        Assert.Equal("RIBBON_RESULT_UNKNOWN", result.Error!.Code);
        Assert.True(result.Error.MayHaveExecuted);
        Assert.Equal(1, calls);
    }

    private sealed class StalledStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return 0; }
    }

    [Fact]
    public async Task DeadlineCoversBodyAfterHeadersArrive()
    {
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StreamContent(new StalledStream()) })));
        var adapter = new RibbonHttpAdapter(new("http://127.0.0.1:4318/"), client, TimeSpan.FromMilliseconds(100));
        var result = await adapter.InvokeAsync("ribbon.workspace.save", "workspace/save", Input, default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("RIBBON_RESULT_UNKNOWN", result.Error!.Code);
        Assert.True(result.Error.MayHaveExecuted);
    }

    [Fact]
    public async Task MalformedWriteReceiptRemainsUnknown()
    {
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("not-json", Encoding.UTF8, "application/json") })));
        var result = await new RibbonHttpAdapter(new("http://127.0.0.1:4318/"), client).InvokeAsync("ribbon.workspace.save", "workspace/save", Input, default);
        Assert.Equal("RIBBON_RESULT_UNKNOWN", result.Error!.Code);
        Assert.True(result.Error.MayHaveExecuted);
    }

    [Fact]
    public async Task MalformedErrorBodyKeepsKnownHttpStatusAndBytes()
    {
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
        { Content = new StringContent("original-error", Encoding.UTF8, "application/json") })));
        var result = await new RibbonHttpAdapter(new("http://127.0.0.1:4318/"), client).InvokeAsync("ribbon.workspace.save", "workspace/save", Input, default);
        Assert.Equal("RIBBON_HTTP_403", result.Error!.Code);
        Assert.Equal(403, result.Data!.Value.GetProperty("httpStatus").GetInt32());
        Assert.Equal("original-error", Encoding.UTF8.GetString(result.Data.Value.GetProperty("bytesBase64").GetBytesFromBase64()));
    }

    [Fact]
    public async Task ExportPreservesExactBytes()
    {
        byte[] bytes = [0xff, 0xfe, 60, 0, 62, 0];
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) })));
        var result = await new RibbonHttpAdapter(new("http://127.0.0.1:4318/"), client).InvokeAsync("ribbon.uic.export", "uic/export", Input, default);
        Assert.True(result.Success);
        Assert.Equal(bytes, result.Data!.Value.GetProperty("bytesBase64").GetBytesFromBase64());
    }

    [Fact]
    public async Task MissingWriteGrantNeverCreatesClient()
    {
        var executor = new OperationExecutor(new OperationRegistry(RibbonHttpAdapter.Registrations(() => throw new Exception("must not construct"))));
        var result = await executor.InvokeAsync("ribbon.workspace.restore", Input, new());
        Assert.Equal("WRITE_NOT_AUTHORIZED", result.Error!.Code);
    }

    [Theory]
    [InlineData("http://example.com/")] [InlineData("http://127.0.0.1/api/")] [InlineData("http://user@127.0.0.1/")]
    public void RejectsUnscopedEndpoints(string endpoint)
    {
        using var client = new HttpClient();
        Assert.Throws<ArgumentException>(() => new RibbonHttpAdapter(new(endpoint), client));
    }
}
