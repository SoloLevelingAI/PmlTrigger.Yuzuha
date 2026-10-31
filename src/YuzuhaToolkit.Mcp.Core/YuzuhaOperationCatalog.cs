using System.Text.Json;
using PmlTwin.Phayton;

namespace YuzuhaToolkit.Mcp;

/// <summary>CLI 与 MCP 共用的稳定操作标识。</summary>
public static class YuzuhaOperationIds
{
    /// <summary>定义会话发现、选择、状态、调用生成、执行及数组读取的操作标识。</summary>
    public const string Sessions = "aveva.sessions.list", Select = "aveva.session.select",
        Status = "aveva.session.status", Generate = "pml.call.generate",
        Execute = "pml.command.execute", ReadArray = "pml.array.read";
}

// Compiled into both frontends to retain existing public Yuzuha type/assembly identities.
// Only this adapter knows about Yuzuha; the Phayton core has no MCP or AVEVA dependencies.
/// <summary>为 CLI/MCP 建立共用操作契约，并隔离显式 PID 调用的连接状态。</summary>
public static class YuzuhaOperationCatalog
{
    /// <summary>建立使用真实会话桥的操作执行器；显式目标调用按请求创建独立连接。</summary>
    public static OperationExecutor Create(YuzuhaRpcBridge bridge, PmlFunctionTrustStore trust,
        Func<YuzuhaRpcBridge> createBridge)
    {
        var handler = new DelegateOperationHandler(async (id, input, context, token) =>
        {
            YuzuhaRpcBridge? ownedBridge = null;
            var dispatchStarted = false;
            try
            {
                var current = bridge;
                var bindsTarget = id is YuzuhaOperationIds.Select or YuzuhaOperationIds.Status
                    or YuzuhaOperationIds.Execute or YuzuhaOperationIds.ReadArray;
                // Each explicit-PID invocation owns its connection. Concurrent MCP invocations
                // cannot change the selected target between validation and command dispatch.
                if (bindsTarget && context.ProcessId != null && id != YuzuhaOperationIds.Select)
                    current = ownedBridge = createBridge();
                if (bindsTarget && context.ProcessId != null)
                    await current.SelectSessionAsync(context.ProcessId.Value, context.ExpectedModel, token, context.ExpectedProcessStartTimeUtcTicks)
                        .ConfigureAwait(false);
                var backend = new YuzuhaOperationBackend(current, trust);
                string json;
                switch (id)
                {
                    case YuzuhaOperationIds.Sessions: json = backend.ListAvevaSessions(); break;
                    case YuzuhaOperationIds.Select:
                    case YuzuhaOperationIds.Status:
                        json = await backend.GetConnectionStatus(token).ConfigureAwait(false); break;
                    case YuzuhaOperationIds.Generate:
                        var parameters = input.TryGetProperty("parameters", out var values)
                            ? JsonSerializer.Deserialize(values, YuzuhaToolJsonContext.Default.ListDynamicParameterInput)
                            : null;
                        var command = backend.GeneratePmlCall(input.GetProperty("methodName").GetString()!, parameters);
                        return OperationResult.Ok(id, JsonSerializer.SerializeToElement(command, OperationJsonContext.Default.String));
                    case YuzuhaOperationIds.Execute:
                        dispatchStarted = true;
                        json = await backend.RunPmlCommand(input.GetProperty("pmlCommand").GetString()!, token)
                            .ConfigureAwait(false); break;
                    case YuzuhaOperationIds.ReadArray:
                        dispatchStarted = true;
                        json = await backend.RunPmlCommandList(input.GetProperty("pmlCommand").GetString()!,
                            input.GetProperty("globalVar").GetString()!, input.GetProperty("deleteGlobalVar").GetBoolean(),
                            !input.TryGetProperty("includeEmpty", out var include) || include.GetBoolean(), token)
                            .ConfigureAwait(false); break;
                    default: throw new InvalidOperationException("Unregistered handler: " + id);
                }
                if (json.StartsWith("PML RPC failed:", StringComparison.Ordinal))
                {
                    var targetFailure = json.Contains("E3D_TARGET_", StringComparison.Ordinal);
                    return OperationResult.Fail(id, targetFailure ? 5 : 7,
                        targetFailure ? "TARGET_FAILED" : "RPC_FAILED", json,
                        mayHaveExecuted: dispatchStarted && !targetFailure);
                }
                using var document = JsonDocument.Parse(json);
                var data = document.RootElement.Clone();
                if (data.TryGetProperty("Error", out var error) && error.ValueKind == JsonValueKind.String)
                    return OperationResult.Fail(id, 5, "TARGET_FAILED", error.GetString()!, data);
                if (data.TryGetProperty("TargetVerified", out var verified) && !verified.GetBoolean())
                    return OperationResult.Fail(id, 5, "TARGET_NOT_VERIFIED", "Host identity was not verified.", data);
                if (data.TryGetProperty("Success", out var success) && !success.GetBoolean())
                    return OperationResult.Fail(id, 6, "HOST_FAILED",
                        data.GetProperty("ErrorMessage").GetString() ?? "Host rejected the command.", data,
                        mayHaveExecuted: dispatchStarted);
                return OperationResult.Ok(id, data);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) when (exception is ArgumentException or FormatException or NotSupportedException)
            {
                return OperationResult.Fail(id, 2, "INVALID_INPUT", exception.Message);
            }
            catch (Exception exception)
            {
                var targetFailure = exception.Message.Contains("E3D_TARGET_", StringComparison.Ordinal) ||
                    exception.Message.Contains("AVEVA_", StringComparison.Ordinal);
                return OperationResult.Fail(id, targetFailure ? 5 : 7,
                    targetFailure ? "TARGET_FAILED" : "RPC_FAILED", exception.Message,
                    mayHaveExecuted: dispatchStarted);
            }
            finally { ownedBridge?.Dispose(); }
        });
        return new OperationExecutor(new OperationRegistry(Descriptors().Select(d => new OperationRegistration(d, handler))));
    }

    private static IEnumerable<OperationDescriptor> Descriptors()
    {
        yield return Describe(YuzuhaOperationIds.Sessions, "Discover visible AVEVA sessions without connecting or running PML.", "{}");
        yield return Describe(YuzuhaOperationIds.Select, "Select and verify an explicit PID. Selection lives only in this process.", "{}", pid: true);
        yield return Describe(YuzuhaOperationIds.Status, "Recheck identity; with PID, open a fresh verified connection. Without PID, inspect this process's selection.", "{}");
        yield return Describe(YuzuhaOperationIds.Generate, "Generate PML call text only; never executes it.", """
            {"methodName":{"type":"string","minLength":1,"pattern":"^[A-Za-z_][A-Za-z0-9_]*(\\.[A-Za-z_][A-Za-z0-9_]*)*$"},
             "parameters":{"type":"array","items":{"type":"object","additionalProperties":false,
                "properties":{"type":{"type":"string","enum":["string","str","bool","boolean","double","real","number"]},
                              "value":{"anyOf":[{"type":"string"},{"type":"boolean"},{"type":"number"},{"type":"null"}]}},
                "required":["type","value"]}}}
            """, "[\"methodName\"]");
        yield return Describe(YuzuhaOperationIds.Execute, "Execute PML exactly once through the existing verified RPC/Host path. No automatic retry.",
            """{"pmlCommand":{"type":"string","minLength":1}}""", "[\"pmlCommand\"]", true, true);
        yield return Describe(YuzuhaOperationIds.ReadArray, "Execute an expression and read its global result array through the Host; may mutate state, including optional cleanup. Returns structured items and summary, without claiming walker completeness.", """
            {"pmlCommand":{"type":"string","minLength":1},
             "globalVar":{"type":"string","pattern":"^[A-Za-z_][A-Za-z0-9_]*$"},
             "deleteGlobalVar":{"type":"boolean"},"includeEmpty":{"type":"boolean","default":true}}
            """, "[\"pmlCommand\",\"globalVar\",\"deleteGlobalVar\"]", true, true);
    }

    private static OperationDescriptor Describe(string id, string description, string properties,
        string required = "[]", bool pid = false, bool execute = false)
    {
        using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":" +
            properties + ",\"required\":" + required + "}");
        return new(id, 1, description, schema.RootElement.Clone(), pid, execute);
    }
}
