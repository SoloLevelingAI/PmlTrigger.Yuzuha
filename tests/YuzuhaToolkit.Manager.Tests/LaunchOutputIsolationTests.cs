using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace YuzuhaToolkit.Manager.Tests;

public sealed class LaunchOutputIsolationTests
{
    [Theory]
    [InlineData("monitor")]
    [InlineData("marine")]
    public async Task LaunchExecuteCliClosesCapturedOutputWhileProductContinuesRunning(string kind)
    {
        var root = Path.Combine(Path.GetTempPath(), "LaunchCapture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var fixture = Path.Combine(AppContext.BaseDirectory, "ProcessFixture", "YuzuhaToolkit.Manager.ProcessFixture.exe");
        var start = new ProcessStartInfo(fixture) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("--launch-cli-fixture"); start.ArgumentList.Add(root); start.ArgumentList.Add(kind);
        using var cli = Process.Start(start)!;
        // Same pipe EOF dependency as PowerShell's native-command | Out-String.
        var stdout = cli.StandardOutput.ReadToEndAsync(); var stderr = cli.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Process? product = null;
        try
        {
            await cli.WaitForExitAsync(deadline.Token);
            Assert.True(cli.ExitCode == 0, "CLI exit " + cli.ExitCode + ": " + await stderr.WaitAsync(TimeSpan.FromSeconds(3)));
            var receiptPath = Path.Combine(root, "child.json");
            // The fixture renames a closed pending file before publishing this path.
            while (!File.Exists(receiptPath)) await Task.Delay(20, deadline.Token);
            using var child = JsonDocument.Parse(await File.ReadAllTextAsync(receiptPath, deadline.Token));
            product = Process.GetProcessById(child.RootElement.GetProperty("processId").GetInt32());
            var output = await stdout.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("", await stderr.WaitAsync(TimeSpan.FromSeconds(3)));
            using var result = JsonDocument.Parse(output);
            Assert.Equal("success", result.RootElement.GetProperty("data").GetProperty("state").GetString());
            Assert.DoesNotContain("synthetic-only", output);
            Assert.False(product.HasExited);
        }
        finally
        {
            // The synthetic child leaves cooperatively; never kill the launched process.
            File.WriteAllText(Path.Combine(root, "release"), "done");
            if (product is not null) { await product.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); product.Dispose(); }
            await cli.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(30));
            // Marine's cmd wrapper can release its working directory just after the
            // cooperative child exits. Bound cleanup only; keep all EOF assertions strict.
            for (var attempt = 0; ; attempt++)
            {
                try { Directory.Delete(root, true); break; }
                catch (IOException) when (attempt < 19) { await Task.Delay(100); }
            }
        }
    }
}
