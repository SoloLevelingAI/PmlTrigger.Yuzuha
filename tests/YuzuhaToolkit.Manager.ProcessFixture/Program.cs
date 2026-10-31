using System.Text;
using System.Text.Json;
using PmlTwin.Phayton;
using YuzuhaToolkit.Communication;
using YuzuhaToolkit.Plugins;
using YuzuhaToolkit.PluginFixtures;

if (args.FirstOrDefault() == "--launch-cli-fixture")
{
    var root = args[1]; var marine = args[2] == "marine";
    var fixture = Environment.ProcessPath!;
    if (!marine)
        foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory)) File.Copy(file, Path.Combine(root, Path.GetFileName(file)), true);
    var entry = marine ? Path.Combine(root, "marine.bat") : Path.Combine(root, Path.GetFileName(fixture));
    if (marine) File.WriteAllText(entry, "@echo off\r\n\"" + fixture + "\" PROD\r\n");
    var init = Path.Combine(root, marine ? "evars.bat" : "evars.init"); File.WriteAllText(init, "synthetic");
    var profiles = new YuzuhaToolkit.Workspace.LaunchProfileService(new YuzuhaToolkit.Workspace.WorkspaceConfigurationStore(Path.Combine(root, "state")));
    profiles.Save(new() { Id = "fixture", Name = "Synthetic", Product = marine ? "AM" : "E3D", Kind = marine ? "MarineBatch" : "Monitor", Executable = entry,
        Initialization = init, WorkingDirectory = root, Project = "TST", User = "SYSTEM", Module = marine ? "Outfitting" : "Design",
        Environment = new() { ["YUZUHA_LAUNCH_FIXTURE_OUTPUT"] = Path.Combine(root, "child.json"), ["YUZUHA_LAUNCH_FIXTURE_RELEASE"] = Path.Combine(root, "release") } }, "missing", true);
    var workflow = new YuzuhaToolkit.Manager.LaunchWorkflowService(profiles, Path.Combine(root, "receipts"));
    var plan = workflow.Plan("fixture", profiles.Read().Revision, "capture-test");
    var planJson = JsonSerializer.SerializeToElement(plan, YuzuhaToolkit.Manager.LaunchWorkflowJsonContext.Default.LaunchWorkflowPlan);
    using var input = new StringReader(JsonSerializer.Serialize(new { plan = planJson, password = "synthetic-only" }));
    var executor = new OperationExecutor(new OperationRegistry(YuzuhaToolkit.Manager.LaunchWorkflowOperations.Registrations(workflow)));
    return await YuzuhaToolkit.Manager.ManagerCliHost.RunAsync(["op", "invoke", "launch.execute", "--input-stdin", "--allow-launch"], executor, Console.Out, inputReader: input);
}

// Synthetic process only: no AVEVA dependencies, PML or user data access.
Console.InputEncoding = new UTF8Encoding(false);
Console.OutputEncoding = new UTF8Encoding(false);
if (args.SequenceEqual(new[] { "--manager-plugin-protocol" }))
    return await PluginWorkerHost.RunAsync(FixturePluginDefinition.Capabilities, FixturePluginDefinition.Registrations(), Console.In, Console.Out);
if (args.FirstOrDefault() == "PROD" && Environment.GetEnvironmentVariable("YUZUHA_LAUNCH_FIXTURE_OUTPUT") is { } launchOutput)
{
    File.WriteAllText(launchOutput, JsonSerializer.Serialize(new { arguments = args, workingDirectory = Environment.CurrentDirectory,
        commandLine = Environment.CommandLine, processId = Environment.ProcessId, selectedEnvironment = Environment.GetEnvironmentVariable("YUZUHA_TEST_ONLY"), license = Environment.GetEnvironmentVariable("AVEVA_LICENSE_SERVER_LIST") }));
    if (Environment.GetEnvironmentVariable("YUZUHA_LAUNCH_FIXTURE_RELEASE") is { } release)
    {
        var until = DateTime.UtcNow.AddSeconds(25);
        while (!File.Exists(release) && DateTime.UtcNow < until)
        {
            Console.WriteLine(new string('x', 8192)); Console.Error.WriteLine(new string('e', 8192));
            await Task.Delay(10);
        }
    }
    return 0;
}
if (!args.SequenceEqual(new[] { "--manager-protocol" })) return 2;
var request = JsonSerializer.Deserialize(await Console.In.ReadToEndAsync(),
    CommunicationJsonContext.Default.CommunicationRequest)!;
var mode = request.Input.TryGetProperty("mode", out var selected) ? selected.GetString() : "echo";
if (request.Input.TryGetProperty("marker", out var marker)) File.AppendAllText(marker.GetString()!,
    Environment.ProcessId + "|" + System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks + "\n");
if (mode == "timeout") { await Task.Delay(TimeSpan.FromMinutes(2)); return 0; }
if (mode == "flood") { await Console.Out.WriteAsync(new string('x', 65536)); return 0; }
if (mode == "stderr-flood") { await Console.Error.WriteAsync(new string('x', 65536)); return 0; }
if (mode == "bad-json") { Console.WriteLine("invalid response"); return 0; }
Console.Error.WriteLine("fixture diagnostic 中文");
var data = JsonSerializer.SerializeToElement(new { request.Input, request.Context, workingDirectory = Environment.CurrentDirectory });
if (request.OperationId is "aveva.session.status" or "aveva.session.select") data = JsonSerializer.SerializeToElement(new {
    SessionSelected = true, Connected = true, TargetVerified = true,
    ExpectedProcessId = request.Context.ProcessId, ExpectedProcessStartTimeUtcTicks = request.Context.ExpectedProcessStartTimeUtcTicks,
    ExpectedModel = request.Context.ExpectedModel, PipeName = "fixture-pipe",
    Host = new { ProcessId = request.Context.ProcessId, ProcessStartTimeUtcTicks = request.Context.ExpectedProcessStartTimeUtcTicks,
        Model = request.Context.ExpectedModel, PipeName = "fixture-pipe", HostFramework = "net48", FormQueryVersion = 1, BoundedArrayReadVersion = 1 }
});
var result = mode == "host-failed" ? OperationResult.Fail(request.OperationId, 6, "HOST_FAILED", "synthetic rejection", data)
    : OperationResult.Ok(request.OperationId, data);
var response = new CommunicationResponse(mode == "wrong-version" ? 2 : CommunicationProtocol.Version,
    mode == "wrong-request" ? Guid.NewGuid().ToString("N") : request.RequestId, result, "fixture/1");
Console.WriteLine(JsonSerializer.Serialize(response, CommunicationJsonContext.Default.CommunicationResponse));
return mode == "wrong-exit" ? 9 : result.ExitCode;
