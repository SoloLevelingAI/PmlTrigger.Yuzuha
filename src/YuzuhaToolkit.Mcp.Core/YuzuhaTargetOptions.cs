namespace YuzuhaToolkit.Mcp;

/// <summary>显式选定会话的不可混用目标身份与 PID 专用管道。</summary>
public sealed class YuzuhaTargetOptions
{
    private YuzuhaTargetOptions(
        int processId,
        long processStartTimeUtcTicks,
        string expectedModel,
        string pipeName)
    {
        ProcessId = processId;
        ProcessStartTimeUtcTicks = processStartTimeUtcTicks;
        ExpectedModel = expectedModel;
        PipeName = pipeName;
    }

    /// <summary>目标或发送方操作系统进程编号；不能代替启动时间核验。</summary>
    public int ProcessId { get; }
    /// <summary>进程启动 UTC ticks；与 PID 联合识别目标，防止 PID 重用。</summary>
    public long ProcessStartTimeUtcTicks { get; }
    /// <summary>选定或显式要求匹配的 AVEVA 模块名称。</summary>
    public string ExpectedModel { get; }
    /// <summary>绑定该目标 PID 的专用 Named Pipe 名称。</summary>
    public string PipeName { get; }

    /// <summary>从已发现候选生成 PID、启动时间、模块与专用管道目标约束。</summary>
    /// <param name="candidate">最近一次只读发现得到的目标会话候选。</param>
    /// <param name="expectedModel">可选的预期 AVEVA 模块；提供时必须与 Host 身份匹配。</param>
    public static YuzuhaTargetOptions FromCandidate(
        AvevaSessionCandidate candidate,
        string expectedModel)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (string.IsNullOrWhiteSpace(expectedModel))
            throw new ArgumentException(
                "The host identity must report a nonblank AVEVA model.",
                nameof(expectedModel));

        return new YuzuhaTargetOptions(
            candidate.ProcessId,
            candidate.ProcessStartTimeUtcTicks,
            expectedModel.Trim(),
            candidate.PipeName);
    }
}
