using System.Runtime.CompilerServices;

namespace LaunchDesk.Windows;

internal sealed record ProcessFamilyObservation(
    int AliveProcesses,
    int ReadyWindows,
    bool RootExited,
    int? ExitCode,
    DateTimeOffset CheckedAt);

/// <summary>实现产品入口启动及进程、窗口和可用会话证据采集。</summary>
/// <param name="observationWindow">仅观察入口和后代进程的最长时间。</param>
public sealed class WindowsProcessGateway(TimeSpan? observationWindow = null) : IProcessGateway
{
    private readonly TimeSpan window = observationWindow ?? TimeSpan.FromSeconds(90);

    /// <inheritdoc />
    public Result<EnvironmentSnapshot> CaptureEnvironment()
    {
        return Result<EnvironmentSnapshot>.Ok(new EnvironmentSnapshot(Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>().ToImmutableDictionary(entry => (string)entry.Key,
                entry => (string?)entry.Value ?? "", StringComparer.OrdinalIgnoreCase)));
    }

    /// <inheritdoc />
    public ValueTask<Result<TrackedProcess>> StartAsync(LaunchPlan plan, SecretLease? secret, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!File.Exists(plan.Executable) || !Directory.Exists(plan.WorkingDirectory))
            return ValueTask.FromResult(Result<TrackedProcess>.Fail("MissingBinding", "程序入口或工作目录不存在，请修复本机路径。"));
        if (plan.Kind == EntryKind.Monitor && (plan.Arguments.Length < 4 || !File.Exists(plan.Arguments[3].Value)))
            return ValueTask.FromResult(Result<TrackedProcess>.Fail("InitializationMissing", "初始化文件不存在。"));
        if (plan.Arguments.Any(a => a.SecretSlot) && secret is null)
            return ValueTask.FromResult(Result<TrackedProcess>.Fail("CredentialMissing", "本次启动缺少凭据。"));
        var start = new ProcessStartInfo { UseShellExecute = false, WorkingDirectory = plan.WorkingDirectory };
        start.Environment.Clear();
        foreach (var pair in plan.Environment) start.Environment[pair.Key] = pair.Value;
        string[] arguments = [];
        try
        {
            arguments = plan.Arguments
                .Select(a => a.SecretSlot ? a.SecretPrefix + new string(secret!.Borrow()) : a.Value).ToArray();
            if (arguments.Any(a => a.IndexOfAny(['\0', '\r', '\n']) >= 0))
                return ValueTask.FromResult(Result<TrackedProcess>.Fail("InvalidArguments", "启动参数包含无效字符。"));
            if (plan.Kind == EntryKind.MarineBatch)
            {
                if (!new[] { ".bat", ".cmd" }.Contains(Path.GetExtension(plan.Executable),
                        StringComparer.OrdinalIgnoreCase) || plan.Executable.IndexOfAny(['"', '%', '\r', '\n']) >= 0 ||
                    arguments.Length != 5 || !arguments[1].Equals("noconsole", StringComparison.OrdinalIgnoreCase) ||
                    arguments.Any(argument => !MarineBatchPolicy.IsLiteralToken(argument)))
                    return ValueTask.FromResult(Result<TrackedProcess>.Fail("UnsafeBatchArguments",
                        "Marine 需要五个固定顺序参数；路径或登录包含多层批处理无法原样传递的字符。请核对入口，或使用产品支持的原生 EXE 入口。"));
                start.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                start.CreateNoWindow = true;
                for (var i = 0; i < arguments.Length; i++) start.Environment["LAUNCHDESK_ARG_" + i] = arguments[i];
                // 厂商marine.bat比较原始%1而非%~1；带引号的"Paragon"是不同模块。
                // 上述共享字面量规则保证无引号词元安全通过嵌套cmd/c转交。
                start.Arguments = $"/d /v:off /s /c \"\"{plan.Executable}\" " +
                                  string.Join(" ", arguments.Select((_, i) => $"%LAUNCHDESK_ARG_{i}%")) + "\"";
            }
            else
            {
                if (!Path.GetExtension(plan.Executable).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                    return ValueTask.FromResult(Result<TrackedProcess>.Fail("UnsupportedEntrypoint", "原生入口需要指定 EXE。"));
                start.FileName = plan.Executable;
                foreach (var argument in arguments) start.ArgumentList.Add(argument);
            }

            var process = BreakawayProcess.Start(start);
            if (process is null)
                return ValueTask.FromResult(Result<TrackedProcess>.Fail("ProcessCreateFailed", "未能创建目标进程。"));
            var tracker = new ProcessFamilyObserver(process);
            return ValueTask.FromResult(Result<TrackedProcess>.Ok(new TrackedProcess(process.Id, tracker, () =>
            {
                tracker.Dispose();
                return ValueTask.CompletedTask;
            })));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex) || ex is InvalidOperationException)
        {
            var detail = ex is System.ComponentModel.Win32Exception win32 ? $"（系统错误 {win32.NativeErrorCode}）" : "";
            return ValueTask.FromResult(Result<TrackedProcess>.Fail("ProcessCreateFailed", $"目标进程创建失败{detail}，请检查入口、权限与应用设置。"));
        }
        finally
        {
            start.ArgumentList.Clear();
            start.Arguments = "";
            start.Environment.Clear();
            Array.Clear(arguments);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ProcessEvidence> ObserveAsync(TrackedProcess process,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (process.Handle is not ProcessFamilyObserver tracker)
            throw new ArgumentException("Invalid tracked-process handle.", nameof(process));
        var deadline = DateTimeOffset.UtcNow + window;
        var observedWindow = false;
        var entryExitReported = false;
        while (DateTimeOffset.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var observation = tracker.Observe();
            if (observation.ReadyWindows > 0 && !observedWindow)
            {
                observedWindow = true;
                yield return new ProcessEvidence(EvidenceKind.WindowObserved, DateTimeOffset.UtcNow, process.RootId,
                    "WindowObserved", "已观察到模块窗口；项目登录尚未独立确认。");
            }

            if (observedWindow && observation.AliveProcesses == 0)
            {
                yield return new ProcessEvidence(EvidenceKind.SessionExited, DateTimeOffset.UtcNow, process.RootId,
                    "TrackedFamilyExited", "已观察到本次跟踪的应用进程全部退出。");
                yield break;
            }

            if (observation.RootExited && !entryExitReported)
            {
                entryExitReported = true;
                yield return new ProcessEvidence(EvidenceKind.EntryExited, DateTimeOffset.UtcNow, process.RootId,
                    "EntryExited", "启动入口已退出，继续核对模块交接。");
            }

            await Task.Delay(400, ct);
        }
    }
}
