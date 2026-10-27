using System.Text.Json;
using System.Text.Json.Serialization;
using PmlTwin.Phayton;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Mcp;

/// <summary>配置操作的结构化失败诊断；版本冲突携带当前修订，校验失败携带字段错误。</summary>
public sealed record LaunchOperationDiagnostic(string? ActualRevision = null, LaunchValidation? Validation = null);

/// <summary>已创建启动入口进程的回执，不代表 AVEVA 登录或 Host 身份核验成功。</summary>
public sealed record LaunchProcessReceipt(int ProcessId, string ProfileId, string Revision, string State);

/// <summary>启动操作附加回执的 Native AOT 元数据。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(LaunchOperationDiagnostic))]
[JsonSerializable(typeof(LaunchProcessReceipt))]
public partial class LaunchOperationJsonContext : JsonSerializerContext;

/// <summary>接线目录：启动操作 ID → LaunchProfileService / AvevaLaunchScript；只适配 JSON 与失败回执。</summary>
public static class LaunchOperationCatalog
{
    /// <summary>建立可注入服务和进程创建边界的操作集合，供正式 CLI 与隔离测试复用。</summary>
    public static IEnumerable<OperationRegistration> Registrations(LaunchProfileService? service = null,
        IProductModuleCatalog? modules = null, Func<LaunchProfile, string, int>? startProcess = null)
    {
        var profiles = service ?? new LaunchProfileService();
        var catalog = modules ?? new ProductModuleCatalog();
        var handler = new DelegateOperationHandler((id, input, context, token) =>
        {
            token.ThrowIfCancellationRequested();
            try
            {
                JsonElement result;
                switch (id)
                {
                    case "launch.workspace.read": result = JsonSerializer.SerializeToElement(profiles.Read(), LaunchLifecycleJsonContext.Default.ConfigSnapshotWorkspaceSettings); break;
                    case "launch.profile.list": result = JsonSerializer.SerializeToElement(profiles.List(S(input, "query") ?? "", S(input, "product"), S(input, "scope") ?? "all"), LaunchLifecycleJsonContext.Default.LaunchProfileReadArray); break;
                    case "launch.profile.read": result = JsonSerializer.SerializeToElement(profiles.ReadProfile(S(input, "profileId")!), LaunchLifecycleJsonContext.Default.LaunchProfileRead); break;
                    case "launch.modules.list": result = JsonSerializer.SerializeToElement(catalog.Get(new(S(input, "executable") ?? "", S(input, "product")!, S(input, "version") ?? "", S(input, "kind") ?? "Monitor")), LaunchLifecycleJsonContext.Default.ProductModuleSet); break;
                    case "launch.profile.validate": result = JsonSerializer.SerializeToElement(profiles.Validate(Profile(input)), LaunchLifecycleJsonContext.Default.LaunchValidation); break;
                    case "launch.profile.preview": result = JsonSerializer.SerializeToElement(profiles.PreviewSave(Profile(input), S(input, "expectedRevision")!), LaunchLifecycleJsonContext.Default.LaunchProfilePreview); break;
                    case "launch.profile.save": result = JsonSerializer.SerializeToElement(profiles.Save(Profile(input), S(input, "expectedRevision")!, context.AllowWrite, S(input, "reason") ?? "CLI 保存启动配置"), LaunchLifecycleJsonContext.Default.ConfigCommitWorkspaceSettings); break;
                    case "launch.profile.update": result = JsonSerializer.SerializeToElement(profiles.Update(S(input, "profileId")!, input.GetProperty("changes"), S(input, "expectedRevision")!, context.AllowWrite), LaunchLifecycleJsonContext.Default.ConfigCommitWorkspaceSettings); break;
                    case "launch.profile.history": result = JsonSerializer.SerializeToElement(profiles.History(S(input, "profileId"), input.TryGetProperty("limit", out var limit) ? limit.GetInt32() : 100), LaunchLifecycleJsonContext.Default.ConfigHistoryEntryArray); break;
                    case "launch.profile.restore.preview": result = JsonSerializer.SerializeToElement(profiles.PreviewRestore(S(input, "profileId")!, S(input, "revision")!, S(input, "expectedRevision")!), LaunchLifecycleJsonContext.Default.LaunchProfilePreview); break;
                    case "launch.profile.restore": result = JsonSerializer.SerializeToElement(profiles.Restore(S(input, "profileId")!, S(input, "revision")!, S(input, "expectedRevision")!, context.AllowWrite), LaunchLifecycleJsonContext.Default.ConfigCommitWorkspaceSettings); break;
                    case "launch.profile.delete": result = JsonSerializer.SerializeToElement(profiles.Delete(S(input, "profileId")!, S(input, "expectedRevision")!, context.AllowWrite), LaunchLifecycleJsonContext.Default.ConfigCommitWorkspaceSettings); break;
                    case "launch.profile.duplicate": result = JsonSerializer.SerializeToElement(profiles.Duplicate(S(input, "profileId")!, S(input, "expectedRevision")!, context.AllowWrite), LaunchLifecycleJsonContext.Default.LaunchProfileRead); break;
                    case "launch.profile.favorite": result = JsonSerializer.SerializeToElement(profiles.SetFavorite(S(input, "profileId")!, input.GetProperty("favorite").GetBoolean(), S(input, "expectedRevision")!, context.AllowWrite), LaunchLifecycleJsonContext.Default.ConfigCommitWorkspaceSettings); break;
                    case "launch.profile.default": result = JsonSerializer.SerializeToElement(profiles.SetDefault(S(input, "profileId")!, S(input, "expectedRevision")!, context.AllowWrite), LaunchLifecycleJsonContext.Default.ConfigCommitWorkspaceSettings); break;
                    case "launch.profile.start":
                        var started = new AvevaLaunchScript(profiles, startProcess: startProcess).StartWithPassword(
                            S(input, "profileId")!, S(input, "expectedRevision")!, S(input, "password")!, context.AllowLaunch);
                        result = JsonSerializer.SerializeToElement(
                            new LaunchProcessReceipt(started.ProcessId, started.ProfileId, started.Revision, started.State),
                            LaunchOperationJsonContext.Default.LaunchProcessReceipt);
                        break;
                    default: throw new ArgumentException("Unknown launch operation.");
                }
                return Task.FromResult(OperationResult.Ok(id, result));
            }
            catch (ConfigurationConflictException error)
            {
                return Task.FromResult(OperationResult.Fail(id, 10, "CONFIGURATION_CONFLICT", error.Message,
                    JsonSerializer.SerializeToElement(new LaunchOperationDiagnostic(error.ActualRevision), LaunchOperationJsonContext.Default.LaunchOperationDiagnostic)));
            }
            catch (LaunchProfileValidationException error)
            {
                return Task.FromResult(OperationResult.Fail(id, 2, "LAUNCH_VALIDATION_FAILED", error.Message,
                    JsonSerializer.SerializeToElement(new LaunchOperationDiagnostic(Validation: error.Validation), LaunchOperationJsonContext.Default.LaunchOperationDiagnostic)));
            }
            catch (UnauthorizedAccessException error) { return Task.FromResult(OperationResult.Fail(id, 4, "ACCESS_DENIED", error.Message)); }
            catch (Exception error) when (error is ArgumentException or JsonException or InvalidDataException or KeyNotFoundException)
            { return Task.FromResult(OperationResult.Fail(id, 2, "INVALID_INPUT", error.Message)); }
            catch (IOException error) { return Task.FromResult(OperationResult.Fail(id, 7, "STORAGE_OR_LAUNCH_FAILED", error.Message)); }
        });
        return Descriptors().Select(d => new OperationRegistration(d, handler)).ToArray();
    }

    private static LaunchProfile Profile(JsonElement input) => JsonSerializer.Deserialize(input.GetProperty("profile"), LaunchLifecycleJsonContext.Default.LaunchProfile) ?? throw new ArgumentException("profile is required.");
    private static string? S(JsonElement input, string key) => input.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetString() : null;

    private static IEnumerable<OperationDescriptor> Descriptors()
    {
        const string text = "{\"type\":\"string\",\"minLength\":1}";
        const string stringValue = "{\"type\":\"string\"}";
        const string boolean = "{\"type\":\"boolean\"}";
        var fields = string.Join(',', new[] { "id", "name", "kind", "executable", "initialization", "product", "version", "workingDirectory", "project", "user", "mdb", "module", "license", "credentialKey" }.Select(name => "\"" + name + "\":" + stringValue))
            + ",\"rememberPassword\":" + boolean + ",\"isFavorite\":" + boolean + ",\"isDefault\":" + boolean
            + ",\"environment\":{\"type\":\"object\",\"properties\":{},\"additionalProperties\":{\"type\":\"string\"}}";
        var profile = "{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + fields + "}}";
        var changes = "{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + fields + "}}";
        var id = "\"profileId\":" + text;
        var expected = "\"expectedRevision\":" + text;
        yield return D("launch.workspace.read", "Read an independent workspace snapshot and current revision.");
        yield return D("launch.profile.list", "Search saved profiles with product and favorite/recent filters.", "\"query\":" + stringValue + ",\"product\":" + stringValue + ",\"scope\":{\"type\":\"string\",\"enum\":[\"all\",\"favorites\",\"recent\"]}");
        yield return D("launch.profile.read", "Read one saved profile with revision and effect information.", id, "\"profileId\"");
        yield return D("launch.modules.list", "Get product/version-specific module options with source and verification state.", "\"product\":" + text + ",\"executable\":" + stringValue + ",\"version\":" + stringValue + ",\"kind\":" + stringValue, "\"product\"");
        yield return D("launch.profile.validate", "Validate the same profile rules used by the desktop without writing or launching.", "\"profile\":" + profile, "\"profile\"");
        yield return D("launch.profile.preview", "Preview field changes and validation against an expected document revision.", "\"profile\":" + profile + "," + expected, "\"profile\",\"expectedRevision\"");
        yield return D("launch.profile.save", "Create or update one complete profile and persist a recoverable revision.", "\"profile\":" + profile + "," + expected + ",\"reason\":" + text, "\"profile\",\"expectedRevision\"", write: true);
        yield return D("launch.profile.update", "Apply known-field changes to one profile with optimistic revision checking.", id + "," + expected + ",\"changes\":" + changes, "\"profileId\",\"expectedRevision\",\"changes\"", write: true);
        yield return D("launch.profile.history", "Read bounded configuration history, optionally for one profile.", id + ",\"limit\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":500}");
        var restore = id + "," + expected + ",\"revision\":" + text;
        yield return D("launch.profile.restore.preview", "Preview restoring one historical profile under current validation rules.", restore, "\"profileId\",\"expectedRevision\",\"revision\"");
        yield return D("launch.profile.restore", "Restore one historical profile as a new revision without launching or restoring passwords.", restore, "\"profileId\",\"expectedRevision\",\"revision\"", write: true);
        yield return D("launch.profile.delete", "Delete only the saved profile; preserve independent credentials and running processes.", id + "," + expected, "\"profileId\",\"expectedRevision\"", write: true);
        yield return D("launch.profile.duplicate", "Save a distinct copy without copying credentials, default state or launch time.", id + "," + expected, "\"profileId\",\"expectedRevision\"", write: true);
        yield return D("launch.profile.favorite", "Change favorite state with the same versioned configuration service.", id + "," + expected + ",\"favorite\":" + boolean, "\"profileId\",\"expectedRevision\",\"favorite\"", write: true);
        yield return D("launch.profile.default", "Set the sole default profile without starting it.", id + "," + expected, "\"profileId\",\"expectedRevision\"", write: true);
        yield return D("launch.profile.start", "Validate the saved revision and create its entry process; does not claim login or Host verification.", id + "," + expected + ",\"password\":" + stringValue, "\"profileId\",\"expectedRevision\",\"password\"", launch: true);
    }

    private static OperationDescriptor D(string id, string description, string properties = "", string required = "", bool write = false, bool launch = false)
    {
        using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + properties + "},\"required\":[" + required + "]}");
        return new(id, 1, description, schema.RootElement.Clone(), RequiresWrite: write, RequiresLaunch: launch);
    }
}
