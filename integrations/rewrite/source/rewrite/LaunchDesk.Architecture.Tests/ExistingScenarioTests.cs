namespace LaunchDesk.Architecture.Tests;

/// <summary>将既有控制台场景逐项纳入xUnit；各保留独立STA进程和子进程入口。</summary>
public sealed class ExistingScenarioTests
{
    /// <summary>从原测试程序发现场景，避免手工复制清单造成遗漏。</summary>
    public static IEnumerable<object[]> Scenarios
    {
        get
        {
            var start = StartInfo("--list-scenarios");
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Scenario discovery did not start.");
            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(15000) || process.ExitCode != 0)
                throw new InvalidOperationException("Scenario discovery failed: " + process.StandardError.ReadToEnd());
            var names = JsonSerializer.Deserialize<string[]>(output) ?? throw new InvalidDataException("Empty scenario list.");
            if (names.Length == 0 || names.Distinct().Count() != names.Length) throw new InvalidDataException("Missing or duplicate scenarios.");
            return names.Select(name => new object[] { name });
        }
    }

    /// <summary>执行一个既有场景，并核验它实际执行一项且输出成功结果。</summary>
    /// <param name="scenario">由原测试程序发现的场景名称。</param>
    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task ExistingScenarioRemainsInTheUnifiedSuite(string scenario)
    {
        var identity = Guid.NewGuid().ToString("N");
        // 崩溃/恢复夹具含多层GUID及临时文件名；
        // Windows运行根应短，JSON另行归档。
        var report = Path.Combine(Path.GetTempPath(), "pmltwin-rw", identity[..12]);
        var start = StartInfo("--scenario", scenario, report);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Scenario did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90)); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        Assert.True(process.ExitCode == 0, await stdout + Environment.NewLine + await stderr);
        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(report, "results.json")));
        Assert.Equal(1, result.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(1, result.RootElement.GetProperty("passed").GetInt32());
        Assert.Equal(scenario, result.RootElement.GetProperty("results")[0].GetProperty("name").GetString());
        var archive = Path.Combine(ArchitectureBoundaryTests.Repository(), "artifacts", "architecture-tests", "scenarios", identity);
        Directory.CreateDirectory(archive);
        File.Copy(Path.Combine(report, "results.json"), Path.Combine(archive, "results.json"));
    }

    private static ProcessStartInfo StartInfo(params string[] arguments)
    {
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var start = new ProcessStartInfo(Path.Combine(ArchitectureBoundaryTests.Repository(), "rewrite", "LaunchDesk.Rewrite.Tests",
            "bin", configuration, "net10.0-windows", "LaunchDesk.Rewrite.Tests.exe"))
        {
            WorkingDirectory = ArchitectureBoundaryTests.Repository(), UseShellExecute = false,
            CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return start;
    }
}

