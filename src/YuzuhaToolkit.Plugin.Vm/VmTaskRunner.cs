using System.Text.Json;
using YuzuhaToolkit.Workspace;

namespace YuzuhaToolkit.Plugins.Vm;

/// <summary>独立 SDK 适配边界；发现、预览与模拟不得创建此对象。</summary>
public interface IVmwareSession : IDisposable
{
    /// <summary>是否发出过有状态动作，用于超时 unknown 判定。</summary>
    bool HasDispatched { get; }
    /// <summary>仅执行冻结步骤，不接受任意 guest shell 命令。</summary>
    Task<VmStepEvidence> ExecuteAsync(VmTaskStep step, int index, CancellationToken token);
}

/// <summary>先持久化计划和意图，再执行；相同 attemptId 只回收证据，不重复动作。</summary>
public static class VmTaskRunner
{
    /// <summary>输出绑定具体目标、快照、产品、步骤和关联号；没有失败后自动恢复或重试。</summary>
    public static async Task<VmTaskEvidence> ExecuteAsync(VmTaskPlan plan, string confirmation, string attemptId,
        Func<VmTaskPlan, CancellationToken, Task<IVmwareSession>>? open = null, CancellationToken token = default)
    {
        if (!Guid.TryParseExact(attemptId, "N", out _)) throw new ArgumentException("VM attemptId 必须为 GUID N 格式。");
        token.ThrowIfCancellationRequested(); VmPlanning.Verify(plan, confirmation);
        var output = plan.Request.OutputDirectory; PathRules.NoLinks(output); Directory.CreateDirectory(output);
        var attempt = PathRules.Inside(output, attemptId); Directory.CreateDirectory(attempt);
        var planFile = Path.Combine(attempt, "plan.json"); var resultFile = Path.Combine(attempt, "result.json");
        var initial = new VmTaskEvidence(attemptId, confirmation, plan.Request.Profile, "running", false, planFile, resultFile, [], "任务意图已记录，尚未发出状态动作。");
        FileStream guard;
        try { guard = new FileStream(Path.Combine(attempt, "attempt.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.Read); }
        catch (IOException) when (File.Exists(Path.Combine(attempt, "attempt.lock")))
        {
            PathRules.NoLinks(planFile); PathRules.NoLinks(resultFile);
            if (File.Exists(planFile))
            {
                using var previous = new FileStream(planFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (previous.Length > 1024 * 1024) throw new IOException("VM 已有计划超过预算。");
                var recorded = JsonSerializer.Deserialize(previous, VmJsonContext.Default.VmTaskPlan);
                if (recorded?.Confirmation != confirmation) throw new IOException("attemptId 已绑定另一任务，禁止重用。");
            }
            if (File.Exists(resultFile))
            {
                using var existing = new FileStream(resultFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (existing.Length > 1024 * 1024) throw new IOException("VM 任务证据超过预算。");
                var evidence = JsonSerializer.Deserialize(existing, VmJsonContext.Default.VmTaskEvidence) ?? throw new InvalidDataException("VM 任务证据为空。");
                if (evidence.Confirmation != confirmation) throw new IOException("attemptId 的结果目标不一致。");
                return evidence.State == "running" ? evidence with { State = "unknown", MayHaveExecuted = true, Message = "已有运行意图，结果尚不能确认；未重复执行。" } : evidence;
            }
            return initial with { State = "unknown", MayHaveExecuted = true, Message = "已有任务锁，结果未保存；请人工检查，未重复执行。" };
        }
        using (guard)
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            deadline.CancelAfter(TimeSpan.FromSeconds(plan.Request.TimeoutSeconds));
            WriteNew(planFile, JsonSerializer.SerializeToUtf8Bytes(plan, VmJsonContext.Default.VmTaskPlan)); Save(initial);
            var steps = new List<VmStepEvidence>(); IVmwareSession? session = null; var dispatched = false;
            try
            {
                using var libraryLease = new FileStream(plan.Request.VixLibraryPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                session = await (open ?? NativeVixSession.OpenAsync)(plan, deadline.Token).ConfigureAwait(false);
                for (var index = 0; index < plan.Request.Steps.Length; index++)
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    var current = VmPlanning.Inspect(plan.Request.VmPath);
                    if (!current.HostOnly || !current.Uuid.Equals(plan.Request.ExpectedUuid, StringComparison.OrdinalIgnoreCase) || !current.Mac.Equals(plan.Request.ExpectedMac, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("VM 当前身份/隔离网络改变，停止后续步骤。");
                    steps.Add(await session.ExecuteAsync(plan.Request.Steps[index], index, deadline.Token).ConfigureAwait(false));
                    dispatched |= session.HasDispatched;
                    Save(initial with { MayHaveExecuted = session.HasDispatched, Steps = steps.ToArray(), Message = "已回收步骤证据，尚有未完成步骤。" });
                }
                dispatched |= session.HasDispatched; session.Dispose(); session = null;
                var completed = initial with { State = "completed", MayHaveExecuted = dispatched, Steps = steps.ToArray(), Message = "选定 VM 步骤完成；不代表 AVEVA 登录、Host 连接或许可签出通过。" };
                Save(completed); return completed;
            }
            catch (Exception error)
            {
                dispatched |= session?.HasDispatched == true;
                var uncertain = dispatched || error is TimeoutException or OperationCanceledException;
                var failed = initial with { State = uncertain ? "unknown" : "failed", MayHaveExecuted = dispatched, Steps = steps.ToArray(),
                    Message = (string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message) + " 不自动重跑或恢复快照。" };
                try { Save(failed); }
                catch (Exception writing) when (writing is IOException or UnauthorizedAccessException)
                { failed = failed with { State = dispatched ? "unknown" : "failed", Message = failed.Message + " 结果写入失败：" + (string.IsNullOrWhiteSpace(writing.Message) ? writing.GetType().Name : writing.Message) }; }
                return failed;
            }
            finally
            {
                try { session?.Dispose(); }
                catch (Exception cleanup)
                {
                    try { File.WriteAllText(Path.Combine(attempt, "cleanup-warning.txt"), string.IsNullOrWhiteSpace(cleanup.Message) ? cleanup.GetType().Name : cleanup.Message); }
                    catch (Exception logging) when (logging is IOException or UnauthorizedAccessException) { }
                }
            }
        }
        void Save(VmTaskEvidence evidence)
        {
            var temporary = resultFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { WriteNew(temporary, JsonSerializer.SerializeToUtf8Bytes(evidence, VmJsonContext.Default.VmTaskEvidence)); File.Move(temporary, resultFile, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
    private static void WriteNew(string path, byte[] bytes) { PathRules.NoLinks(path); using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read); file.Write(bytes); file.Flush(true); }
}
