namespace LaunchDesk.Next.Domain.Processes;

public static class CommandLineRules
{
    public static bool IsLiteralBatchToken(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(c => !char.IsWhiteSpace(c) && !char.IsControl(c) && !"\"%!?&|<>^()".Contains(c));
}
