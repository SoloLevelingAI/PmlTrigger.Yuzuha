using System.Text.Json;
using PmlTwin.Phayton;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Plugins.Vm;

/// <summary>独立可选 VM 工作包；模拟、只读计划与实际状态动作明确分开。</summary>
public static class VmPlugin
{
    /// <summary>能力描述不加载 SDK，不读取凭据，不接触虚拟机。</summary>
    public static PluginCapabilities Capabilities => new("vm", "1.0.0", 1, Registrations().Select(r => r.Descriptor).ToArray());

    /// <summary>受控 SDK 边界可供测试替换；默认只在 execute 中使用 VIX。</summary>
    public static OperationRegistration[] Registrations(Func<VmTaskPlan, CancellationToken, Task<IVmwareSession>>? open = null)
    {
        const string text = "{\"type\":\"string\",\"minLength\":1}";
        const string profile = "\"profile\":{\"type\":\"string\",\"enum\":[\"AM\",\"PDMS\",\"E3D2.1\",\"E3D3.1.6\"]}";
        const string objectValue = "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":true}";
        var request = string.Join(',', new[] { "allowedVmRoot", "vmPath", "expectedUuid", "expectedMac", "outputDirectory", "vixLibraryPath", "credentialFile" }.Select(k => "\"" + k + "\":" + text)) + "," + profile +
            ",\"timeoutSeconds\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":90},\"steps\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":8,\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{\"action\":{\"type\":\"string\",\"enum\":[\"probe\",\"start\",\"stop\",\"createSnapshot\",\"revertSnapshot\"]},\"snapshot\":" + text + "},\"required\":[\"action\"]}}";
        return [
            Entry("vm.discover", "\"roots\":{\"type\":\"array\",\"items\":" + text + "},\"maxDepth\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":6}", "\"roots\""),
            Entry("vm.plan", request, "\"allowedVmRoot\",\"vmPath\",\"expectedUuid\",\"expectedMac\",\"profile\",\"outputDirectory\",\"vixLibraryPath\",\"steps\""),
            Entry("vm.execute", "\"plan\":" + objectValue + ",\"confirmation\":" + text + ",\"attemptId\":" + text, "\"plan\",\"confirmation\",\"attemptId\"", true),
            Entry("vm.task.read", "\"outputDirectory\":" + text + ",\"attemptId\":" + text, "\"outputDirectory\",\"attemptId\""),
            Entry("vm.simulate", profile + ",\"scenario\":{\"type\":\"string\",\"enum\":[\"ok\",\"no-session\",\"pipe-missing\",\"protocol-old\",\"identity-reused\",\"module-changed\"]}", "\"profile\",\"scenario\""),
            Entry("vm.credential.store", string.Join(',', new[] { "vmPath", "expectedUuid", "expectedMac", "directory", "password" }.Select(k => "\"" + k + "\":" + text)), "\"vmPath\",\"expectedUuid\",\"expectedMac\",\"directory\",\"password\"", secret: true)
        ];
        OperationRegistration Entry(string id, string properties, string required, bool execute = false, bool secret = false)
        {
            using var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" + properties + "},\"required\":[" + required + "]}");
            return new(new(id, 1, "Explicit VM task scope; discovery/plan/simulation never load VIX. Execute requires confirmed identity and all three action grants.", schema.RootElement.Clone(), RequiresExecution: execute, RequiresWrite: execute || secret, RequiresLaunch: execute),
                new DelegateOperationHandler(async (operation, input, _, token) => {
                    token.ThrowIfCancellationRequested();
                    switch (operation)
                    {
                        case "vm.discover": return OperationResult.Ok(operation, JsonSerializer.SerializeToElement(VmPlanning.Discover(input.GetProperty("roots").EnumerateArray().Select(v => v.GetString()!).ToArray(),
                            input.TryGetProperty("maxDepth", out var depth) ? depth.GetInt32() : 3, token), VmJsonContext.Default.VmCandidateArray));
                        case "vm.plan": return OperationResult.Ok(operation, JsonSerializer.SerializeToElement(VmPlanning.Preview(input.Deserialize(VmJsonContext.Default.VmTaskRequest) ?? throw new ArgumentException("缺少 VM 计划。")), VmJsonContext.Default.VmTaskPlan));
                        case "vm.execute":
                            var plan = input.GetProperty("plan").Deserialize(VmJsonContext.Default.VmTaskPlan) ?? throw new ArgumentException("缺少 VM 冻结计划。");
                            var evidence = await VmTaskRunner.ExecuteAsync(plan, S("confirmation"), S("attemptId"), open, token).ConfigureAwait(false);
                            var data = JsonSerializer.SerializeToElement(evidence, VmJsonContext.Default.VmTaskEvidence);
                            return evidence.State == "completed" ? OperationResult.Ok(operation, data) : OperationResult.Fail(operation, 7,
                                evidence.State == "unknown" ? "VM_TASK_UNKNOWN" : "VM_TASK_FAILED", evidence.Message, data, evidence.MayHaveExecuted);
                        case "vm.simulate":
                            var selected = S("profile"); var scenario = S("scenario"); var protocol = scenario == "protocol-old" ? 0 : 1;
                            return OperationResult.Ok(operation, JsonSerializer.SerializeToElement(new VmHostSimulation("simulation", selected, scenario, VmPlanning.Framework(selected),
                                protocol, protocol, scenario == "ok" ? "simulated-pass" : "simulated-rejection", false, "unverified"), VmJsonContext.Default.VmHostSimulation));
                        case "vm.credential.store":
                            var identity = VmPlanning.Inspect(S("vmPath"));
                            if (identity.Uuid != S("expectedUuid") || identity.Mac != S("expectedMac")) throw new InvalidDataException("VM 凭据目标不匹配。");
                            var receipt = VmCredentials.Store(S("directory"), VmCredentials.TargetKey(identity.VmPath, identity.Uuid, identity.Mac), S("password"));
                            return OperationResult.Ok(operation, JsonSerializer.SerializeToElement(receipt, VmJsonContext.Default.VmCredentialReceipt));
                        default:
                        {
                            var directory = VmPlanning.Local(S("outputDirectory")); var id = S("attemptId"); if (!Guid.TryParseExact(id, "N", out var parsed)) throw new ArgumentException("attemptId 无效。");
                            var resultFile = PathRules.Inside(directory, id + "/result.json");
                            using var stream = new FileStream(resultFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                            if (stream.Length > 1024 * 1024) throw new IOException("VM 证据超过预算。");
                            var result = JsonSerializer.Deserialize(stream, VmJsonContext.Default.VmTaskEvidence) ?? throw new InvalidDataException("VM 证据为空。");
                            if (result.AttemptId != id) throw new InvalidDataException("VM 证据关联不一致。");
                            if (result.State == "running") result = result with { State = "unknown", MayHaveExecuted = true, Message = "任务意图存在，结果待确认；未重复执行。" };
                            return OperationResult.Ok(operation, JsonSerializer.SerializeToElement(result, VmJsonContext.Default.VmTaskEvidence));
                        }
                    }
                    string S(string key) => input.GetProperty(key).GetString()!;
                }));
        }
    }
}
