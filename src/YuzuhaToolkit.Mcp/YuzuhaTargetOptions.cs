namespace YuzuhaToolkit.Mcp;

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

    public int ProcessId { get; }
    public long ProcessStartTimeUtcTicks { get; }
    public string ExpectedModel { get; }
    public string PipeName { get; }

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
