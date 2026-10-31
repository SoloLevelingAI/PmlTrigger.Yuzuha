namespace LaunchDesk.Next.Domain.Security;

public static class SensitiveFields
{
    public static bool ContainsSecret(string name) =>
        new[] { "PASSWORD", "SECRET", "TOKEN", "CREDENTIAL" }
            .Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
