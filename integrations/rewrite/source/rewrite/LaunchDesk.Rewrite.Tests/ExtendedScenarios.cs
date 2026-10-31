using System.IO.Compression;

namespace LaunchDesk.Rewrite.Tests;

internal static partial class Program
{
    private static void RegisterExtended()
    {
        Add("AR22 current format cannot be migrated without its control history", async () =>
        {
            await using var f = await Fixture.Create();
            var before = File.ReadAllText(f.Location.ConfigurationFile);
            var result =
                await new MigrationPlanningService(f.Location).PreviewAsync(f.Location.ConfigurationFile,
                    new Revision(0), default);
            Check(!result.IsSuccess);
            Equal("MigrationNotRequired", result.Error!.Code);
            Equal(before, File.ReadAllText(f.Location.ConfigurationFile));
        });
        Add("AR24 target process survives an orchestrator job closing", async () =>
        {
            var root = Path.Combine(reportRoot, "job-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var start = new ProcessStartInfo(Environment.ProcessPath!)
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            start.ArgumentList.Add("--job-child");
            start.ArgumentList.Add(root);
            using var helper = Process.Start(start)!;
            await helper.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!File.Exists(Path.Combine(root, "survived.txt")) && DateTime.UtcNow < deadline) await Task.Delay(50);
            Check(File.Exists(Path.Combine(root, "started.txt")));
            Equal("survived", File.ReadAllText(Path.Combine(root, "survived.txt")));
        });
        Add("AR18 worker authenticates its parent and writes only the admitted plan", async () =>
        {
            var root = Path.Combine(reportRoot, "worker", Guid.NewGuid().ToString("N"));
            var location = Ok(new WorkspaceLocationResolver().Resolve(
                new StartupOptions(DataDirectory: Path.Combine(root, "workspace")), AppContext.BaseDirectory));
            var isolatedRegistry = Path.Combine(root, "registry");
            var guard = new WindowsControlDataGuard(isolatedRegistry);
            Ok(guard.RegisterWorkspace(location));
            var store = new JsonWorkspaceStore(location);
            Ok(await store.InitializeEmptyAsync(default));
            var reader = new PackageReader(Path.Combine(location.ControlRoot, "inspection"),
                Path.Combine(AppContext.BaseDirectory, "Tools", "7zr.exe"));
            var staging = new PrivateStagingStore(location, reader);
            var files = new FileDeliveryPort(location, staging, guard);
            var source = Path.Combine(root, "source.txt");
            File.WriteAllText(source, "worker-fixture");
            var inspected = Ok(await reader.InspectAsync(new PackageSource(PackageKind.Files, source), default));
            var plan = Ok(await new DeploymentPlanner(reader, staging, files, guard).PreviewAsync(
                new DeployRequest(new Revision(0), Guid.NewGuid(), inspected,
                    new TargetSpec(Path.Combine(root, "target"))), default));
            var frozen = new OperationRequestSnapshot(
                new OperationRequest(OperationId.New(), OperationKind.Deploy, new Revision(0),
                    Deployment: ConfirmedPlan.Confirm(plan), ElevationRequested: true), location.MachineId, null,
                plan.PlanDigest, DateTimeOffset.UtcNow);
            var journal = new JsonRunJournal(location);
            var accepted = Ok(await journal.AcceptAsync(frozen, default));
            await using var execution = Ok(await journal.TryBeginExecutionAsync(accepted, default));
            await using var context = new OperationExecutionContext(journal, new OperationStatePolicy(), execution);
            // Run the exact authenticated Worker host in this configuration's fixture executable.
            // Production Worker entry never reads this test-only environment variable.
            var executable = Environment.ProcessPath!;
            var previousRegistry = Environment.GetEnvironmentVariable("PMLTWIN_TEST_CONTROL_REGISTRY");
            Environment.SetEnvironmentVariable("PMLTWIN_TEST_CONTROL_REGISTRY", isolatedRegistry);
            try
            {
            var broker = new WindowsPrivilegeBroker(executable, false);
            var receipt =
                Ok(await broker.ExecuteAsync(new PrivilegedRequest(frozen, location.ControlRoot, context), default));
            Check(receipt.Completed);
            Equal("worker-fixture", File.ReadAllText(Path.Combine(root, "target", "source.txt")));
            Equal("Completed", Ok(await files.ReadReceiptAsync(frozen.Id, default)).State);
            Check(!(await broker.ExecuteAsync(
                new PrivilegedRequest(frozen with { Request = frozen.Request with { Kind = OperationKind.Launch } },
                    location.ControlRoot, context), default)).IsSuccess);
            Check(!(await broker.ExecuteAsync(new PrivilegedRequest(frozen, location.ControlRoot, context), default))
                .IsSuccess);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PMLTWIN_TEST_CONTROL_REGISTRY", previousRegistry);
            }
        });
        Add("AR11 accepted operations outlive callers and explicit stop does not replay", async () =>
        {
            await using var f = await Fixture.Create();
            var journal = new JsonRunJournal(f.Location);
            var handler = new WaitingHandler();
            await using var coordinator =
                new OperationCoordinator(journal, new FixtureRegistry(handler), f.Store, f.Location.MachineId);
            var plan = new MigrationPlan("fixture", new string('a', 64), new Revision(0));
            var request = new OperationRequest(OperationId.New(), OperationKind.MigrateConfiguration, new Revision(0),
                Migration: plan);
            using var caller = new CancellationTokenSource();
            Ok(await coordinator.SubmitAsync(request, caller.Token));
            caller.Cancel();
            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Equal(1, handler.Calls);
            Ok(await coordinator.SubmitAsync(request, default));
            Equal(1, handler.Calls);
            Ok(await coordinator.CancelAsync(request.Id, default));
            await handler.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        });
        Add("AR01-25 compiled dependency graph keeps UI and core independent", () =>
        {
            var kernel = typeof(ProcessEnvironmentPolicy).Assembly.GetReferencedAssemblies();
            Check(!kernel.Any(a => a.Name?.StartsWith("LaunchDesk.", StringComparison.Ordinal) == true));
            var uses = typeof(OperationCoordinator).Assembly.GetReferencedAssemblies();
            Check(!uses.Any(a => a.Name is "LaunchDesk.Windows" or "LaunchDesk.Desktop" or "LaunchDesk.Bootstrap"));
            var contracts = typeof(IOperationCommands).Assembly.GetReferencedAssemblies();
            Check(!contracts.Any(a => a.Name is "LaunchDesk.Windows" or "LaunchDesk.UseCases"));
            var desktop = System.Reflection.Assembly.LoadFrom(
                Path.GetFullPath("rewrite/LaunchDesk.Bootstrap/bin/Release/net10.0-windows/LaunchDesk.Desktop.dll"));
            Check(!desktop.GetReferencedAssemblies().Any(a =>
                a.Name is "LaunchDesk.Windows" or "LaunchDesk.UseCases" or "LaunchDesk.Bootstrap"));
            Check(!typeof(WindowsProcessGateway).Assembly.GetReferencedAssemblies().Any(a =>
                a.Name?.Contains("Aspire", StringComparison.OrdinalIgnoreCase) == true));
            return Task.CompletedTask;
        });
        Add("AR14 ZIP traversal and file-map traversal are rejected", async () =>
        {
            await using var f = await Fixture.Create();
            var path = Path.Combine(f.Root, "bad.zip");
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(zip.CreateEntry("../escape.txt").Open()))
            {
                writer.Write("bad");
            }

            Check(!(await f.Reader.InspectAsync(new PackageSource(PackageKind.Zip, path), default)).IsSuccess);
            var source = Path.Combine(f.Root, "source.txt");
            File.WriteAllText(source, "test");
            Check(!(await f.Reader.InspectAsync(
                    new PackageSource(PackageKind.Files, "", [new ToolFile(source, "..\\escape.txt")]), default))
                .IsSuccess);
            Check(!File.Exists(Path.Combine(f.Root, "escape.txt")));
        });
        Add("AR14 ZIP flattening and 7z packages use verified contents", async () =>
        {
            await using var f = await Fixture.Create();
            var path = Path.Combine(f.Root, "valid.zip");
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(zip.CreateEntry("root/folder/file.txt").Open()))
            {
                writer.Write("content");
            }

            var inspected = Ok(await f.Reader.InspectAsync(new PackageSource(PackageKind.Zip, path), default));
            var plan = Ok(await f.Planner.PreviewAsync(
                new DeployRequest(new Revision(0), Guid.NewGuid(), inspected,
                    new TargetSpec(Path.Combine(f.Root, "target"))), default));
            Equal("folder\\file.txt", plan.Files[0].RelativePath);
            // Optional external codec is not vendored into PMLTWIN.
            if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "Tools", "7zr.exe")))
            {
                var missing = await f.Reader.InspectAsync(new PackageSource(PackageKind.SevenZip, path), default);
                Check(!missing.IsSuccess);
                return;
            }
            var archive = Path.Combine(f.Root, "valid.7z");
            var source = Path.Combine(f.Root, "file.txt");
            File.WriteAllText(source, "seven");
            var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "Tools", "7zr.exe"))
            {
                WorkingDirectory = f.Root, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var argument in new[] { "a", "-t7z", archive, "file.txt" }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            await process.WaitForExitAsync();
            Equal(0, process.ExitCode);
            var seven = Ok(await f.Reader.InspectAsync(new PackageSource(PackageKind.SevenZip, archive), default));
            Equal(1, seven.Files.Length);
        });
        Add("AR16 external target changes invalidate recovery and application", async () =>
        {
            await using var f = await Fixture.Create();
            var source = Path.Combine(f.Root, "file.txt");
            File.WriteAllText(source, "new");
            var target = Path.Combine(f.Root, "target");
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "file.txt"), "before");
            var snapshot = Ok(await f.Reader.InspectAsync(new PackageSource(PackageKind.Files, source), default));
            var plan = Ok(await f.Planner.PreviewAsync(
                new DeployRequest(new Revision(0), Guid.NewGuid(), snapshot, new TargetSpec(target)), default));
            File.WriteAllText(Path.Combine(target, "file.txt"), "external");
            await using var lease =
                Ok(await f.Leases.AcquireAsync(
                    [new CanonicalPath(plan.TargetDirectory, plan.TargetIdentity, true, true)], default));
            Check(!(await f.Files.ApplyAsync(OperationId.New(), ConfirmedPlan.Confirm(plan), lease, default))
                .IsSuccess);
            Equal("external", File.ReadAllText(Path.Combine(target, "file.txt")));
        });
        Add("AR17 variable rename checks baseline and keeps old value on uncertain new write", async () =>
        {
            await using var f = await Fixture.Create();
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["Before"] = "old" };
            var store = new WindowsEnvironmentStore(f.Root, (name, _) => values.GetValueOrDefault(name),
                (name, value, _) =>
                {
                    if (value is null) values.Remove(name);
                    else values[name] = value;
                });
            var planner = new VariablePlanner(store);
            var plan = Ok(
                await planner.PreviewAsync(new VariableRenameRequest("Before", "After", "new", VariableScope.User),
                    default));
            values["Before"] = "external";
            Check(!(await store.ApplyVerifiedAsync(new ConfirmedVariablePlan(plan, plan.PlanDigest),
                new MemoryContext(), default)).IsSuccess);
            Check(!values.ContainsKey("After"));
            values["Before"] = "old";
            Ok(await store.ApplyVerifiedAsync(new ConfirmedVariablePlan(plan, plan.PlanDigest), new MemoryContext(),
                default));
            Check(!values.ContainsKey("Before"));
            Equal("new", values["After"]);
            Check(!(await planner.PreviewAsync(
                    new VariableRenameRequest("After", "AVEVA_LICENSE_SERVER_LIST", "x", VariableScope.User), default))
                .IsSuccess);
            values["Before"] = "old";
            values.Remove("After");
            var interrupted = new MemoryContext { RejectAt = 2 };
            Check(!(await store.ApplyVerifiedAsync(new ConfirmedVariablePlan(plan, plan.PlanDigest), interrupted,
                default)).IsSuccess);
            Equal("old", values["Before"]);
            Equal("new", values["After"]);
        });
        Add("AR12 native process receives exact arguments and isolated environment", async () =>
        {
            await using var f = await Fixture.Create();
            var output = Path.Combine(f.Root, "child.json");
            var executable = Environment.ProcessPath!;
            var fields = new ProfileDraft(Guid.NewGuid(), "fixture", Guid.NewGuid(), "1", "project", "name with spaces",
                "/mdb", "module", null, []);
            var app = new ApplicationDefinition(fields.ApplicationId, "fixture", "1", EntryKind.Executable, executable,
                "", f.Root, "", ArgumentTemplate: ["--environment-child", output, "{User}", "{Password}"]);
            var config = new LaunchConfiguration(new Revision(1),
                new ProfileConfiguration(fields.ProfileId!.Value, fields, new CredentialRef("fixture")), app,
                Resource(), []);
            var gateway = new WindowsProcessGateway(TimeSpan.FromSeconds(1));
            var plan = Ok(
                new LaunchPlanBuilder(new ProcessEnvironmentPolicy()).Build(config, Ok(gateway.CaptureEnvironment())));
            var inherited = Environment.GetEnvironmentVariable("AVEVA_LICENSE_SERVER_LIST");
            using var secret = new SecretLease(new TransientSecret("p&% !中文"));
            await using var process = Ok(await gateway.StartAsync(plan, secret, default));
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (!File.Exists(output) && DateTime.UtcNow < deadline) await Task.Delay(50);
            using var document = JsonDocument.Parse(File.ReadAllText(output));
            Equal("4545@localhost", document.RootElement.GetProperty("license").GetString());
            Equal("name with spaces", document.RootElement.GetProperty("arguments")[0].GetString());
            Equal("p&% !中文", document.RootElement.GetProperty("arguments")[1].GetString());
            Equal(inherited, Environment.GetEnvironmentVariable("AVEVA_LICENSE_SERVER_LIST"));
        });
        Add("AR13 Marine launch rejects unsafe batch quoting before side effects", async () =>
        {
            await using var f = await Fixture.Create();
            var path = Path.Combine(f.Root, "launch.cmd");
            File.WriteAllText(path, "@exit /b 0");
            var plan = new LaunchPlan(Guid.NewGuid(), new Revision(0), path, f.Root, EntryKind.MarineBatch,
                [new LaunchArgument("", true)], ImmutableDictionary<string, string>.Empty, null,
                new CredentialRef("fixture"), "fixture");
            using var secret = new SecretLease(new TransientSecret("contains\"quote"));
            Check(!(await new WindowsProcessGateway().StartAsync(plan, secret, default)).IsSuccess);
        });
        Add("AR10 window observation never confirms a project login", async () =>
        {
            await using var f = await Fixture.Create();
            var fields = new ProfileDraft(Guid.NewGuid(), "fixture", Guid.NewGuid(), "1", "p", "u", "/m", "d", null,
                []);
            var app = new ApplicationDefinition(fields.ApplicationId, "fixture", "1", EntryKind.Executable, "test.exe",
                "", f.Root, "");
            var context = new MemoryContext();
            var handler = new LaunchOperationHandler(new LaunchPlanBuilder(new ProcessEnvironmentPolicy()),
                new ResourceSupervisor(new RecordingProbe(), new ConnectionHealthPolicy(), f.Store), new FakeVault(),
                new ObservedGateway());
            var request = new OperationRequestSnapshot(
                new OperationRequest(OperationId.New(), OperationKind.Launch, new Revision(0)), f.Location.MachineId,
                new LaunchConfiguration(new Revision(0),
                    new ProfileConfiguration(fields.ProfileId!.Value, fields, null), app, null, []), "fixture",
                DateTimeOffset.UtcNow);
            var result = Ok(await handler.ExecuteAsync(request, context, default));
            Equal(RunStage.Unconfirmed, result.Stage);
            Check(context.Facts.Any(fact => fact.Stage == RunStage.WindowObserved));
            Check(!context.Facts.Any(fact => fact.Stage == RunStage.SessionConfirmed));
        });
        Add("AR22 legacy migration preserves identities and credential references without changing source", async () =>
        {
            await using var f = await Fixture.Create();
            var app = Guid.NewGuid();
            var license = Guid.NewGuid();
            var profile = Guid.NewGuid();
            var tool = Guid.NewGuid();
            var variable = Guid.NewGuid();
            var source = Path.Combine(f.Root, "legacy.json");
            File.WriteAllText(source,
                JsonSerializer.Serialize(new
                {
                    SchemaVersion = 1,
                    Applications = new[]
                    {
                        new
                        {
                            Id = app.ToString("N"), Name = "legacy", Version = "3.1", Kind = 0,
                            ExecutablePath = "mon.exe", InitializationPath = "launch.init", WorkingDirectory = f.Root
                        }
                    },
                    Licenses = new[] { new { Id = license, Name = "legacy", Host = "localhost" } },
                    Tools = new[]
                    {
                        new
                        {
                            Id = tool, Name = "legacy-tool", Group = "PML",
                            TargetDirectory = Path.Combine(f.Root, "target"), SourceKind = 1, SourcePath = f.Root,
                            RecordsDirectory = Path.Combine(f.Root, "old-records"),
                            Variables = new[]
                                { new { Id = variable, Name = "ToolRoot", Value = "{Target}", Scope = 0 } }
                        }
                    },
                    Profiles = new[]
                    {
                        new
                        {
                            Id = profile, Name = "legacy-profile", AppId = app, LicenseId = license, Project = "mar",
                            Username = "SYSTEM", Mdb = "/ALL", Module = "design", CredentialKey = "fixture-key",
                            ToolIds = new[] { tool }
                        }
                    }
                }));
            var before = File.ReadAllText(source);
            var planning = new MigrationPlanningService(f.Location);
            var preview = Ok(await planning.PreviewAsync(source, new Revision(0), default));
            var id = OperationId.New();
            var migration = new JsonMigrationStore(f.Location, f.Store);
            var request = new OperationRequestSnapshot(
                new OperationRequest(id, OperationKind.MigrateConfiguration, new Revision(0), Migration: preview.Plan),
                f.Location.MachineId, null, preview.Plan.SourceSha256, DateTimeOffset.UtcNow);
            Ok(await migration.PrepareAsync(request, default));
            Ok(await migration.ValidateAsync(id, default));
            Ok(await migration.ActivateAsync(id, new Revision(0), default));
            var current = Ok(await f.Store.ReadAsync(default));
            Equal(profile, current.Profiles[0].Id);
            Equal("LaunchDesk/Profile/fixture-key", current.Profiles[0].Credential!.Value);
            Equal(tool, current.Profiles[0].Fields.ToolIds[0]);
            Equal(before, File.ReadAllText(source));
            var history = Ok(await new DeploymentHistoryQueries(f.Files, f.Store).ReadAsync(default));
            Equal("LegacyUnavailable", history[0].State);
        });
        Add("AR05 tool conflicts and cycles prevent launch planning", () =>
        {
            var fields = new ProfileDraft(Guid.NewGuid(), "fixture", Guid.NewGuid(), "", "p", "u", "/m", "d", null, []);
            var app = new ApplicationDefinition(fields.ApplicationId, "fixture", "", EntryKind.Executable, "test.exe",
                "", "C:\\", "");
            var tool = new ToolDefinition(Guid.NewGuid(), "fixture", "", "C:\\",
                new PackageSource(PackageKind.Folder, "C:\\"), "",
                [
                    new ToolVariable(Guid.NewGuid(), "One", "%Two%", VariableScope.Process),
                    new ToolVariable(Guid.NewGuid(), "Two", "%One%", VariableScope.Process)
                ]);
            var builder = new LaunchPlanBuilder(new ProcessEnvironmentPolicy());
            Check(!builder
                .Build(
                    new LaunchConfiguration(new Revision(0),
                        new ProfileConfiguration(fields.ProfileId!.Value, fields, null), app, null, [tool]),
                    new EnvironmentSnapshot(ImmutableDictionary<string, string>.Empty)).IsSuccess);
            var conflict = tool with
            {
                Id = Guid.NewGuid(),
                Variables = [new ToolVariable(Guid.NewGuid(), "One", "different", VariableScope.Process)]
            };
            Check(!builder
                .Build(
                    new LaunchConfiguration(new Revision(0),
                        new ProfileConfiguration(fields.ProfileId!.Value, fields, null), app, null, [tool, conflict]),
                    new EnvironmentSnapshot(ImmutableDictionary<string, string>.Empty)).IsSuccess);
            return Task.CompletedTask;
        });
    }

    private sealed class MemoryContext : IOperationContext
    {
        internal List<OperationFact> Facts { get; } = [];
        internal int RejectAt { get; init; }

        public ValueTask<Result<RunSnapshot>> ReportAsync(OperationFact fact, CancellationToken ct)
        {
            Facts.Add(fact);
            return ValueTask.FromResult(RejectAt > 0 && Facts.Count == RejectAt
                ? Result<RunSnapshot>.Fail("Fault", "fixture")
                : Result<RunSnapshot>.Ok(new RunSnapshot(OperationId.New(), OperationKind.Launch, Facts.Count,
                    fact.Stage, fact.ReasonCode, fact.SafeMessage, DateTimeOffset.UtcNow, false, [])));
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixtureRegistry(IOperationHandler handler) : IOperationHandlerRegistry
    {
        public Result<HandlerLease> Create(OperationKind kind)
        {
            return Result<HandlerLease>.Ok(new HandlerLease(handler));
        }
    }

    private sealed class WaitingHandler : IOperationHandler
    {
        internal int Calls;
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Stopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<Result<HandlerOutcome>> ExecuteAsync(OperationRequestSnapshot request,
            IOperationContext context, CancellationToken ct)
        {
            Calls++;
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
                return Result<HandlerOutcome>.Ok(new HandlerOutcome(RunStage.Completed, "fixture", "fixture"));
            }
            finally
            {
                Stopped.TrySetResult();
            }
        }
    }

    private sealed class ObservedGateway : IProcessGateway
    {
        public Result<EnvironmentSnapshot> CaptureEnvironment()
        {
            return Result<EnvironmentSnapshot>.Ok(new EnvironmentSnapshot(ImmutableDictionary<string, string>.Empty));
        }

        public ValueTask<Result<TrackedProcess>> StartAsync(LaunchPlan plan, SecretLease? secret, CancellationToken ct)
        {
            return ValueTask.FromResult(
                Result<TrackedProcess>.Ok(new TrackedProcess(1, new object(), () => ValueTask.CompletedTask)));
        }

        public async IAsyncEnumerable<ProcessEvidence> ObserveAsync(TrackedProcess process,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield return new ProcessEvidence(EvidenceKind.EntryExited, DateTimeOffset.UtcNow, 1, "EntryExited",
                "fixture");
            yield return new ProcessEvidence(EvidenceKind.WindowObserved, DateTimeOffset.UtcNow, 2, "WindowObserved",
                "fixture");
        }
    }
}