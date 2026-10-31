namespace LaunchDesk.Next.Application.Infrastructure.Contracts;

public sealed record Record(long Sequence, DateTimeOffset Timestamp, Guid AttemptId, Guid? RunId, string Stage, string Outcome, IReadOnlyDictionary<string, string> Snapshot);
