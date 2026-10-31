namespace LaunchDesk.Architecture.Tests;

/// <summary>统一入口纳入仓库旧Core/Update测试套件，不改旧测试源码。</summary>
public sealed class LegacyProductSuiteTests
{
    /// <summary>执行旧产品测试程序，并把其实际用例计数与原始结果归档。</summary>
    /// <param name="project">需保持回归覆盖的旧测试项目名称。</param>
    [Theory]
    [InlineData("LaunchDesk.Tests")]

    public async Task ExistingProductSuiteStillPasses(string project)
    {
        var repository = ArchitectureBoundaryTests.Repository();
        var report = Path.Combine(repository, "artifacts", "architecture-tests", project + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        // 禁止在测试宿主下执行dotnet run：可复用编译/构建服务器
        // 可能在套件写出结果后仍持有重定向输出句柄。
        var build = await Run("dotnet", ["build", Path.Combine(repository, project, project + ".csproj"),
            "-c", "Release", "--nologo", "-m:1", "-nr:false", "-p:UseSharedCompilation=false"], repository);
        await File.WriteAllTextAsync(report + ".build.log", build);
        await Run(Path.Combine(repository, project, "bin", "Release", "net10.0", project + ".exe"), [report], repository);
        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(report));
        Assert.Equal(0, result.RootElement.GetProperty("Failed").GetInt32());
        Assert.True(result.RootElement.GetProperty("Passed").GetInt32() > 0, "Legacy suite reported no executed tests.");
    }

    /// <summary>需要显式提供7zr的可选旧更新器回归。</summary>
    [Fact]
    [Trait("Category", "Optional7Zip")]
    public Task OptionalUpdateSuiteStillPasses() => ExistingProductSuiteStillPasses("LaunchDesk.Update.Tests");

    private static async Task<string> Run(string executable, string[] arguments, string repository)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = repository, UseShellExecute = false, CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Legacy suite process did not start.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var stdout = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var stderr = process.StandardError.ReadToEndAsync(deadline.Token);
        try { await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(deadline.Token)); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        var output = await stdout + Environment.NewLine + await stderr;
        Assert.True(process.ExitCode == 0, output);
        return output;
    }
}
