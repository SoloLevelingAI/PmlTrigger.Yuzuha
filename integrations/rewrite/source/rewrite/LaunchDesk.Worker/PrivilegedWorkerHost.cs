using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using LaunchDesk.Kernel;
using LaunchDesk.OperationContracts;
using LaunchDesk.Windows;
using Microsoft.Win32.SafeHandles;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("LaunchDesk.Rewrite.Tests")]

namespace LaunchDesk.Worker;

/// <summary>校验受理身份和冻结计划后执行有限管理员操作。</summary>
public sealed class PrivilegedWorkerHost
{
    private readonly string? registryDirectory;

    /// <summary>由可信宿主装配保护登记根；协议请求不能覆盖此根。</summary>
    /// <param name="registryDirectory">显式隔离宿主使用的登记根；生产入口省略以保留用户保护范围。</param>
    public PrivilegedWorkerHost(string? registryDirectory = null) => this.registryDirectory = registryDirectory;
    private NamedPipeClientStream? authenticatedPipe;
    private int authenticatedParent;
    private long authenticatedStart;

    internal async Task<int> RunPipeAsync(string pipeName, int parentId, long parentStartTicks, CancellationToken ct)
    {
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(10000, ct);
        if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var serverId) || serverId != parentId) return 3;
        var begin = await WorkerTransport.ReadAsync(pipe, ct);
        if (begin.Kind != "begin" || begin.Envelope is not { } envelope || envelope.Version != 1 ||
            envelope.ParentProcessId != parentId || envelope.ParentStartUtcTicks != parentStartTicks ||
            begin.Nonce != envelope.Nonce || envelope.Nonce.Length != 64) return 4;
        authenticatedPipe = pipe;
        authenticatedParent = parentId;
        authenticatedStart = parentStartTicks;
        try
        {
            return await RunAsync(envelope, ct);
        }
        finally
        {
            authenticatedPipe = null;
        }
    }

    /// <summary>核验调用者、协议、一次性会话、期限、OperationId、执行代次及摘要；独立读取冻结计划和可信控制根，禁止请求缩小保护范围。重算变更范围、检查保护资料后，worker取得组合目标租约，锁内复核并调用文件端口。变量检查点由认证上下文代理交父进程持久确认；worker不另取执行租约。通道丢失则停止后续副作用。</summary>
    /// <param name="envelope">携带冻结请求与双向身份字段的 Worker 信封。</param>
    /// <include file="../Comments.xml" path="doc/AsyncResult/param"/>
    /// <returns>有限Worker协议退出码：无认证父通道返回2，其他非零为拒绝/失败。</returns>
    public async ValueTask<int> RunAsync(WorkerEnvelope envelope, CancellationToken ct)
    {
        // DTO不授予执行权；本实例须拥有已核验的父进程传输通道。
        if (authenticatedPipe is not { } pipe || envelope.ParentProcessId != authenticatedParent ||
            envelope.ParentStartUtcTicks != authenticatedStart) return 2;
        var parentId = authenticatedParent;
        var parentStartTicks = authenticatedStart;
        var request = envelope.Snapshot;
        try
        {
            using var parent = Process.GetProcessById(parentId);
            if (parent.StartTime.ToUniversalTime().Ticks != parentStartTicks || request.Launch is not null ||
                request.Kind is not (OperationKind.Deploy or OperationKind.ApplyVariables) ||
                !request.Request.ElevationRequested) throw new InvalidDataException("Unsupported worker envelope.");
            var directory = Path.GetDirectoryName(Path.GetFullPath(envelope.ControlRoot))!;
            var resolved = new WorkspaceLocationResolver().Resolve(new StartupOptions(DataDirectory: directory),
                AppContext.BaseDirectory);
            if (!resolved.IsSuccess || resolved.Value.MachineId != request.MachineId)
                throw new InvalidDataException("Machine identity mismatch.");
            var location = resolved.Value;
            var registry = registryDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LaunchDesk", "ControlV3");
            var guard = new WindowsControlDataGuard(registry);
            if (!guard.IsRegisteredControlRoot(envelope.ControlRoot))
                throw new InvalidDataException("Unregistered control root.");
            var journal = new JsonRunJournal(location);
            var original = await journal.ReadRequestAsync(request.Id, ct);
            if (!original.IsSuccess || CanonicalDigest.Of(original.Value) != CanonicalDigest.Of(request) ||
                !journal.IsExecutionOwnedBy(request.Id, parentId, parentStartTicks))
                throw new InvalidDataException("The parent does not hold execution admission.");
            await using var context = new PipeContext(pipe, envelope.Nonce);
            Result<Unit> result;
            if (request.Kind == OperationKind.Deploy)
            {
                var plan = request.Request.Deployment ?? throw new InvalidDataException("Missing deployment plan.");
                var reader = new PackageReader(Path.Combine(location.ControlRoot, "inspection"),
                    Path.Combine(AppContext.BaseDirectory, "Tools", "7zr.exe"));
                var staging = new PrivateStagingStore(location, reader);
                var files = new FileDeliveryPort(location, staging, guard,
                    mayMutate: () => journal.IsExecutionOwnedBy(request.Id, parentId, parentStartTicks));
                var intent = await context.ReportAsync(
                    new OperationFact(RunStage.Checking, "WorkerDeploymentIntent", "管理员执行端已独立核验父进程、执行权、计划和保护登记。"), ct);
                if (!intent.IsSuccess)
                {
                    result = Result<Unit>.Fail(intent.Error!);
                }
                else
                {
                    var acquired = await new CanonicalTargetLeaseProvider(guard).AcquireAsync(
                    [
                        new CanonicalPath(plan.Plan.TargetDirectory, plan.Plan.TargetIdentity,
                            !plan.Plan.TargetIdentity.StartsWith("missing:", StringComparison.Ordinal), true)
                    ], ct);
                    if (!acquired.IsSuccess)
                    {
                        result = Result<Unit>.Fail(acquired.Error!);
                    }
                    else
                    {
                        await using var lease = acquired.Value;
                        var applied = await files.ApplyAsync(request.Id, plan, lease, ct);
                        result = applied.IsSuccess ? Result<Unit>.Ok(new Unit()) : Result<Unit>.Fail(applied.Error!);
                    }
                }
            }
            else
            {
                if (request.Request.Variables is null ||
                    !journal.IsExecutionOwnedBy(request.Id, parentId, parentStartTicks))
                    throw new InvalidDataException("Missing variable admission.");
                result = await new WindowsEnvironmentStore(registry).ApplyVerifiedAsync(request.Request.Variables,
                    context, ct);
            }

            await WorkerTransport.WriteAsync(pipe,
                result.IsSuccess
                    ? new WorkerMessage("completed", envelope.Nonce,
                        Receipt: new WorkerReceipt(request.Id, "WorkerCompleted", true))
                    : new WorkerMessage("error", envelope.Nonce, Error: result.Error), ct);
            return result.IsSuccess ? 0 : 5;
        }
        catch (Exception)
        {
            try
            {
                await WorkerTransport.WriteAsync(pipe,
                    new WorkerMessage("error", envelope.Nonce,
                        Error: new Error("WorkerNeedsVerification", "管理员执行端未完整确认，请保留原操作资料并核验。")),
                    CancellationToken.None);
            }
            catch (IOException)
            {
            }

            return 6;
        }
    }

    private sealed class PipeContext(NamedPipeClientStream pipe, string nonce) : IOperationContext
    {
        public async ValueTask<Result<RunSnapshot>> ReportAsync(OperationFact fact, CancellationToken ct)
        {
            await WorkerTransport.WriteAsync(pipe, new WorkerMessage("fact", nonce, Fact: fact), ct);
            var response = await WorkerTransport.ReadAsync(pipe, ct);
            return response.Kind == "ack" && response.Nonce == nonce && response.Snapshot is not null
                ? Result<RunSnapshot>.Ok(response.Snapshot)
                : Result<RunSnapshot>.Fail(response.Error ?? new Error("ParentAdmissionLost", "父进程未确认本次写前事实。"));
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);
}

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 6 || args[0] != "--pipe" ||
            !args[1].StartsWith("LaunchDesk.V3.", StringComparison.Ordinal) || args[2] != "--parent" ||
            !int.TryParse(args[3], out var parent) || args[4] != "--start" ||
            !long.TryParse(args[5], out var ticks)) return 1;
        try
        {
            return await new PrivilegedWorkerHost().RunPipeAsync(args[1], parent, ticks, CancellationToken.None);
        }
        catch (Exception)
        {
            return 2;
        }
    }
}