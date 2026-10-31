namespace LaunchDesk.Rewrite.Tests;

internal static partial class Program
{
    private static void RegisterLaunchArguments()
    {
        Add("CORE Marine Paragon survives vendor percent-1 parsing and nested cmd handoff", async () =>
        {
            await using var fixture = await Fixture.Create();
            var folder = Path.Combine(fixture.Root, "Marine Install With Spaces");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "marine.cmd");
            var output = Path.Combine(fixture.Root, "marine-child.json");
            // 须匹配厂商%1比较及后续cmd/c转发，不能只回显argv。
            File.WriteAllText(path, "@echo off\r\nsetlocal\r\nset moduleArg=%1\r\n" +
                "if /i not \"%moduleArg%\"==\"Paragon\" exit /b 41\r\n" +
                "if not \"%2\"==\"noconsole\" exit /b 42\r\n" +
                "if not \"%6\"==\"\" exit /b 43\r\nset args=%1 %2 %3 %4 %5\r\n" +
                "cmd /d /v:off /s /c \"\"%FIXTURE_HOST%\" --environment-child \"%FIXTURE_OUTPUT%\" %args%\"\r\n");
            var app = new ApplicationDefinition(Guid.NewGuid(), "Fixture Marine", "12.1", EntryKind.MarineBatch,
                path, "", folder, "");
            var fields = new ProfileDraft(Guid.NewGuid(), "Fixture", app.Id, "12.1", "SEA", "SYSTEM", "/ALL", "Paragon", null, []);
            var gateway = new WindowsProcessGateway();
            var parent = Ok(gateway.CaptureEnvironment()) with { Values = Ok(gateway.CaptureEnvironment()).Values
                .SetItem("FIXTURE_HOST", Environment.ProcessPath!).SetItem("FIXTURE_OUTPUT", output) };
            var plan = Ok(new LaunchPlanBuilder(new ProcessEnvironmentPolicy()).Build(new LaunchConfiguration(new Revision(1),
                new ProfileConfiguration(fields.ProfileId!.Value, fields, new CredentialRef("fixture")), app, Resource(), []), parent));
            using var secret = new SecretLease(new TransientSecret("FixtureOnly"));
            // 真实失败反例：厂商比较须拒绝旧的带引号模块参数。
            var legacy = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"))
            {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = folder,
                Arguments = $"/d /v:off /s /c \"\"{path}\" \"Paragon\" \"noconsole\" \"SEA\" \"SYSTEM/FixtureOnly\" \"/ALL\"\""
            };
            using (var failed = Process.Start(legacy)!)
            {
                await failed.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                Equal(41, failed.ExitCode);
            }
            await using var tracked = Ok(await gateway.StartAsync(plan, secret, default));
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!File.Exists(output) && DateTime.UtcNow < deadline) await Task.Delay(50);
            Check(File.Exists(output));
            using var capture = JsonDocument.Parse(File.ReadAllText(output));
            Check(capture.RootElement.GetProperty("arguments").EnumerateArray().Select(value => value.GetString())
                .SequenceEqual(["Paragon", "noconsole", "SEA", "SYSTEM/FixtureOnly", "/ALL"]));
            Equal("4545@localhost", capture.RootElement.GetProperty("license").GetString());
        });
        foreach (var (product, initialization, module) in new[]
                 { ("E3D", "launch.init", "Design"), ("CATALOGUE", "catalogue.init", "paragon") })
            Add("CORE Monitor native receiver preserves " + product + " and " + initialization, async () =>
            {
                await using var fixture = await Fixture.Create();
                var directory = Path.Combine(fixture.Root, "Installed Product With Spaces");
                Directory.CreateDirectory(directory);
                var init = Path.Combine(directory, initialization);
                File.WriteAllText(init, "fixture");
                var output = Path.Combine(fixture.Root, "monitor-child.json");
                var app = new ApplicationDefinition(Guid.NewGuid(), "Fixture Monitor", "2.1", EntryKind.Monitor,
                    Environment.ProcessPath!, init, directory, product);
                var fields = new ProfileDraft(Guid.NewGuid(), "Fixture", app.Id, "2.1", "SEA", "SYSTEM", "/ALL", module, null, []);
                var gateway = new WindowsProcessGateway();
                var captured = Ok(gateway.CaptureEnvironment());
                var parent = captured with { Values = captured.Values.SetItem("LAUNCHDESK_TEST_MONITOR_CAPTURE", output) };
                var plan = Ok(new LaunchPlanBuilder(new ProcessEnvironmentPolicy()).Build(new LaunchConfiguration(new Revision(1),
                    new ProfileConfiguration(fields.ProfileId!.Value, fields, new CredentialRef("fixture")), app, Resource(), []), parent));
                using var secret = new SecretLease(new TransientSecret("FixtureOnly"));
                await using var tracked = Ok(await gateway.StartAsync(plan, secret, default));
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (!File.Exists(output) && DateTime.UtcNow < deadline) await Task.Delay(50);
                using var document = JsonDocument.Parse(File.ReadAllText(output));
                Check(document.RootElement.GetProperty("arguments").EnumerateArray().Select(value => value.GetString())
                    .SequenceEqual(["PROD", product, "init", init, "SEA", "SYSTEM/FixtureOnly", "/ALL", module]));
                Equal("4545@localhost", document.RootElement.GetProperty("license").GetString());
            });
    }
}
