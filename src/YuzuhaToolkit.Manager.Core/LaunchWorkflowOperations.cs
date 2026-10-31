using System.Text.Json;
using PmlTwin.Phayton;

namespace YuzuhaToolkit.Manager;

/// <summary>冻结计划、受控执行及持久观察的统一操作入口。</summary>
public static class LaunchWorkflowOperations
{
    /// <summary>注册启动工作流；密码字段仅供标准输入、文件或 HTTP 内存请求，宿主不得记录输入。</summary>
    public static IEnumerable<OperationRegistration> Registrations(LaunchWorkflowService? service = null)
    {
        var workflow = service ?? new();
        var handler = new DelegateOperationHandler(async (id, input, context, token) =>
        {
            try
            {
                JsonElement data;
                if (id == "launch.plan")
                    data = JsonSerializer.SerializeToElement(workflow.Plan(S(input, "profileId"), S(input, "expectedRevision"), S(input, "operationId")), LaunchWorkflowJsonContext.Default.LaunchWorkflowPlan);
                else if (id == "launch.observe")
                    data = JsonSerializer.SerializeToElement(workflow.Observe(S(input, "operationId")), LaunchWorkflowJsonContext.Default.LaunchReceipt);
                else
                {
                    var plan = JsonSerializer.Deserialize(input.GetProperty("plan"), LaunchWorkflowJsonContext.Default.LaunchWorkflowPlan) ?? throw new ArgumentException("plan 必填。");
                    var receipt = await workflow.ExecuteAsync(plan, S(input, "password"), input.TryGetProperty("requestId", out var request) ? request.GetString() : null, context.AllowLaunch, token);
                    data = JsonSerializer.SerializeToElement(receipt, LaunchWorkflowJsonContext.Default.LaunchReceipt);
                    if (receipt.State == "unknown") return OperationResult.Fail(id, 7, receipt.Code, receipt.Message, data, mayHaveExecuted: true);
                }
                return OperationResult.Ok(id, data);
            }
            catch (YuzuhaToolkit.Workspace.ConfigurationConflictException) { return OperationResult.Fail(id, 10, "CONFIGURATION_CONFLICT", "配置修订已改变，请重新生成计划。"); }
            catch (YuzuhaToolkit.Workspace.LaunchProfileValidationException) { return OperationResult.Fail(id, 2, "LAUNCH_VALIDATION_FAILED", "启动配置未通过现有后端校验，请检查路径、产品和模块。"); }
            catch (UnauthorizedAccessException) { return OperationResult.Fail(id, 4, "ACCESS_DENIED", "执行授权或状态目录权限不足，未受理新启动。"); }
            catch (InvalidDataException error)
            {
                var code = error.Message.Split('：')[0];
                var safe = code is "PLAN_DIGEST_INVALID" or "PLAN_NOT_FOUND" or "PLAN_CACHE_INVALID" or "PLAN_CACHE_FULL" or "PLAN_EXPIRED" or "TARGET_CHANGED" or "EVARS_SELECTION_FAILED" or "OPERATION_CONFLICT" or "INVALID_LICENSE_POLICY" or "UNSUPPORTED_LICENSE_POLICY"
                    || code.StartsWith("LICENSE_", StringComparison.Ordinal) && code.All(c => char.IsAsciiLetterUpper(c) || c == '_');
                return OperationResult.Fail(id, code == "OPERATION_CONFLICT" ? 10 : 2, safe ? code : "LAUNCH_PRECHECK_REJECTED",
                    safe ? error.Message : "许可预检未通过，请检查启动配置及许可诊断。");
            }
            catch (KeyNotFoundException) { return OperationResult.Fail(id, 2, "LAUNCH_RECORD_NOT_FOUND", "启动配置或操作编号不存在。"); }
            catch (Exception error) when (error is ArgumentException or JsonException)
            { return OperationResult.Fail(id, 2, "INVALID_INPUT", "输入字段或计划格式无效，请按操作契约重试。"); }
            catch (IOException) { return OperationResult.Fail(id, 7, "LAUNCH_STORAGE_OR_LEASE_FAILED", "无法访问持久回执或取得执行租约，请按原操作编号观察，勿重放。"); }
        });
        const string text = "{\"type\":\"string\",\"minLength\":1}";
        const string operation = "\"operationId\":" + text;
        const string file = "{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{\"path\":" + text + ",\"sha256\":" + text + "},\"required\":[\"path\",\"sha256\"]}";
        const string plan = "{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + operation + ",\"profileId\":" + text + ",\"expectedRevision\":" + text + ",\"files\":{\"type\":\"array\",\"items\":" + file + "},\"expiresAt\":" + text + ",\"planDigest\":" + text + "},\"required\":[\"operationId\",\"profileId\",\"expectedRevision\",\"files\",\"expiresAt\",\"planDigest\"]}";
        yield return new(D("launch.plan", "只读目标并缓存非秘密冻结计划，不改变启动配置或厂商文件。", operation + ",\"profileId\":" + text + ",\"expectedRevision\":" + text, "\"operationId\",\"profileId\",\"expectedRevision\""), handler);
        yield return new(D("launch.execute", "先持久受理再启动；重复操作返回已有事实，未知结果禁止重放。", "\"plan\":" + plan + ",\"password\":{\"type\":\"string\"},\"requestId\":" + text, "\"plan\",\"password\"", true), handler);
        yield return new(D("launch.observe", "观察持久启动回执；入口进程存在不代表已登录。", operation, "\"operationId\""), handler);
    }
    private static string S(JsonElement input, string name) => input.GetProperty(name).GetString() ?? throw new ArgumentException(name);
    private static OperationDescriptor D(string id, string description, string fields, string required, bool launch = false)
    {
        using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + fields + "},\"required\":[" + required + "]}");
        return new(id, 1, description, schema.RootElement.Clone(), RequiresLaunch: launch);
    }
}
