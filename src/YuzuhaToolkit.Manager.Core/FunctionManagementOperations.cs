using System.Text.Json;
using PmlTwin.Phayton;

namespace YuzuhaToolkit.Manager;

/// <summary>函数管理的薄入口；注册、源摘要、权限与实际调用均由既有通信后端负责。</summary>
public static class FunctionManagementOperations
{
    /// <summary>声明原有通信 ID 和严格输入；装配时不创建通信进程或读写注册表。</summary>
    public static IEnumerable<OperationRegistration> Registrations(Func<CommunicationCliClient>? communication = null)
    {
        const string text = "{\"type\":\"string\",\"minLength\":1}";
        const string parameters = "{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{\"type\":{\"type\":\"string\",\"enum\":[\"string\",\"str\",\"bool\",\"boolean\",\"double\",\"real\",\"number\"]},\"value\":{\"anyOf\":[{\"type\":\"string\"},{\"type\":\"boolean\"},{\"type\":\"number\"},{\"type\":\"null\"}]}},\"required\":[\"type\",\"value\"]}}";
        yield return Entry("pml.function.register", "\"sourceFile\":" + text + ",\"functionName\":" + text, "\"sourceFile\",\"functionName\"", write: true);
        yield return Entry("pml.functions.list", "", "");
        yield return Entry("pml.function.permission", "\"functionName\":" + text + ",\"allowInvoke\":{\"type\":\"boolean\"}", "\"functionName\",\"allowInvoke\"", write: true);
        yield return Entry("pml.function.invoke", "\"functionName\":" + text + ",\"parameters\":" + parameters, "\"functionName\",\"parameters\"", execute: true);

        OperationRegistration Entry(string id, string properties, string required, bool write = false, bool execute = false)
        {
            using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + properties + "},\"required\":[" + required + "]}");
            return new(new(id, 1, "复用通信 CLI 的函数登记、列表、独立权限与单次调用；本机登记不表示 Host 已加载。", schema.RootElement.Clone(),
                RequiresPid: execute, RequiresExecution: execute, RequiresWrite: write, LegacyTools: [id switch
                { "pml.function.register" => "register_pml_function", "pml.functions.list" => "list_registered_pml_functions",
                    "pml.function.permission" => "set_pml_function_permission", _ => "invoke_pml_function" }]),
                new DelegateOperationHandler(async (operation, input, context, token) =>
                {
                    if (execute && context.ExpectedProcessStartTimeUtcTicks is not > 0)
                        return OperationResult.Fail(operation, 5, "PROCESS_IDENTITY_REQUIRED", "函数调用必须提供发现时的进程启动时间。");
                    if (execute && string.IsNullOrWhiteSpace(context.ExpectedModel))
                        return OperationResult.Fail(operation, 5, "MODULE_IDENTITY_REQUIRED", "函数调用必须提供已选模块。");
                    try
                    {
                        return (await (communication ?? CommunicationCliClient.FromEnvironment)().InvokeAsync(operation, input, context, token).ConfigureAwait(false)).Result;
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
                    { return OperationResult.Fail(operation, 7, "COMMUNICATION_CLI_UNAVAILABLE", error.Message); }
                }));
        }
    }
}
