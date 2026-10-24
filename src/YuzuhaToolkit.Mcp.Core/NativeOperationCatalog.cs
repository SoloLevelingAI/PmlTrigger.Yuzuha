using System.Text.Json;
using PmlTwin.Phayton;
using YuzuhaToolkit.Knowledge;
using YuzuhaToolkit.Pml;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Mcp;

/// <summary>直接调用本机服务的完整 CLI 操作目录；不创建 MCP 服务器或 JSON-RPC 子进程。</summary>
public static partial class NativeOperationCatalog
{
    /// <summary>组合既有 RPC、知识、工作区操作；可注入隔离存储与合成 Host 进行相同路径测试。</summary>
    public static OperationExecutor Create(YuzuhaRpcBridge bridge, PmlFunctionTrustStore trust,
        Func<YuzuhaRpcBridge> createBridge, PmlFunctionRegistry? functions = null,
        KnowledgeRepository? knowledge = null, RuntimeJournal? journal = null,
        IEnumerable<OperationRegistration>? additional = null)
    {
        var original = YuzuhaOperationCatalog.Create(bridge, trust, createBridge);
        var list = new List<OperationRegistration>();
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [YuzuhaOperationIds.Sessions] = "list_aveva_sessions",
            [YuzuhaOperationIds.Select] = "select_aveva_session",
            [YuzuhaOperationIds.Status] = "get_connection_status",
            [YuzuhaOperationIds.Generate] = "generate_pml_call",
            [YuzuhaOperationIds.Execute] = "run_pml_command",
            [YuzuhaOperationIds.ReadArray] = "run_pml_command_list"
        };
        foreach (var descriptor in original.Registry.Descriptors)
        {
            var native = descriptor with
            {
                LegacyTools = [aliases[descriptor.Id]],
                RequiresPid = descriptor.RequiresPid || descriptor.Id == YuzuhaOperationIds.Status,
                Description = descriptor.Id is YuzuhaOperationIds.Select or YuzuhaOperationIds.Status
                    ? "Verify an explicit PID for this invocation only. Every subsequent CLI call must supply --pid again."
                    : descriptor.Description
            };
            list.Add(new(native, new DelegateOperationHandler(original.InvokeAsync)));
        }
        var handler = new DelegateOperationHandler(async (id, input, context, token) =>
        {
            YuzuhaRpcBridge? owned = null;
            var dispatched = false;
            try
            {
                var registry = functions ?? new PmlFunctionRegistry();
                var preparedFunction = id == "pml.function.invoke"
                    ? registry.PrepareCall(S(input, "functionName")!, JsonSerializer.Deserialize(input.GetProperty("parameters"), YuzuhaToolJsonContext.Default.ListDynamicParameterInput)!)
                    : null;
                var requiresTarget = id is "pml.array.window" or "pml.forms.list" or "pml.gadgets.list"
                    or "pml.gadget.read" or "pml.function.invoke" or "pml.undo.begin" or "pml.undo.end";
                if (requiresTarget)
                {
                    owned = createBridge();
                    await owned.SelectSessionAsync(context.ProcessId!.Value, context.ExpectedModel, token).ConfigureAwait(false);
                }
                string json;
                switch (id)
                {
                    case "pml.array.window":
                        dispatched = true;
                        json = JsonSerializer.Serialize(await owned!.ReadPmlArrayAsync(S(input, "globalVar")!, I(input, "startIndex", 1), I(input, "maxItems", 100), I(input, "maxCharacters", 32768), token).ConfigureAwait(false), YuzuhaJsonContext.Default.RunPmlCommandResponse);
                        break;
                    case "pml.forms.list":
                    case "pml.gadgets.list":
                    case "pml.gadget.read":
                        var request = new PmlFormQueryRequest
                        {
                            Kind = id == "pml.forms.list" ? B(input, "shownOnly", true) ? "shown-forms" : "forms" : id == "pml.gadgets.list" ? "gadgets" : "gadget",
                            FormName = S(input, "formName"), GadgetName = S(input, "gadgetName"),
                            StartIndex = I(input, "startIndex", 1), MaxItems = id == "pml.gadget.read" ? 1 : I(input, "maxItems", 100), MaxCharacters = I(input, "maxCharacters", 32768)
                        };
                        dispatched = true;
                        json = JsonSerializer.Serialize(await owned!.QueryPmlFormsAsync(request, token).ConfigureAwait(false), YuzuhaJsonContext.Default.RunPmlCommandResponse);
                        break;
                    case "pml.function.register":
                        json = JsonSerializer.Serialize(registry.Register(S(input, "sourceFile")!, S(input, "functionName")!), PmlFunctionRegistryJsonContext.Default.PmlFunctionRegistration); break;
                    case "pml.functions.list":
                        json = JsonSerializer.Serialize(registry.List(), PmlFunctionRegistryJsonContext.Default.ListPmlFunctionRegistration); break;
                    case "pml.function.permission":
                        json = JsonSerializer.Serialize(registry.SetPermission(S(input, "functionName")!, B(input, "allowInvoke")), PmlFunctionRegistryJsonContext.Default.PmlFunctionRegistration); break;
                    case "pml.function.invoke":
                        dispatched = true;
                        json = await new YuzuhaOperationBackend(owned!, trust).RunPmlCommand(preparedFunction!, token).ConfigureAwait(false); break;
                    case "pml.undo.begin":
                    case "pml.undo.end":
                        var undoCommand = id == "pml.undo.begin" ? WorkspaceTools.BuildMarkDb(S(input, "description") ?? "Phayton plugin development") : "ENDMARKDB";
                        dispatched = true;
                        json = await new YuzuhaOperationBackend(owned!, trust).RunPmlCommand(undoCommand, token).ConfigureAwait(false); break;
                    case "pml.trust.list": json = new PmlFunctionTrustTools(trust).ListPmlFunctionTrust(); break;
                    case "pml.trust.set": json = new PmlFunctionTrustTools(trust).SetPmlFunctionTrust(S(input, "functionName")!, S(input, "state")!, S(input, "reason"), S(input, "failingCommand")); break;
                    case "builtin.usage": json = new BuiltinUsageTools().GetBuiltinUsage(S(input, "query")); break;
                    case "workspace.pmllib.inspect":
                        var settings = WorkspaceSettings.Load();
                        var directory = S(input, "directory") ?? settings.RegisteredPmlLib;
                        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Specify directory or configure registeredPmlLib.");
                        json = JsonSerializer.Serialize(DirectoryInspector.Scan(directory, settings.WarnFiles, settings.WarnBytes), WorkspaceJsonContext.Default.DirectoryHealth); break;
                    case "runtime.status": json = JsonSerializer.Serialize(RuntimeReader.Statuses(), WorkspaceJsonContext.Default.RuntimeStatusArray); break;
                    case "runtime.calls": json = JsonSerializer.Serialize(RuntimeReader.Recent(I(input, "limit", 50)), WorkspaceJsonContext.Default.ActivityEntryArray); break;
                    default:
                        json = InvokeKnowledge(id, input, knowledge ?? new KnowledgeRepository()); break;
                }
                return ParseResult(id, json, dispatched);
            }
            catch (OperationCanceledException) { throw; }
            catch (HostCapabilityException error) { return OperationResult.Fail(id, 6, error.Code, error.Message, mayHaveExecuted: false); }
            catch (UnauthorizedAccessException error) { return OperationResult.Fail(id, 4, "ACCESS_DENIED", error.Message); }
            catch (Exception error) when (error is ArgumentException or FormatException or InvalidDataException or KeyNotFoundException or NotSupportedException or KnowledgeException)
            {
                var target = error.Message.Contains("E3D_TARGET_", StringComparison.Ordinal) || error.Message.Contains("AVEVA_", StringComparison.Ordinal);
                return OperationResult.Fail(id, target ? 5 : 2, target ? "TARGET_FAILED" : "INVALID_INPUT", error.Message);
            }
            catch (Exception error)
            {
                var target = error.Message.Contains("E3D_TARGET_", StringComparison.Ordinal) || error.Message.Contains("AVEVA_", StringComparison.Ordinal);
                return OperationResult.Fail(id, target ? 5 : 7, target ? "TARGET_FAILED" : "OPERATION_FAILED", error.Message, mayHaveExecuted: dispatched && !target);
            }
            finally { owned?.Dispose(); }
        });
        list.AddRange(Descriptors().Select(d => new OperationRegistration(d, handler)));
        if (additional is not null) list.AddRange(additional);
        return new(new OperationRegistry(list), journal is null ? null : new JournalObserver(journal));
    }

    private static string InvokeKnowledge(string id, JsonElement input, KnowledgeRepository repository)
    {
        var tools = new KnowledgeTools(repository);
        var layers = new KnowledgeLayerTools(repository);
        return id switch
        {
            "knowledge.databases.list" => tools.ListKnowledgeDatabases(B(input, "checkFreshness", true)),
            "knowledge.database.build" => tools.BuildKnowledgeDatabase(S(input, "pmlLibRoot"), S(input, "pmlUiRoot"), S(input, "webHelpRoot"), S(input, "dbName") ?? "pml-knowledge", S(input, "dbDir"), B(input, "rebuild"), L(input, "maxFilesPerRoot", 0)),
            "knowledge.database.check" => tools.CheckKnowledgeDatabase(S(input, "dbPath")!),
            "knowledge.search" => tools.SearchKnowledge(S(input, "query")!, S(input, "dbName"), S(input, "dbPath"), I(input, "topK", 8), S(input, "sourceType"), S(input, "module"), S(input, "chunkType")),
            "knowledge.chunk.read" => tools.GetKnowledgeChunk(L(input, "chunkId", 0), S(input, "dbName"), S(input, "dbPath")),
            "knowledge.source.register" => layers.RegisterKnowledgeSource(S(input, "role")!, S(input, "name")!, S(input, "pmlLibRoot"), S(input, "pmlUiRoot"), S(input, "webHelpRoot"), B(input, "rebuild")),
            "knowledge.layers.search" => layers.SearchKnowledgeLayers(S(input, "query")!, I(input, "topK", 5), B(input, "includeSupplemental")),
            "knowledge.experience.record" => layers.RecordLocalExperience(S(input, "title")!, S(input, "content")!, S(input, "context")!, S(input, "id")),
            _ => throw new ArgumentException("Unknown native operation: " + id)
        };
    }

    private static OperationResult ParseResult(string id, string json, bool dispatched)
    {
        if (json.StartsWith("PML RPC failed:", StringComparison.Ordinal))
        {
            var target = json.Contains("E3D_TARGET_", StringComparison.Ordinal);
            return OperationResult.Fail(id, target ? 5 : 7, target ? "TARGET_FAILED" : "RPC_FAILED", json, mayHaveExecuted: dispatched && !target);
        }
        using var document = JsonDocument.Parse(json);
        var data = document.RootElement.Clone();
        if (data.ValueKind == JsonValueKind.Object)
        {
            foreach (var field in new[] { "ok", "Ok", "success", "Success" })
                if (data.TryGetProperty(field, out var ok) && ok.ValueKind == JsonValueKind.False)
                {
                    var message = S(data, "ErrorMessage") ?? S(data, "error") ?? S(data, "note") ?? "The underlying operation returned failure.";
                    return OperationResult.Fail(id, dispatched ? 6 : 2, dispatched ? "HOST_FAILED" : "OPERATION_REJECTED", message, data,
                        mayHaveExecuted: dispatched);
                }
        }
        return OperationResult.Ok(id, data);
    }

    private static string? S(JsonElement value, string key) => value.TryGetProperty(key, out var item) && item.ValueKind != JsonValueKind.Null ? item.GetString() : null;
    private static bool B(JsonElement value, string key, bool fallback = false) => value.TryGetProperty(key, out var item) ? item.GetBoolean() : fallback;
    private static int I(JsonElement value, string key, int fallback) => value.TryGetProperty(key, out var item) ? item.GetInt32() : fallback;
    private static long L(JsonElement value, string key, long fallback) => value.TryGetProperty(key, out var item) ? item.GetInt64() : fallback;

    private static OperationDescriptor D(string id, string legacy, string description, string properties = "", string required = "", bool target = false, bool execute = false, bool write = false)
    {
        using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + properties + "},\"required\":[" + required + "]}");
        return new(id, 1, description, schema.RootElement.Clone(), target, execute, write, LegacyTools: [legacy]);
    }

    private static IEnumerable<OperationDescriptor> Descriptors()
    {
        const string text = "{\"type\":\"string\",\"minLength\":1}";
        const string optionalText = "{\"anyOf\":[{\"type\":\"string\"},{\"type\":\"null\"}]}";
        const string boolean = "{\"type\":\"boolean\"}";
        const string identifier = "{\"type\":\"string\",\"pattern\":\"^[A-Za-z][A-Za-z0-9_]{0,63}$\"}";
        const string arrayIdentifier = "{\"type\":\"string\",\"pattern\":\"^[A-Za-z][A-Za-z0-9_]{0,127}$\"}";
        const string window = "\"startIndex\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":2147483647},\"maxItems\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":1000},\"maxCharacters\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":262144}";
        const string parameters = "{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{\"type\":{\"type\":\"string\",\"enum\":[\"string\",\"str\",\"bool\",\"boolean\",\"double\",\"real\",\"number\"]},\"value\":{\"anyOf\":[{\"type\":\"string\"},{\"type\":\"boolean\"},{\"type\":\"number\"},{\"type\":\"null\"}]}},\"required\":[\"type\",\"value\"]}}";
        yield return D("pml.array.window", "read_pml_array", "Read a bounded existing array through verified Host v1; no producer evaluation or deletion.", "\"globalVar\":" + arrayIdentifier + "," + window, "\"globalVar\"", true, true);
        yield return D("pml.forms.list", "list_pml_forms", "List native shown or loaded forms through verified Host v1 without changing forms.", "\"shownOnly\":" + boolean + "," + window, target: true, execute: true);
        yield return D("pml.gadgets.list", "list_pml_gadgets", "Read bounded native gadget metadata for a loaded form.", "\"formName\":" + identifier + "," + window, "\"formName\"", true, true);
        yield return D("pml.gadget.read", "get_pml_gadget", "Read fixed metadata of one native gadget; unavailable properties retain status and errors.", "\"formName\":" + identifier + ",\"gadgetName\":" + identifier + ",\"maxCharacters\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":262144}", "\"formName\",\"gadgetName\"", true, true);
        yield return D("pml.function.register", "register_pml_function", "Register a local source identity and scalar signature; defaults denied and does not load PML.", "\"sourceFile\":" + text + ",\"functionName\":" + text, "\"sourceFile\",\"functionName\"", write: true);
        yield return D("pml.functions.list", "list_registered_pml_functions", "List persisted source identities and invocation permission.");
        yield return D("pml.function.permission", "set_pml_function_permission", "Grant or revoke invocation permission for an unchanged source revision.", "\"functionName\":" + text + ",\"allowInvoke\":" + boolean, "\"functionName\",\"allowInvoke\"", write: true);
        yield return D("pml.function.invoke", "invoke_pml_function", "Verify source, permission and ordered typed parameters, then dispatch once to the explicit Host.", "\"functionName\":" + text + ",\"parameters\":" + parameters, "\"functionName\",\"parameters\"", true, true);
        yield return D("pml.undo.begin", "markdb", "Start an AVEVA undo group; does not implement a transactional rollback.", "\"description\":{\"type\":\"string\",\"maxLength\":256}", target: true, execute: true);
        yield return D("pml.undo.end", "endmarkdb", "Finish the AVEVA undo group without saving or undoing edits.", target: true, execute: true);
        yield return D("pml.trust.list", "list_pml_function_trust", "Read user-maintained advisory trust records.");
        yield return D("pml.trust.set", "set_pml_function_trust", "Persist explicitly requested advisory trust changes; transport errors are not evidence of untrustworthiness.", "\"functionName\":" + text + ",\"state\":{\"type\":\"string\",\"enum\":[\"untrusted\",\"trusted\",\"remove\"]},\"reason\":" + optionalText + ",\"failingCommand\":" + optionalText, "\"functionName\",\"state\"", write: true);
        yield return D("builtin.usage", "get_builtin_usage", "Read complete embedded usage guides without SQLite or AVEVA.", "\"query\":" + optionalText);
        yield return D("workspace.pmllib.inspect", "inspect_registered_pmllib", "Bounded read-only directory inspection; duplicates are candidates, not proven load conflicts.", "\"directory\":" + optionalText);
        yield return D("runtime.status", "get_ai_runtime_status", "Read CLI/desktop process heartbeats and active operations.");
        yield return D("runtime.calls", "get_recent_tool_calls", "Read bounded, correlated, redacted operation log events.", "\"limit\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200}");
        var roots = "\"pmlLibRoot\":" + optionalText + ",\"pmlUiRoot\":" + optionalText + ",\"webHelpRoot\":" + optionalText;
        var database = "\"dbName\":" + optionalText + ",\"dbPath\":" + optionalText;
        yield return D("knowledge.databases.list", "list_knowledge_databases", "List existing local indices and optional source freshness without creating indices.", "\"checkFreshness\":" + boolean);
        yield return D("knowledge.database.build", "build_knowledge_database", "Build a user-selected local index; overwrite additionally requires rebuild=true.", roots + ",\"dbName\":" + text + ",\"dbDir\":" + optionalText + ",\"rebuild\":" + boolean + ",\"maxFilesPerRoot\":{\"type\":\"integer\",\"minimum\":0}", write: true);
        yield return D("knowledge.database.check", "check_knowledge_database", "Validate SQLite integrity, schema and recorded roots.", "\"dbPath\":" + text, "\"dbPath\"");
        yield return D("knowledge.search", "search_knowledge", "Search a selected local index with source/module/chunk filters.", "\"query\":" + text + "," + database + ",\"topK\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":50},\"sourceType\":" + optionalText + ",\"module\":" + optionalText + ",\"chunkType\":" + optionalText, "\"query\"");
        yield return D("knowledge.chunk.read", "get_knowledge_chunk", "Read complete chunk text and local source provenance from its database.", "\"chunkId\":{\"type\":\"integer\",\"minimum\":1}," + database, "\"chunkId\"");
        yield return D("knowledge.source.register", "register_knowledge_source", "Index an explicitly selected official/custom source; no automatic indexing during update.", "\"role\":" + text + ",\"name\":" + text + "," + roots + ",\"rebuild\":" + boolean, "\"role\",\"name\"", write: true);
        yield return D("knowledge.layers.search", "search_knowledge_layers", "Search embedded guides and optional local source layers using the existing retrieval policy.", "\"query\":" + text + ",\"topK\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":50},\"includeSupplemental\":" + boolean, "\"query\"");
        yield return D("knowledge.experience.record", "record_local_experience", "Append a local lesson with verification context; identical retries may use the same ID.", "\"title\":" + text + ",\"content\":" + text + ",\"context\":" + text + ",\"id\":" + optionalText, "\"title\",\"content\",\"context\"", write: true);
    }

    /// <summary>将原生操作的开始、拒绝、成功和未知结果关联到现有运行日志。</summary>
    private sealed class JournalObserver(RuntimeJournal journal) : IOperationObserver
    {
        /// <summary>记录脱敏输入与显式目标。</summary>
        public string Begin(string id, JsonElement input, OperationContext context) => journal.Begin(id, input.GetRawText(), context.ProcessId);
        /// <summary>保存原始结果的成功或失败语义，不推断业务结果。</summary>
        public void Complete(string correlationId, OperationResult result, OperationContext context) => journal.Complete(correlationId,
            result.Success ? "succeeded" : result.Error?.MayHaveExecuted == true ? "cancelled-unknown" : "failed",
            result.Success ? "Completed; see operation JSON for data." : result.Error?.Code + ": " + result.Error?.Message, context.ProcessId);
    }
}
