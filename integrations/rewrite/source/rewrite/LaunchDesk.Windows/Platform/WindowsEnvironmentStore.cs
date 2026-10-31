namespace LaunchDesk.Windows;

/// <summary>持久变量读取及带检查点的尽力冲突检测写入。</summary>
/// <param name="lockDirectory">跨进程变量或目标互斥使用的目录。</param>
/// <param name="readValue">读取指定变量地址当前值的测试可替换回调。</param>
/// <param name="writeValue">在明确作用域写入变量的测试可替换回调。</param>
public sealed class WindowsEnvironmentStore(
    string lockDirectory,
    Func<string, EnvironmentVariableTarget, string?>? readValue = null,
    Action<string, string?, EnvironmentVariableTarget>? writeValue = null) : IEnvironmentStore
{
    private string? ReadValue(string name, EnvironmentVariableTarget scope)
    {
        return readValue is null ? Environment.GetEnvironmentVariable(name, scope) : readValue(name, scope);
    }

    private void WriteValue(string name, string? value, EnvironmentVariableTarget scope)
    {
        if (writeValue is null) Environment.SetEnvironmentVariable(name, value, scope);
        else writeValue(name, value, scope);
    }

    /// <inheritdoc />
    public ValueTask<Result<VariableSnapshot>> ReadAsync(VariableAddress address, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!ProcessEnvironmentPolicy.ValidVariableName(address.Name) || !Enum.IsDefined(address.Scope))
            return ValueTask.FromResult(Result<VariableSnapshot>.Fail("InvalidVariable", "变量名称或作用域无效。"));
        try
        {
            return ValueTask.FromResult(
                Result<VariableSnapshot>.Ok(
                    new VariableSnapshot(address, ReadValue(address.Name, Scope(address.Scope)))));
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex) || ex is System.Security.SecurityException)
        {
            return ValueTask.FromResult(Result<VariableSnapshot>.Fail("VariableReadFailed", "无法读取所选作用域的变量。"));
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> ApplyVerifiedAsync(ConfirmedVariablePlan confirmed, IOperationContext context,
        CancellationToken ct)
    {
        var plan = confirmed.Plan;
        if (!ProcessEnvironmentPolicy.ValidVariableName(plan.Before.Address.Name) ||
            !ProcessEnvironmentPolicy.ValidVariableName(plan.NewName)
            || plan.TargetBefore.Address.Name != plan.NewName ||
            plan.TargetBefore.Address.Scope != plan.Before.Address.Scope || !Enum.IsDefined(plan.Before.Address.Scope)
            || confirmed.ConfirmedDigest != plan.PlanDigest ||
            plan.PlanDigest != CanonicalDigest.Of(plan with { PlanDigest = "" })
            || ProcessEnvironmentPolicy.Reserved(plan.Before.Address.Name) ||
            ProcessEnvironmentPolicy.Reserved(plan.NewName))
            return Result<Unit>.Fail("VariablePlanChanged", "变量计划无效或涉及保留名称。");
        if (plan.Before.Address.Scope == VariableScope.Process)
            return Result<Unit>.Fail("ProcessVariableOnly", "进程变量仅随关联应用启动注入，无需持久应用。");
        try
        {
            await using var gate = await AtomicFiles.LockAsync(Path.Combine(lockDirectory, "variables.lock"), ct);
            var before = await ReadAsync(plan.Before.Address, ct);
            var target = await ReadAsync(plan.TargetBefore.Address, ct);
            if (!before.IsSuccess || !target.IsSuccess) return Result<Unit>.Fail(before.Error ?? target.Error!);
            if (before.Value.Value != plan.Before.Value || target.Value.Value != plan.TargetBefore.Value)
                return Result<Unit>.Fail("VariableConflict", "变量被其他程序修改，请重新预览；当前值未被覆盖。");
            var intent =
                await context.ReportAsync(
                    new OperationFact(RunStage.Checking, "VariableWriteIntent", "已核验变量基线并记录写入意图。"), ct);
            if (!intent.IsSuccess) return Result<Unit>.Fail(intent.Error!);
            var scope = Scope(plan.Before.Address.Scope);
            WriteValue(plan.NewName, plan.NewValue, scope);
            var set = await ReadAsync(new VariableAddress(plan.NewName, plan.Before.Address.Scope), ct);
            if (!set.IsSuccess || set.Value.Value != plan.NewValue)
                return Result<Unit>.Fail("VariableNeedsVerification", "新变量值未能确认，请核验本次操作。");
            if (!plan.NewName.Equals(plan.Before.Address.Name, StringComparison.OrdinalIgnoreCase))
            {
                var old = await ReadAsync(plan.Before.Address, ct);
                if (!old.IsSuccess || old.Value.Value != plan.Before.Value)
                    return Result<Unit>.Fail("VariableNeedsVerification", "新变量已写入，但旧变量发生外部变化，未删除旧值。");
                var remove = await context.ReportAsync(
                    new OperationFact(RunStage.Checking, "VariableRenameDeleteIntent", "准备移除仍符合预览基线的旧变量名。"), ct);
                if (!remove.IsSuccess) return Result<Unit>.Fail(remove.Error!);
                WriteValue(plan.Before.Address.Name, null, scope);
                if (ReadValue(plan.Before.Address.Name, scope) is not null)
                    return Result<Unit>.Fail("VariableNeedsVerification", "旧变量移除结果未确认。");
            }

            if (writeValue is null)
                SendMessageTimeoutW(new IntPtr(0xffff), 0x1A, IntPtr.Zero, "Environment", 2, 1000, out _);
            return Result<Unit>.Ok(new Unit());
        }
        catch (UnauthorizedAccessException)
        {
            return Result<Unit>.Fail("AccessDenied", "当前作用域需要管理员权限，请为本次计划选择管理员执行。");
        }
        catch (Exception ex) when (AtomicFiles.Expected(ex) || ex is System.Security.SecurityException)
        {
            return Result<Unit>.Fail("VariableNeedsVerification", "变量应用未完整确认，请核验原操作，不要自动重试。");
        }
    }

    private static EnvironmentVariableTarget Scope(VariableScope scope)
    {
        return scope switch
        {
            VariableScope.User => EnvironmentVariableTarget.User,
            VariableScope.Machine => EnvironmentVariableTarget.Machine, _ => EnvironmentVariableTarget.Process
        };
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeoutW(IntPtr window, uint message, IntPtr wparam, string lparam,
        uint flags, uint timeout, out IntPtr result);
}