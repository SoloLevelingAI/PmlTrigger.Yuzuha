using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Mcp;

/// <summary>开发流程的后端会话适配；同一流程保留固定目标身份并复用数组解析。</summary>
public sealed class PmlDevelopmentHost(DevelopmentTarget expected) : IPmlDevelopmentHost, IDisposable
{
    private readonly YuzuhaRpcBridge bridge = new(NullLogger<YuzuhaRpcBridge>.Instance,new AvevaSessionDiscovery());
    private bool selected;
    /// <inheritdoc />
    public async Task<DevelopmentTarget> VerifyAsync(CancellationToken token)
    {
        var status = selected ? await bridge.GetConnectionStatusAsync(token) : await bridge.SelectSessionAsync(expected.ProcessId,expected.Model,token);
        selected = status.TargetVerified;
        if (!selected || status.Host is null) throw new InvalidOperationException("会话核验失败。");
        return new(status.Host.ProcessId,status.Host.ProcessStartTimeUtcTicks,status.Host.Model!);
    }
    /// <inheritdoc />
    public async Task<DevelopmentReply> ExecuteAsync(string command,CancellationToken token) => Parse(
        await new YuzuhaOperationBackend(bridge,new PmlFunctionTrustStore()).RunPmlCommand(command,token));
    /// <inheritdoc />
    public async Task<DevelopmentRows> ReadAsync(string global,CancellationToken token)
    {
        var reply = Parse(await new YuzuhaOperationBackend(bridge,new PmlFunctionTrustStore()).RunPmlCommandList("!!"+global,global,true,true,token));
        if (!reply.Success) return new(reply,0,[]);
        return new(reply,reply.Raw.GetProperty("UnparsedCount").GetInt32(),reply.Raw.GetProperty("Items").EnumerateArray()
            .Select(i=>new DevelopmentValue(i.GetProperty("Path").GetString()!,i.GetProperty("Value").ValueKind==JsonValueKind.Null?null:i.GetProperty("Value").GetString())).ToArray());
    }
    private static DevelopmentReply Parse(string json)
    {
        if(json.StartsWith("PML RPC failed:",StringComparison.Ordinal)) return new(false,JsonSerializer.SerializeToElement(new JsonObject { ["error"] = json }, YuzuhaToolJsonContext.Default.JsonObject),json,true);
        using var document=JsonDocument.Parse(json); var raw=document.RootElement.Clone();
        return new(raw.GetProperty("Success").GetBoolean(),raw,raw.GetProperty("ErrorMessage").GetString());
    }
    /// <inheritdoc />
    public void Dispose()=>bridge.Dispose();
}
