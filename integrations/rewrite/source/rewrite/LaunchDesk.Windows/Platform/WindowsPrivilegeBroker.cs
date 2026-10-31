using System.IO.Pipes;

namespace LaunchDesk.Windows;

/// <summary>建立受限提权会话并转交已确认操作。</summary>
/// <param name="workerExecutable">有限管理员 Worker 的完整路径。</param>
/// <param name="requestElevation">是否在启动 Worker 时明确请求 UAC 提升。</param>
public sealed class WindowsPrivilegeBroker(string workerExecutable, bool requestElevation = true) : IPrivilegeBroker
{
    /// <inheritdoc />
    public async ValueTask<Result<WorkerReceipt>> ExecuteAsync(PrivilegedRequest request, CancellationToken ct)
    {
        if (request.Snapshot.Kind is not (OperationKind.Deploy or OperationKind.ApplyVariables) ||
            request.Snapshot.Launch is not null || request.Context is null ||
            !request.Snapshot.Request.ElevationRequested)
            return Result<WorkerReceipt>.Fail("UnsupportedPrivilegedRequest", "提权执行端只接受本次已确认的交付或变量计划。");
        if (!File.Exists(workerExecutable)) return Result<WorkerReceipt>.Fail("WorkerMissing", "执行端文件缺失，请使用完整发布目录。");
        var pipeName = "LaunchDesk.V3." + Guid.NewGuid().ToString("N");
        var nonce = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        using var parent = Process.GetCurrentProcess();
        using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = workerExecutable, UseShellExecute = requestElevation,
                WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = !requestElevation
            };
            if (requestElevation) start.Verb = "runas";
            foreach (var value in new[]
                     {
                         "--pipe", pipeName, "--parent", parent.Id.ToString(), "--start",
                         parent.StartTime.ToUniversalTime().Ticks.ToString()
                     }) start.ArgumentList.Add(value);
            using var worker = Process.Start(start);
            if (worker is null) return Result<WorkerReceipt>.Fail("WorkerStartFailed", "未能创建执行端进程。");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            await pipe.WaitForConnectionAsync(timeout.Token);
            if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var peer) || peer != worker.Id)
                return Result<WorkerReceipt>.Fail("WorkerIdentityMismatch", "连接进程不是本次创建的执行端。");
            var envelope = new WorkerEnvelope(1, request.Snapshot, request.ControlRoot, nonce, parent.Id,
                parent.StartTime.ToUniversalTime().Ticks);
            await WorkerTransport.WriteAsync(pipe, new WorkerMessage("begin", nonce, envelope), ct);
            while (true)
            {
                var message = await WorkerTransport.ReadAsync(pipe, ct);
                if (message.Nonce != nonce) return Result<WorkerReceipt>.Fail("WorkerIdentityMismatch", "执行端会话身份不一致。");
                if (message.Kind == "fact" && message.Fact is not null)
                {
                    var persisted = await request.Context.ReportAsync(message.Fact, ct);
                    await WorkerTransport.WriteAsync(pipe,
                        new WorkerMessage("ack", nonce, Error: persisted.Error,
                            Snapshot: persisted.IsSuccess ? persisted.Value : null), ct);
                    if (!persisted.IsSuccess) return Result<WorkerReceipt>.Fail(persisted.Error!);
                }
                else if (message.Kind == "completed" && message.Receipt?.Id == request.Snapshot.Id)
                {
                    return Result<WorkerReceipt>.Ok(message.Receipt);
                }
                else if (message.Kind == "error")
                {
                    return Result<WorkerReceipt>.Fail(message.Error ?? new Error("WorkerFailed", "执行端未完成本次计划。"));
                }
                else
                {
                    return Result<WorkerReceipt>.Fail("InvalidWorkerProtocol", "执行端返回了未支持的消息。");
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Result<WorkerReceipt>.Fail("WorkerConnectionTimeout", "等待执行端连接超时，本次操作需要核验。");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return Result<WorkerReceipt>.Fail("ElevationCancelled", "本次管理员授权已取消。");
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return Result<WorkerReceipt>.Fail("WorkerNeedsVerification", "执行端连接未完整确认，请先核验原操作记录。");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);
}