using System.Text.Json;

namespace YuzuhaToolkit.Manager;

/// <summary>旅程操作成功数据的公开摘要。完整 <c>OperationResult.data</c> 仍是未知 JSON，摘要只承诺列出的字段。</summary>
/// <param name="OperationId">已注册旅程操作 ID。</param>
/// <param name="Coverage"><c>summary</c> 或 <c>passthrough</c>。</param>
/// <param name="SourceType">产生摘要的运行时类型；透传负载使用 passthrough: 前缀。</param>
/// <param name="Schema">JSON Schema。summary 必须允许额外字段；passthrough 为 true。</param>
public sealed record ManagerOutputProjection(string OperationId, string Coverage, string SourceType, JsonElement Schema);

/// <summary>21 项安装到操作旅程的公开摘要。操作目录仍以 <see cref="ManagerContractCatalog"/> 为唯一注册源。</summary>
public static class JourneyOutputProjections
{
    private const string Plan = """
        {"type":"object","additionalProperties":true,"description":"Success payload. Extra fields remain unknown.","properties":{"planId":{"type":"string"},"kind":{"type":"string"},"sha256":{"type":"string"},"summary":{"type":"object","additionalProperties":true}},"required":["planId","kind","sha256","summary"]}
        """;
    private const string Environment = """
        {"type":"object","additionalProperties":true,"description":"Read-only discovery summary. Draft and diagnostics may contain additional unknown fields.","properties":{"state":{"type":"string"},"observedAt":{"type":"string"},"expiresAt":{"type":"string"},"evidenceKind":{"type":"string"},"coverage":{"type":"object","additionalProperties":true},"candidates":{"type":"array","items":true},"fields":{"type":"array","items":true},"needsInput":{"type":"array","items":{"type":"string"}},"diagnostics":{"type":"array","items":true}},"required":["state","observedAt","expiresAt","evidenceKind","coverage","candidates","fields","needsInput","diagnostics"]}
        """;
    private const string LaunchReceipt = """
        {"type":"object","additionalProperties":true,"description":"Launch fact. Unknown state must not be replayed. Password is absent.","properties":{"operationId":{"type":"string"},"planDigest":{"type":"string"},"attemptId":{"type":"string"},"state":{"type":"string"},"phase":{"type":"string"},"code":{"type":"string"},"message":{"type":"string"},"updatedAt":{"type":"string"}},"required":["operationId","planDigest","attemptId","state","phase","code","message","updatedAt"]}
        """;
    private const string Connection = """
        {"type":"object","additionalProperties":true,"description":"Manager lamp summary. Nested communication.data stays unknown and grants nothing.","properties":{"evidence":{"type":"object","additionalProperties":true,"properties":{"target":{"type":"object","additionalProperties":true,"properties":{"processId":{"type":"integer"},"startTimeUtcTicks":{"type":"integer"},"model":{"type":"string"}},"required":["processId","startTimeUtcTicks","model"]},"expiresAtUtc":{"type":"string"},"overall":{"type":"object","additionalProperties":true,"properties":{"state":{"type":"string"},"code":{"type":"string"}},"required":["state","code"]}},"required":["target","expiresAtUtc","overall"]},"communication":{"type":"object","additionalProperties":true,"properties":{"operationId":{"type":"string"},"success":{"type":"boolean"},"exitCode":{"type":"integer"},"data":true},"required":["operationId","success","exitCode"]}},"required":["evidence","communication"]}
        """;

    /// <summary>按旅程顺序返回摘要；调用方不得另存一份操作 ID 清单。</summary>
    public static ManagerOutputProjection[] For(IReadOnlyList<string> operationIds)
    {
        var projections = Build();
        var map = new Dictionary<string, ManagerOutputProjection>(StringComparer.Ordinal);
        foreach (var projection in projections)
        {
            if (!map.TryAdd(projection.OperationId, projection))
                throw new InvalidOperationException("Duplicate output projection: " + projection.OperationId);
            if (projection.Coverage == "passthrough")
            {
                if (projection.Schema.ValueKind != JsonValueKind.True)
                    throw new InvalidOperationException("Passthrough projection must keep data unknown: " + projection.OperationId);
            }
            else if (projection.Coverage == "summary")
            {
                if (projection.Schema.ValueKind != JsonValueKind.Object ||
                    !projection.Schema.TryGetProperty("additionalProperties", out var additional) || additional.ValueKind != JsonValueKind.True)
                    throw new InvalidOperationException("Summary projection must allow unknown additional fields: " + projection.OperationId);
            }
            else throw new InvalidOperationException("Unknown projection coverage: " + projection.OperationId);
        }
        if (map.Count != operationIds.Count || operationIds.Any(id => !map.ContainsKey(id)))
            throw new InvalidOperationException("Output projections must match the journey operations exactly.");
        return operationIds.Select(id => map[id]).ToArray();
    }

    /// <summary>检查样本是否满足公开摘要。额外字段允许存在，不把 data 收成封闭类型。</summary>
    public static string? Match(JsonElement schema, JsonElement value, string path = "$")
    {
        if (schema.ValueKind == JsonValueKind.True) return null;
        if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("type", out var typeElement))
            return path + " has no summary schema.";
        var type = typeElement.GetString();
        var matches = type switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            _ => false
        };
        if (!matches) return path + " must have type " + type + ".";
        if (schema.TryGetProperty("enum", out var options) && !options.EnumerateArray().Any(option => JsonElement.DeepEquals(option, value)))
            return path + " is outside the public summary.";
        if (type == "object")
        {
            var hasProperties = schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!seen.Add(property.Name)) return path + "." + property.Name + " is duplicated.";
                if (!hasProperties || !properties.TryGetProperty(property.Name, out var child)) continue;
                var error = Match(child, property.Value, path + "." + property.Name);
                if (error is not null) return error;
            }
            if (schema.TryGetProperty("required", out var required))
                foreach (var key in required.EnumerateArray())
                    if (!seen.Contains(key.GetString()!)) return path + "." + key.GetString() + " is required.";
        }
        if (type == "array" && schema.TryGetProperty("items", out var items))
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                var error = Match(items, item, path + "[" + index++ + "]");
                if (error is not null) return error;
            }
        }
        return null;
    }

    private static ManagerOutputProjection[] Build() =>
    [
        Summary("setup.handoff.inspect", "YuzuhaToolkit.Manager.SetupHandoffInspection", """
            {"type":"object","additionalProperties":true,"description":"Read-only handoff summary. File readiness is not an AVEVA connection.","properties":{"packageRoot":{"type":"string"},"version":{"type":"string"},"integrationMode":{"type":"string"},"manifestHash":{"type":"string"},"markerHash":{"type":"string"},"filesVerified":{"type":"integer"},"legacyEnvironmentCount":{"type":"integer"},"nextAction":{"type":"string"}},"required":["packageRoot","version","integrationMode","manifestHash","markerHash","filesVerified","legacyEnvironmentCount","nextAction"]}
            """),
        Summary("installation.discover", "YuzuhaToolkit.Manager.ManagerInstallationReport", """
            {"type":"object","additionalProperties":true,"properties":{"complete":{"type":"boolean"},"filesSeen":{"type":"integer"},"candidates":{"type":"array","items":true},"diagnostics":{"type":"array","items":true}},"required":["complete","filesSeen","candidates","diagnostics"]}
            """),
        Summary("installation.projects", "YuzuhaToolkit.Manager.LaunchDesk.ProjectModuleReport", """
            {"type":"object","additionalProperties":true,"properties":{"projects":{"type":"array","items":true},"modules":{"type":"array","items":true},"projectRoots":{"type":"array","items":true},"filesRead":{"type":"array","items":true},"diagnostics":{"type":"array","items":true}},"required":["projects","modules","projectRoots","filesRead","diagnostics"]}
            """),
        Summary("environment.discover", "YuzuhaToolkit.Manager.LaunchDiscoveryResult", Environment),
        Summary("environment.resolve", "YuzuhaToolkit.Manager.LaunchDiscoveryResult", Environment),
        Summary("setup.legacy.preview", "YuzuhaToolkit.Manager.ManagerPlanReference", Plan),
        Summary("setup.legacy.transfer", "YuzuhaToolkit.Manager.SetupLegacyTransferReceipt", """
            {"type":"object","additionalProperties":true,"description":"Ownership receipt. It does not write EVARS.","properties":{"id":{"type":"string"},"state":{"type":"string"},"packageRoot":{"type":"string"},"environmentFile":{"type":"string"},"environmentHash":{"type":"string"},"markerBeforeHash":{"type":"string"},"markerAfterHash":{"type":"string"},"backupFile":{"type":"string"},"receiptFile":{"type":"string"}},"required":["id","state","packageRoot","environmentFile","environmentHash","markerBeforeHash","markerAfterHash","backupFile","receiptFile"]}
            """),
        Summary("migration.preview", "YuzuhaToolkit.Manager.MigrationPreviewReport", """
            {"type":"object","additionalProperties":true,"description":"Read-only migration material. activate remains false.","properties":{"schemaVersion":{"type":"integer"},"format":{"type":"string"},"activate":{"type":"boolean","enum":[false]},"complete":{"type":"boolean"},"materials":{"type":"array","items":true},"diagnostics":{"type":"array","items":true}},"required":["schemaVersion","format","activate","complete","materials","diagnostics"]}
            """),
        Summary("deployment.preview", "YuzuhaToolkit.Manager.ManagerPlanReference", Plan),
        Summary("evars.preview", "YuzuhaToolkit.Manager.ManagerPlanReference", Plan),
        Summary("deployment.apply", "YuzuhaToolkit.Manager.ManagerPlanReference", Plan),
        Summary("evars.apply", "YuzuhaToolkit.Manager.ManagerPlanReference", Plan),
        Summary("deployment.restore", "YuzuhaToolkit.Manager.RuntimeDeploymentDiagnostic", """
            {"type":"object","additionalProperties":true,"properties":{"phase":{"type":"string"},"transactionDirectory":{"type":"string"},"recoveryErrors":{"type":"array","items":{"type":"string"}}},"required":["phase","transactionDirectory","recoveryErrors"]}
            """),
        Summary("evars.restore", "YuzuhaToolkit.Manager.EvarsReceiptSummary", """
            {"type":"object","additionalProperties":true,"properties":{"file":{"type":"string"},"beforeHash":{"type":"string"},"afterHash":{"type":"string"}},"required":["file","beforeHash","afterHash"]}
            """),
        Summary("launch.plan", "YuzuhaToolkit.Manager.LaunchWorkflowPlan", """
            {"type":"object","additionalProperties":true,"description":"Frozen launch plan. It contains no password.","properties":{"operationId":{"type":"string"},"profileId":{"type":"string"},"expectedRevision":{"type":"string"},"files":{"type":"array","items":true},"expiresAt":{"type":"string"},"planDigest":{"type":"string"}},"required":["operationId","profileId","expectedRevision","files","expiresAt","planDigest"]}
            """),
        Summary("launch.execute", "YuzuhaToolkit.Manager.LaunchReceipt", LaunchReceipt),
        Summary("launch.observe", "YuzuhaToolkit.Manager.LaunchReceipt", LaunchReceipt),
        new("connection.sessions", "passthrough", "passthrough:communication-cli", True()),
        Summary("connection.select", "YuzuhaToolkit.Manager.ManagerConnectionTestResult", Connection),
        Summary("connection.status", "YuzuhaToolkit.Manager.ManagerConnectionTestResult", Connection),
        Summary("connection.test", "YuzuhaToolkit.Manager.ManagerConnectionTestResult", Connection)
    ];

    private static ManagerOutputProjection Summary(string id, string source, string schema) => new(id, "summary", source, Parse(schema));
    private static JsonElement Parse(string json) { using var document = JsonDocument.Parse(json); return document.RootElement.Clone(); }
    private static JsonElement True() { using var document = JsonDocument.Parse("true"); return document.RootElement.Clone(); }
}
