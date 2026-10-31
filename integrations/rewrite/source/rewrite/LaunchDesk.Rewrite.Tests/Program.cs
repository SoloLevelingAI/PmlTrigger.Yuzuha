namespace LaunchDesk.Rewrite.Tests;

internal static partial class Program
{
    private static readonly List<(string Name, Func<Task> Run)> Tests = [];
    private static string reportRoot = "";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.FirstOrDefault() == "PROD" && Environment.GetEnvironmentVariable("LAUNCHDESK_TEST_MONITOR_CAPTURE") is { Length: > 0 } capture)
        {
            File.WriteAllText(capture, JsonSerializer.Serialize(new { arguments = args,
                license = Environment.GetEnvironmentVariable("AVEVA_LICENSE_SERVER_LIST") }));
            return 0;
        }
        if (args.Length > 0 && args[0] == "--journal-child") return JournalChild(args).GetAwaiter().GetResult();
        if (args.Length > 1 && args[0] == "--job-child") return JobChild(args[1]).GetAwaiter().GetResult();
        if (args.Length > 1 && args[0] == "--survive-child")
        {
            Thread.Sleep(600);
            File.WriteAllText(args[1], "survived");
            return 0;
        }

        if (args.Length > 1 && args[0] == "--environment-child")
        {
            File.WriteAllText(args[1],
                JsonSerializer.Serialize(new
                {
                    license = Environment.GetEnvironmentVariable("AVEVA_LICENSE_SERVER_LIST"),
                    arguments = args.Skip(2).ToArray()
                }));
            return 0;
        }

        if (args.Length == 6 && args[0] == "--pipe" && args[2] == "--parent" && args[4] == "--start" &&
            args[1].StartsWith("LaunchDesk.V3.", StringComparison.Ordinal) &&
            int.TryParse(args[3], out var workerParent) && long.TryParse(args[5], out var workerTicks))
        {
            var isolatedRegistry = Environment.GetEnvironmentVariable("PMLTWIN_TEST_CONTROL_REGISTRY");
            if (string.IsNullOrWhiteSpace(isolatedRegistry)) return 2;
            return new LaunchDesk.Worker.PrivilegedWorkerHost(isolatedRegistry)
                .RunPipeAsync(args[1], workerParent, workerTicks, CancellationToken.None).GetAwaiter().GetResult();
        }
        Register();
        if (args.FirstOrDefault() == "--list-scenarios")
        {
            Console.WriteLine(JsonSerializer.Serialize(Tests.Select(test => test.Name)));
            return 0;
        }
        var scenario = args.FirstOrDefault() == "--scenario" ? args.ElementAtOrDefault(1) : null;
        if (args.FirstOrDefault() == "--scenario" && (scenario is null || Tests.All(test => test.Name != scenario)))
        {
            Console.Error.WriteLine("Unknown scenario; use --list-scenarios.");
            return 2;
        }
        reportRoot = Path.GetFullPath((scenario is null ? args.FirstOrDefault() : args.ElementAtOrDefault(2))
            ?? Path.Combine(AppContext.BaseDirectory, "reports"));
        Directory.CreateDirectory(reportRoot);
        var selected = Tests.Where(test => scenario is null || test.Name == scenario).ToArray();
        var results = new List<object>();
        var failed = 0;
        foreach (var (name, run) in selected)
            try
            {
                run().GetAwaiter().GetResult();
                Console.WriteLine("PASS " + name);
                results.Add(new { name, passed = true });
            }
            catch (Exception error)
            {
                failed++;
                Console.WriteLine("FAIL " + name + ": " + error);
                results.Add(new { name, passed = false, error = error.ToString() });
            }

        File.WriteAllText(Path.Combine(reportRoot, "results.json"),
            JsonSerializer.Serialize(new { total = selected.Length, passed = selected.Length - failed, failed, results },
                new JsonSerializerOptions { WriteIndented = true }));
        return failed == 0 ? 0 : 1;
    }

    private static void Register()
    {
        RegisterLaunchArguments();
        RegisterExtended();
        Add("AR05 child environments isolate license bindings", () =>
        {
            var policy = new ProcessEnvironmentPolicy();
            var parent = new Dictionary<string, string>
                { ["AVEVA_LICENSE_SERVER_LIST"] = "parent", ["PATH"] = "parent-path" };
            var first = policy.Build(parent, new Dictionary<string, string> { ["ToolRoot"] = "one" }, "4545@first");
            var second = policy.Build(parent, new Dictionary<string, string>(), "4545@second");
            Equal("4545@first", Ok(first)["AVEVA_LICENSE_SERVER_LIST"]);
            Equal("4545@second", Ok(second)["AVEVA_LICENSE_SERVER_LIST"]);
            Equal("parent", parent["AVEVA_LICENSE_SERVER_LIST"]);
            Check(!policy.Build(parent, new Dictionary<string, string> { ["aveva_license_server_list"] = "bad" },
                "chosen").IsSuccess);
            return Task.CompletedTask;
        });
        Add("AR06-08 health rounds debounce and bind evidence identity", () =>
        {
            var descriptor = Resource();
            var policy = new ConnectionHealthPolicy();
            var now = DateTimeOffset.UtcNow;
            var state = ProbeSnapshot.Unknown(descriptor);
            for (var round = 1; round <= 3; round++)
                state = policy.Reduce(state, new ProbeRound(descriptor, round, [Observation(descriptor, false)]), now);
            Equal(ResourceState.Unreachable, state.State);
            Check(state.NotifyDisconnect);
            state = policy.Reduce(state, new ProbeRound(descriptor, 4, [Observation(descriptor, false)]), now);
            Check(!state.NotifyDisconnect);
            state = policy.Reduce(state, new ProbeRound(descriptor, 5, [Observation(descriptor, true)]), now);
            Equal(ResourceState.Reachable, state.State);
            Check(!state.IsFreshFor(descriptor with { Host = "other" }, now));
            Check(!state.IsFreshFor(descriptor, now.AddMinutes(1)));
            return Task.CompletedTask;
        });
        Add("F04 RequireFresh uses frozen endpoint and never reuses another revision", async () =>
        {
            await using var fixture = await Fixture.Create();
            var probe = new RecordingProbe();
            var supervisor = new ResourceSupervisor(probe, new ConnectionHealthPolicy(), fixture.Store);
            var first = Resource();
            var second = first with { Host = "different", ResourceRevision = new Revision(2) };
            Ok(await supervisor.RequireFreshAsync(first, default));
            Ok(await supervisor.RequireFreshAsync(second, default));
            Equal(2, probe.Resources.Count);
            Equal(second.IdentityKey, probe.Resources[1].IdentityKey);
        });
        Add("F05 stable copy identity, historical receipt and no credential copying", async () =>
        {
            await using var fixture = await Fixture.Create();
            var vault = new FakeVault();
            var catalog = fixture.Catalog(vault);
            var app = new ApplicationDefinition(Guid.NewGuid(), "Test", "1", EntryKind.Executable, "test.exe", "",
                fixture.Root, "", ArgumentTemplate: []);
            var revision = Ok(await catalog.SaveDefinitionAsync(CommitId.New(),
                new DefinitionDraft(DefinitionKind.Application, app), new Revision(0), default));
            var draft = new ProfileDraft(null, "Original", app.Id, "1", "project", "user", "/mdb", "design", null, []);
            using var change = SecretUpdate.Replace(new TransientSecret("fixture-secret"));
            var saved = Ok(await catalog.SaveProfileAsync(CommitId.New(), draft, change, revision, default));
            var copyId = CommitId.New();
            var copy = Ok(await catalog.CopyAsync(copyId, saved.Id, saved.Revision, default));
            var second = Ok(await catalog.CopyAsync(copyId, saved.Id, saved.Revision, default));
            Equal(copy.Id, second.Id);
            Equal(1, vault.Writes);
            Equal("未配置密码", copy.CredentialStatus);
            Ok(await catalog.DeleteAsync(CommitId.New(), copy.Id, copy.Revision, default));
            var originalReceipt = Ok(await catalog.InspectCommitAsync(copyId, default));
            Equal(copy.Id, originalReceipt.Profile!.Id);
            Equal(CommitState.Committed, originalReceipt.State);
        });
        Add("F05 switch outcome unknown remains verifiable by CommitId", async () =>
        {
            var fail = false;
            await using var fixture = await Fixture.Create(stage =>
            {
                if (fail && stage == "pointer-written") throw new IOException("fixture interrupted after switch");
            });
            var catalog = fixture.Catalog(new FakeVault());
            var commit = CommitId.New();
            fail = true;
            var app = new ApplicationDefinition(Guid.NewGuid(), "Test", "1", EntryKind.Executable, "x.exe", "",
                fixture.Root, "");
            var result = await catalog.SaveDefinitionAsync(commit, new DefinitionDraft(DefinitionKind.Application, app),
                new Revision(0), default);
            Check(!result.IsSuccess);
            Equal("CommitIndeterminate", result.Error!.Code);
            Equal(CommitState.Committed, Ok(await catalog.InspectCommitAsync(commit, default)).State);
        });
        Add("F01 two real processes admit at most one side effect", async () =>
        {
            await using var fixture = await Fixture.Create();
            var id = OperationId.New();
            var start = new Func<Process>(() =>
            {
                var info = new ProcessStartInfo
                {
                    FileName = Path.Combine(AppContext.BaseDirectory, "LaunchDesk.Rewrite.Tests.exe"),
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                foreach (var value in new[]
                             { "--journal-child", fixture.Root, id.ToString(), fixture.Location.MachineId.ToString() })
                    info.ArgumentList.Add(value);
                return Process.Start(info)!;
            });
            using var one = start();
            using var two = start();
            await Task.WhenAll(one.WaitForExitAsync(), two.WaitForExitAsync());
            Equal(0, one.ExitCode);
            Equal(0, two.ExitCode);
            Equal(1, File.ReadAllLines(Path.Combine(fixture.Root, "side-effects.txt")).Length);
            var snapshot = Frozen(id, fixture.Location.MachineId);
            var resumed = Ok(await new JsonRunJournal(fixture.Location).AcceptAsync(snapshot, default));
            Equal(AcceptanceDisposition.Existing, resumed.Disposition);
            Check(!(await new JsonRunJournal(fixture.Location).TryBeginExecutionAsync(resumed, default)).IsSuccess);
        });
        Add("F02 reject workspace, ancestor, traversal and held target conflicts", async () =>
        {
            await using var fixture = await Fixture.Create();
            Check(!(await fixture.Files.InspectTargetAsync(new TargetSpec(fixture.Location.Directory), default))
                .IsSuccess);
            Check(!(await fixture.Files.InspectTargetAsync(new TargetSpec(fixture.Root), default)).IsSuccess);
            var target =
                Ok(await fixture.Files.InspectTargetAsync(new TargetSpec(Path.Combine(fixture.Root, "target")),
                    default));
            await using var lease = Ok(await fixture.Leases.AcquireAsync([target.Root], default));
            var current =
                Ok(await fixture.Files.InspectTargetAsync(new TargetSpec(Path.Combine(fixture.Root, "target")),
                    default));
            Check(!(await fixture.Leases.AcquireAsync([current.Root], default)).IsSuccess);
        });
        Add("AR12 sources preserve a manual older package when refreshed", async () =>
        {
            await using var fixture = await Fixture.Create();
            var folder = Path.Combine(fixture.Root, "sources");
            Directory.CreateDirectory(folder);
            var old = Path.Combine(folder, "old.zip");
            var recent = Path.Combine(folder, "new.zip");
            File.WriteAllText(old, "one");
            File.WriteAllText(recent, "two");
            File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-1));
            var sourceCatalog = new ToolSourceCatalog(fixture.Reader);
            var retained = Ok(await sourceCatalog.RefreshAsync(new SourceQuery(folder), new SourceSelection(old),
                RefreshIntent.RefreshSources, default));
            Equal(old, retained.Selection!.Path);
            var newest = Ok(await sourceCatalog.RefreshAsync(new SourceQuery(folder), new SourceSelection(old),
                RefreshIntent.SelectLatest, default));
            Equal(recent, newest.Selection!.Path);
        });
        Add("AR13-17 folder delivery, interrupted write, explicit recovery and rollback", async () =>
        {
            var stop = false;
            await using var fixture = await Fixture.Create(null, stage =>
            {
                if (stop && stage == "target-written")
                {
                    stop = false;
                    throw new IOException("fixture crash");
                }
            });
            var source = Path.Combine(fixture.Root, "package");
            var target = Path.Combine(fixture.Root, "target");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(source, "one.txt"), "new");
            File.WriteAllText(Path.Combine(source, "two.txt"), "added");
            File.WriteAllText(Path.Combine(target, "one.txt"), "old");
            var inspected =
                Ok(await fixture.Reader.InspectAsync(new PackageSource(PackageKind.Folder, source), default));
            var plan = Ok(await fixture.Planner.PreviewAsync(
                new DeployRequest(new Revision(0), Guid.NewGuid(), inspected, new TargetSpec(target)), default));
            Equal("old", File.ReadAllText(Path.Combine(target, "one.txt")));
            var original = OperationId.New();
            stop = true;
            await using (var lease =
                         Ok(await fixture.Leases.AcquireAsync(
                             [new CanonicalPath(plan.TargetDirectory, plan.TargetIdentity, true, true)], default)))
            {
                Check(!(await fixture.Files.ApplyAsync(original, ConfirmedPlan.Confirm(plan), lease, default))
                    .IsSuccess);
            }

            var recovery =
                Ok(await fixture.Planner.PreviewRecoveryAsync(
                    new RecoveryRequest(new Revision(0), original, DeploymentAction.Recover), default));
            await using (var lease = Ok(await fixture.Leases.AcquireAsync(
                             [new CanonicalPath(recovery.TargetDirectory, recovery.TargetIdentity, true, true)],
                             default)))
            {
                Ok(await fixture.Files.ApplyAsync(OperationId.New(), ConfirmedPlan.Confirm(recovery), lease, default));
            }

            Equal("new", File.ReadAllText(Path.Combine(target, "one.txt")));
            Equal("added", File.ReadAllText(Path.Combine(target, "two.txt")));
            var rollback =
                Ok(await fixture.Planner.PreviewRecoveryAsync(
                    new RecoveryRequest(new Revision(0), original, DeploymentAction.Rollback), default));
            await using (var lease = Ok(await fixture.Leases.AcquireAsync(
                             [new CanonicalPath(rollback.TargetDirectory, rollback.TargetIdentity, true, true)],
                             default)))
            {
                Ok(await fixture.Files.ApplyAsync(OperationId.New(), ConfirmedPlan.Confirm(rollback), lease, default));
            }

            Equal("old", File.ReadAllText(Path.Combine(target, "one.txt")));
            Check(!File.Exists(Path.Combine(target, "two.txt")));
        });
        Add("AR14 changing source invalidates a confirmed plan", async () =>
        {
            await using var fixture = await Fixture.Create();
            var source = Path.Combine(fixture.Root, "source.txt");
            File.WriteAllText(source, "first");
            var inspected =
                Ok(await fixture.Reader.InspectAsync(new PackageSource(PackageKind.Files, source), default));
            var plan = Ok(await fixture.Planner.PreviewAsync(
                new DeployRequest(new Revision(0), Guid.NewGuid(), inspected,
                    new TargetSpec(Path.Combine(fixture.Root, "target"))), default));
            File.WriteAllText(source, "changed");
            Check(!(await fixture.Staging.ValidateAsync(plan, default)).IsSuccess);
        });
        Add("AR19 import has no execution and export contains no credential reference", async () =>
        {
            await using var fixture = await Fixture.Create();
            var catalog = fixture.Catalog(new FakeVault());
            var app = new ApplicationDefinition(Guid.NewGuid(), "Generic", "1", EntryKind.Executable, "sample.exe", "",
                fixture.Root, "");
            var rev = Ok(await catalog.SaveDefinitionAsync(CommitId.New(),
                new DefinitionDraft(DefinitionKind.Application, app), new Revision(0), default));
            var profile = Ok(await catalog.SaveProfileAsync(CommitId.New(),
                new ProfileDraft(null, "shared", app.Id, "1", "p", "u", "/m", "d", null, []),
                SecretUpdate.Replace(new TransientSecret("fixture-secret")), rev, default));
            var sharing = new ConfigurationShareService(fixture.Store, fixture.Location.MachineId);
            var bundle = Ok(await sharing.ExportAsync(new ShareSelection([profile.Id]), default));
            var json = JsonSerializer.Serialize(bundle);
            Check(!json.Contains("fixture-secret"));
            Check(!json.Contains("Credential", StringComparison.OrdinalIgnoreCase));
            var preview = Ok(await sharing.PreviewImportAsync(bundle, default));
            Check(!(await sharing.CommitAsync(CommitId.New(), preview, profile.Revision, default)).IsSuccess);
            Ok(await sharing.CommitAsync(CommitId.New(), preview with { LocalBindingsConfirmed = true },
                profile.Revision, default));
            Equal(2, Ok(await catalog.ReadAsync(default)).Profiles.Length);
            var imported = Ok(await catalog.ReadAsync(default)).Profiles.Single(p => p.Id != profile.Id);
            await using var coordinator = new OperationCoordinator(new JsonRunJournal(fixture.Location),
                new FixtureRegistry(new WaitingHandler()), fixture.Store, fixture.Location.MachineId);
            var launch = await coordinator.SubmitAsync(
                new OperationRequest(OperationId.New(), OperationKind.Launch, imported.Revision, imported.Id), default);
            Check(!launch.IsSuccess);
            Equal("LocalBindingRequired", launch.Error!.Code);
        });
        Add("AR21 portable location is relative to executable rather than current directory", () =>
        {
            var resolver = new WorkspaceLocationResolver();
            var root = Path.Combine(reportRoot, "portable");
            var location = Ok(resolver.Resolve(new StartupOptions(DataDirectory: "Data"), root));
            Equal(Path.Combine(root, "Data", "workspace.json"), location.ConfigurationFile);
            return Task.CompletedTask;
        });
        Add("Runtime secrets and capabilities reject JSON serialization", () =>
        {
            using var secret = new TransientSecret("not-for-json");
            var rejected = false;
            try
            {
                JsonSerializer.Serialize(secret);
            }
            catch (NotSupportedException)
            {
                rejected = true;
            }

            Check(rejected);
            return Task.CompletedTask;
        });
    }

    private static void Add(string name, Func<Task> run)
    {
        Tests.Add((name, run));
    }

    private static T Ok<T>(Result<T> result)
    {
        return result.IsSuccess
            ? result.Value
            : throw new Exception(result.Error!.Code + ": " + result.Error.SafeMessage);
    }

    private static void Check(bool condition)
    {
        if (!condition) throw new Exception("Assertion failed.");
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Expected {expected}; actual {actual}.");
    }

    private static ResourceDescriptor Resource()
    {
        return new ResourceDescriptor(Guid.NewGuid(), new Revision(1), Guid.NewGuid(), "localhost", 4545, false, 4546,
            null, null, true,
            new ProbePolicy(AttemptsPerRound: 1, RetryDelayMilliseconds: 0));
    }

    private static ProbeObservation Observation(ResourceDescriptor resource, bool ready)
    {
        return new ProbeObservation(resource, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            [new ProbeEvidence(EvidenceKind.Tcp, resource.Host, ready, ready ? "Ready" : "Timeout", "fixture")]);
    }

    private static OperationRequestSnapshot Frozen(OperationId id, Guid machine)
    {
        return new OperationRequestSnapshot(new OperationRequest(id, OperationKind.Launch, new Revision(0)), machine,
            null, "fixture-plan", DateTimeOffset.UtcNow);
    }

    private static async Task<int> JournalChild(string[] args)
    {
        var root = args[1];
        var location = new WorkspaceLocation(Path.Combine(root, "workspace"),
            Path.Combine(root, "workspace", "workspace.json"), Path.Combine(root, "workspace", ".control"),
            Guid.Parse(args[3]));
        var journal = new JsonRunJournal(location);
        var accepted =
            Ok(await journal.AcceptAsync(Frozen(new OperationId(Guid.Parse(args[2])), location.MachineId), default));
        if (accepted.Disposition == AcceptanceDisposition.Existing) return 0;
        await using var lease = Ok(await journal.TryBeginExecutionAsync(accepted, default));
        File.AppendAllText(Path.Combine(root, "side-effects.txt"), "once\n");
        return 0;
    }

    private sealed class FakeVault : ISecretVault
    {
        public int Writes;

        public ValueTask<Result<CredentialRef>> StoreNewAsync(TransientSecret secret, CancellationToken ct)
        {
            Writes++;
            return ValueTask.FromResult(Result<CredentialRef>.Ok(new CredentialRef("fixture-reference")));
        }

        public ValueTask<Result<SecretLease>> ResolveAsync(CredentialRef reference, CancellationToken ct)
        {
            return ValueTask.FromResult(Result<SecretLease>.Ok(new SecretLease(new TransientSecret("fixture-secret"))));
        }

        public ValueTask<Result<Unit>> DeleteAsync(CredentialRef reference, CancellationToken ct)
        {
            return ValueTask.FromResult(Result<Unit>.Ok(new Unit()));
        }
    }

    private sealed class EmptyDiscovery : IEntryDiscovery
    {
        public ValueTask<Result<IReadOnlyList<string>>> FindAsync(Guid id, CancellationToken ct)
        {
            return ValueTask.FromResult(Result<IReadOnlyList<string>>.Ok([]));
        }
    }

    private sealed class RecordingProbe : IResourceProbe
    {
        public List<ResourceDescriptor> Resources { get; } = [];

        public ValueTask<Result<ProbeObservation>> ObserveAsync(ResourceDescriptor resource, CancellationToken ct)
        {
            Resources.Add(resource);
            return ValueTask.FromResult(Result<ProbeObservation>.Ok(Observation(resource, true)));
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public string Root { get; private init; } = "";
        public WorkspaceLocation Location { get; private init; } = null!;
        public JsonWorkspaceStore Store { get; private init; } = null!;
        public WindowsControlDataGuard Guard { get; private init; } = null!;
        public CanonicalTargetLeaseProvider Leases { get; private init; } = null!;
        public PackageReader Reader { get; private init; } = null!;
        public PrivateStagingStore Staging { get; private init; } = null!;
        public FileDeliveryPort Files { get; private init; } = null!;
        public DeploymentPlanner Planner { get; private init; } = null!;

        public WorkspaceCatalogService Catalog(ISecretVault vault)
        {
            return new WorkspaceCatalogService(Store, vault, new EmptyDiscovery(), Location.MachineId);
        }

        public static async Task<Fixture> Create(Action<string>? configFault = null,
            Action<string>? deliveryFault = null)
        {
            var root = Path.Combine(reportRoot, "fixtures", Guid.NewGuid().ToString("N"));
            var directory = Path.Combine(root, "workspace");
            Directory.CreateDirectory(directory);
            var location = new WorkspaceLocation(directory, Path.Combine(directory, "workspace.json"),
                Path.Combine(directory, ".control"), Guid.NewGuid());
            var guard = new WindowsControlDataGuard(Path.Combine(root, "registry"));
            Ok(guard.RegisterWorkspace(location));
            var store = new JsonWorkspaceStore(location, configFault);
            Ok(await store.InitializeEmptyAsync(default));
            var reader = new PackageReader(Path.Combine(location.ControlRoot, "inspection"),
                Path.Combine(AppContext.BaseDirectory, "Tools", "7zr.exe"));
            var staging = new PrivateStagingStore(location, reader);
            var files = new FileDeliveryPort(location, staging, guard, deliveryFault);
            return new Fixture
            {
                Root = root, Location = location, Guard = guard, Store = store, Reader = reader, Staging = staging,
                Files = files, Leases = new CanonicalTargetLeaseProvider(guard),
                Planner = new DeploymentPlanner(reader, staging, files, guard)
            };
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
