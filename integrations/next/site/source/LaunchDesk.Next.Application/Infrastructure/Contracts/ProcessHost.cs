namespace LaunchDesk.Next.Application.Infrastructure.Contracts;

public enum ProcessInvocation { Native, WindowsBatch }
public enum ProcessObservation { SingleProcess, DescendantsMayDetach }

// OS instructions only: product names and login configuration stay in feature scripts.
public sealed record ProcessCommand(
    string ExecutablePath,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment,
    ProcessInvocation Invocation = ProcessInvocation.Native,
    ProcessObservation Observation = ProcessObservation.SingleProcess,
    string? SecretPlaceholder = null,
    IReadOnlyList<string>? RemoveEnvironmentKeyFragments = null);

public interface IProcessHost
{
    IProcessHandle Start(ProcessCommand command, ISecretHandle? secret = null);
}

public interface IProcessHandle : IDisposable
{
    int Id { get; }
    bool HasExited { get; }
    bool HasWindow { get; }
}

public sealed class ProcessStartUncertainException()
    : Exception("Process creation outcome is uncertain.");
