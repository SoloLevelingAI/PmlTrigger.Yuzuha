using System.Text.Json;
using PmlTwin.Phayton;
using YuzuhaToolkit.Plugins;

namespace YuzuhaToolkit.PluginFixtures;

// Test-only plugin. The abrupt exits happen only inside the owned worker process.
public static class FixturePluginDefinition
{
    public static OperationRegistration[] Registrations()
    {
        using var echo = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{\"text\":{\"type\":\"string\"}},\"required\":[\"text\"],\"additionalProperties\":false}");
        using var marker = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{\"marker\":{\"type\":\"string\"}},\"required\":[\"marker\"],\"additionalProperties\":false}");
        return [
            new(new("fixture.echo", 1, "Synthetic echo.", echo.RootElement.Clone()), new DelegateOperationHandler((id, body, context, token) => {
                token.ThrowIfCancellationRequested(); return Task.FromResult(OperationResult.Ok(id, JsonSerializer.SerializeToElement(new { text = body.GetProperty("text").GetString(), pid = Environment.ProcessId, context }))); })),
            new(new("fixture.launch", 1, "Synthetic launch marker, no real external app.", marker.RootElement.Clone(), RequiresWrite: true, RequiresLaunch: true),
                new DelegateOperationHandler((id, body, _, _) => { File.AppendAllText(body.GetProperty("marker").GetString()!, "once\n"); return Task.FromResult(OperationResult.Ok(id, JsonSerializer.SerializeToElement(new { created = true }))); })),
            new(new("fixture.commit-crash", 1, "Synthetic commit then lost reply.", marker.RootElement.Clone(), RequiresWrite: true),
                new DelegateOperationHandler((id, body, _, _) => { File.AppendAllText(body.GetProperty("marker").GetString()!, "once\n"); Environment.Exit(27); throw new InvalidOperationException(id); }))
        ];
    }
    public static PluginCapabilities Capabilities => new("fixture", "1.0.0", 1, Registrations().Select(r => r.Descriptor).ToArray());
}
