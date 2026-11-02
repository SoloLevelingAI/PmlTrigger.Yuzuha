using System.Text.Json;
using PmlTwin.Phayton;
using Xunit;
using YuzuhaToolkit.Manager;
using YuzuhaToolkit.Manager.LaunchDesk;

namespace YuzuhaToolkit.Manager.Tests;

/// <summary>公开摘要必须覆盖旅程操作，并且能由真实返回类型序列化出来。</summary>
public sealed class JourneyOutputProjectionTests
{
    [Fact]
    public void SummariesMatchRealPayloadsAndStillAllowUnknownFields()
    {
        var export = ManagerContractCatalog.Export(ManagerOperationCatalog.Create().Registry);
        Assert.Equal(21, export.OutputProjections.Length);
        Assert.Equal(export.Journey.Stages.SelectMany(stage => stage.OperationIds), export.OutputProjections.Select(item => item.OperationId));
        Assert.Contains("unknown JSON", export.OutputBoundary);
        Assert.All(export.ExitTaxonomy, row => Assert.False(row.AutoRetry));
        var installer = Assert.Single(export.ExitTaxonomy, row => row.Id == InstallExitTaxonomy.InstallerApplyFailedCode);
        var handler = Assert.Single(export.ExitTaxonomy, row => row.Id == "MANAGER_HANDLER_FAILURE");
        var conflict = Assert.Single(export.ExitTaxonomy, row => row.Id == InstallExitTaxonomy.ConfigurationConflictCode);
        Assert.Equal(InstallExitTaxonomy.InstallerApplyFailed, installer.ExitCode);
        Assert.Equal(InstallExitTaxonomy.ManagerHandlerExit, handler.ExitCode);
        Assert.Equal(PmlTwin.Phayton.OperationExitCodes.TransportFailed, handler.ExitCode);
        Assert.Equal("Inno Setup", installer.Process);
        Assert.Equal("Manager", conflict.Process);
        Assert.Equal(InstallExitTaxonomy.ManagerConfigurationExit, conflict.ExitCode);
        Assert.NotEqual(installer.Process, conflict.Process);

        foreach (var projection in export.OutputProjections)
        {
            var sample = Sample(projection.OperationId);
            var error = JourneyOutputProjections.Match(projection.Schema, sample);
            Assert.True(error is null, projection.OperationId + " " + error);
            if (projection.Coverage == "passthrough")
            {
                Assert.Equal("connection.sessions", projection.OperationId);
                Assert.StartsWith("passthrough:", projection.SourceType);
            }
            else
            {
                var type = typeof(ManagerContractCatalog).Assembly.GetType(projection.SourceType);
                Assert.NotNull(type);
                using var extra = JsonDocument.Parse("{\"planId\":\"x\",\"unexpected\":1}");
                if (projection.OperationId is "deployment.preview")
                    Assert.Null(JourneyOutputProjections.Match(projection.Schema, Extra(sample)));
            }
        }

        var plan = export.OutputProjections.Single(item => item.OperationId == "deployment.preview");
        using var missing = JsonDocument.Parse("{\"planId\":\"only-one-field\"}");
        Assert.NotNull(JourneyOutputProjections.Match(plan.Schema, missing.RootElement));
    }

    [Fact]
    public void InstallerScriptAndHostProfilesUseTheSameTables()
    {
        var root = RepoRoot();
        var installer = File.ReadAllText(Path.Combine(root, "installer", "Yuzuha.iss"));
        Assert.Contains("FailureCode := " + InstallExitTaxonomy.InstallerApplyFailed, installer);
        Assert.Contains(InstallExitTaxonomy.InstallerApplyFailedCode, installer);
        Assert.Contains("--inspect-handoff", installer);
        Assert.DoesNotContain("--allow-write", installer);
        var view = File.ReadAllText(Path.Combine(root, "src", "YuzuhaToolkit.Desktop", "PreparationWorkbenchView.xaml"));
        Assert.DoesNotContain("Tag=\"E3D2.1\"", view);
        Assert.Contains("SelectedValuePath=\"Profile\"", view);
        var code = File.ReadAllText(Path.Combine(root, "src", "YuzuhaToolkit.Desktop", "PreparationWorkbenchView.xaml.cs"));
        Assert.Contains("ManagerHostProfiles.All", code);

        foreach (var profile in ManagerHostProfiles.All)
            Assert.Equal(profile.Framework, ManagerHostProfiles.Framework(profile.Product, profile.Profile));
        Assert.Equal("E3D2.1", ManagerHostProfiles.ProfileFor("E3D", "2.1.0"));
        Assert.Equal("E3D3.1.6", ManagerHostProfiles.ProfileFor("E3D", "3.1.6"));
        Assert.Equal("", ManagerHostProfiles.ProfileFor("E3D", "9.0"));
        Assert.Throws<ArgumentException>(() => ManagerHostProfiles.Framework("AM", "E3D2.1"));
        Assert.Throws<ArgumentException>(() => ManagerHostProfiles.Framework("E3D", "AM"));
    }

    private static JsonElement Extra(JsonElement sample)
    {
        var node = JsonSerializer.SerializeToNode(sample)!.AsObject();
        node["unspecifiedPublicField"] = "kept";
        return node.Deserialize<JsonElement>();
    }

    private static JsonElement Sample(string operation) => operation switch
    {
        "setup.handoff.inspect" => JsonSerializer.SerializeToElement(new SetupHandoffInspection("C:/pkg", "1", "manager", Hash(), Hash(), 3, 0, "next"), SetupHandoffJsonContext.Default.SetupHandoffInspection),
        "installation.discover" => JsonSerializer.SerializeToElement(new ManagerInstallationReport(true, 1, [], []), ManagerJsonContext.Default.ManagerInstallationReport),
        "installation.projects" => JsonSerializer.SerializeToElement(new ProjectModuleReport([], [], [], [], []), ManagerJsonContext.Default.ProjectModuleReport),
        "environment.discover" or "environment.resolve" => JsonSerializer.SerializeToElement(new LaunchDiscoveryResult("incomplete", Stamp(), Stamp(), "static", new([], false, 0, 5, 1, 30), [], null, [], [], []), LaunchDiscoveryJsonContext.Default.LaunchDiscoveryResult),
        "setup.legacy.preview" or "deployment.preview" or "deployment.apply" or "evars.preview" or "evars.apply" => JsonSerializer.SerializeToElement(new ManagerPlanReference(new('1', 32), "runtime.plan", Hash(), JsonSerializer.SerializeToElement(new { fileCount = 1 })), ManagerJsonContext.Default.ManagerPlanReference),
        "setup.legacy.transfer" => JsonSerializer.SerializeToElement(new SetupLegacyTransferReceipt("id", "transferred", "C:/pkg", "C:/env.bat", Hash(), Hash(), Hash(), "C:/backup", "C:/receipt"), SetupHandoffJsonContext.Default.SetupLegacyTransferReceipt),
        "migration.preview" => JsonSerializer.SerializeToElement(new MigrationPreviewReport(1, "rewrite", false, true, [], []), MigrationPreviewJsonContext.Default.MigrationPreviewReport),
        "deployment.restore" => JsonSerializer.SerializeToElement(new RuntimeDeploymentDiagnostic("restored", "C:/tx", []), ManagerJsonContext.Default.RuntimeDeploymentDiagnostic),
        "evars.restore" => JsonSerializer.SerializeToElement(new EvarsReceiptSummary("C:/evars.bat", "C:/backup", Hash(), Hash()), ManagerJsonContext.Default.EvarsReceiptSummary),
        "launch.plan" => JsonSerializer.SerializeToElement(new LaunchWorkflowPlan("launch.plan", "profile", "rev", [new("C:/mon.exe", Hash())], Stamp(), Hash()), LaunchWorkflowJsonContext.Default.LaunchWorkflowPlan),
        "launch.execute" or "launch.observe" => JsonSerializer.SerializeToElement(new LaunchReceipt(operation, Hash(), "attempt", null, "succeeded", "started", "OK", "started", Stamp()), LaunchWorkflowJsonContext.Default.LaunchReceipt),
        "connection.sessions" => JsonDocument.Parse("{\"sessions\":[{\"pid\":1}]}").RootElement.Clone(),
        "connection.select" or "connection.status" or "connection.test" => JsonSerializer.SerializeToElement(new ManagerConnectionTestResult(
            new(new(4, 64, "DESIGN", "E3D2.1", "fingerprint"), "request", Stamp(), Stamp(), Lamp(), Lamp(), Lamp(), Lamp(), Lamp()),
            OperationResult.Ok("aveva.session.status", JsonSerializer.SerializeToElement(new { unknown = true }))), ManagerJsonContext.Default.ManagerConnectionTestResult),
        _ => throw new InvalidOperationException("No public sample for " + operation)
    };

    private static ConnectionLamp Lamp() => new("grey", "UNVERIFIED", "none");
    private static string Hash() => new('a', 64);
    private static DateTimeOffset Stamp() => DateTimeOffset.Parse("2026-11-02T00:00:00Z");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "installer", "Yuzuha.iss"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }
}
