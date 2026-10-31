namespace LaunchDesk.Windows;

/// <summary>维护ACL保护的跨进程控制根登记及活动变更范围。</summary>
/// <param name="registryDirectory">保护根注册及互斥的独立目录。</param>
public sealed class WindowsControlDataGuard(string registryDirectory) : IControlDataGuard
{
    private readonly string registryRoot = WindowsPaths.Full(registryDirectory);
    private string Registry => Path.Combine(registryRoot, "protected-roots.json");
    private string Gate => Path.Combine(registryRoot, "registration.lock");

    private sealed record Active(Guid Id, string Path, int Pid, long ProcessStartTicks);

    private sealed record Registration(ImmutableArray<string> Roots, ImmutableArray<Active> Active);

    private Registration Read()
    {
        return File.Exists(Registry) ? AtomicFiles.Read<Registration>(Registry) : new Registration([], []);
    }

    private static bool KnownDead(Active active)
    {
        try
        {
            using var process = Process.GetProcessById(active.Pid);
            return process.StartTime.ToUniversalTime().Ticks != active.ProcessStartTicks;
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Result<Unit> RegisterWorkspace(WorkspaceLocation location)
    {
        try
        {
            AtomicFiles.ProtectDirectory(registryRoot);
            using var gate = AtomicFiles.LockAsync(Gate, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            using var workspace = WindowsPaths.PinPath(location.Directory);
            using var control = WindowsPaths.PinPath(location.ControlRoot);
            var registration = Read();
            var active = registration.Active.Where(a => !KnownDead(a)).ToImmutableArray();
            if (active.Any(a =>
                    WindowsPaths.Overlap(a.Path, workspace.Path.Path) ||
                    WindowsPaths.Overlap(a.Path, control.Path.Path)))
                return Result<Unit>.Fail("ProtectedScopeConflict", "工作空间与正在执行的部署范围重叠，暂不开放写入。");
            AtomicFiles.Save(Registry,
                new Registration(
                    registration.Roots.Add(workspace.Path.Path).Add(control.Path.Path).Add(registryRoot)
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToImmutableArray(), active));
            return Result<Unit>.Ok(new Unit());
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return AtomicFiles.Failure<Unit>(ex, "ProtectionUnavailable");
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> ValidateAsync(IReadOnlyList<CanonicalPath> mutationScopes,
        CancellationToken ct)
    {
        try
        {
            await using var gate = await AtomicFiles.LockAsync(Gate, ct);
            return Validate(Read(), mutationScopes);
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            return AtomicFiles.Failure<Unit>(ex, "ProtectionUnavailable");
        }
    }

    private Result<Unit> Validate(Registration registration, IReadOnlyList<CanonicalPath> scopes)
    {
        if (registration.Roots.IsDefaultOrEmpty || scopes.Count == 0)
            return Result<Unit>.Fail("ProtectionUnavailable", "控制资料保护尚未初始化。");
        foreach (var scope in scopes)
        {
            using var pin = WindowsPaths.PinPath(scope.Path);
            if (scope.Identity.Length > 0 && pin.Path.Identity != scope.Identity)
                return Result<Unit>.Fail("IdentityChanged", "目标文件身份已改变，请重新预览。");
            foreach (var root in registration.Roots)
            {
                using var protectedPin = WindowsPaths.PinPath(root);
                if (WindowsPaths.Overlap(pin.Path.Path, protectedPin.Path.Path) ||
                    pin.Path.Identity == protectedPin.Path.Identity)
                    return Result<Unit>.Fail("ProtectedDataOverlap", "部署范围与配置、日志、备份或其他控制资料重叠，已拒绝写入。");
            }
        }

        return Result<Unit>.Ok(new Unit());
    }

    /// <inheritdoc />
    public async ValueTask<Result<ControlDataLease>> AcquireAsync(IReadOnlyList<CanonicalPath> mutationScopes,
        CancellationToken ct)
    {
        var pins = new List<WindowsPaths.Pin>();
        try
        {
            await using var gate = await AtomicFiles.LockAsync(Gate, ct);
            var registration = Read();
            var validation = Validate(registration, mutationScopes);
            if (!validation.IsSuccess) return Result<ControlDataLease>.Fail(validation.Error!);
            var active = registration.Active.Where(a => !KnownDead(a)).ToImmutableArray();
            if (active.Any(a => mutationScopes.Any(s => WindowsPaths.Overlap(a.Path, s.Path))))
                return Result<ControlDataLease>.Fail("TargetBusy", "相同或相交目标正在执行另一个操作。");
            foreach (var scope in mutationScopes) pins.Add(WindowsPaths.PinPath(scope.Path));
            using var process = Process.GetCurrentProcess();
            var id = Guid.NewGuid();
            AtomicFiles.Save(Registry,
                registration with
                {
                    Active = active.AddRange(mutationScopes.Select(s =>
                        new Active(id, s.Path, process.Id, process.StartTime.ToUniversalTime().Ticks)))
                });
            return Result<ControlDataLease>.Ok(new ControlDataLease(mutationScopes, pins, async () =>
            {
                try
                {
                    await using var release = await AtomicFiles.LockAsync(Gate, CancellationToken.None);
                    var current = Read();
                    AtomicFiles.Save(Registry,
                        current with { Active = current.Active.Where(a => a.Id != id).ToImmutableArray() });
                }
                finally
                {
                    foreach (var pin in pins) pin.Dispose();
                }
            }));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            foreach (var pin in pins) pin.Dispose();
            return AtomicFiles.Failure<ControlDataLease>(ex, "ProtectionUnavailable");
        }
    }

    /// <summary>检查路径是否属于已登记的控制资料保护范围。</summary>
    /// <param name="root">需注册或检查的规范化根目录。</param>
    /// <returns>是否满足所述条件。</returns>
    public bool IsRegisteredControlRoot(string root)
    {
        var full = WindowsPaths.Full(root);
        return Read().Roots.Contains(full, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>按真实目标范围提供含控制数据保护的跨进程组合租约。</summary>
/// <param name="guard">控制资料保护端口。</param>
public sealed class CanonicalTargetLeaseProvider(IControlDataGuard guard) : ITargetLeaseProvider
{
    internal sealed record Capability(
        IReadOnlyList<CanonicalPath> Original,
        List<WindowsPaths.Pin> Pins,
        ControlDataLease Protection);

    /// <inheritdoc />
    public async ValueTask<Result<TargetLease>> AcquireAsync(IReadOnlyList<CanonicalPath> targets, CancellationToken ct)
    {
        var protection = await guard.AcquireAsync(targets, ct);
        if (!protection.IsSuccess) return Result<TargetLease>.Fail(protection.Error!);
        var pins = new List<WindowsPaths.Pin>();
        try
        {
            foreach (var target in targets) WindowsPaths.CreatePinnedDirectory(target.Path, pins);
            var capability = new Capability(targets, pins, protection.Value);
            return Result<TargetLease>.Ok(new TargetLease(targets, capability, async () =>
            {
                foreach (var pin in pins) pin.Dispose();
                await protection.Value.DisposeAsync();
            }));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex))
        {
            foreach (var pin in pins) pin.Dispose();
            await protection.Value.DisposeAsync();
            return AtomicFiles.Failure<TargetLease>(ex);
        }
    }
}