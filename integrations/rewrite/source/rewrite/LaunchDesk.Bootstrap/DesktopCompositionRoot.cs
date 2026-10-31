using LaunchDesk.UseCases;
using LaunchDesk.Windows;

namespace LaunchDesk.Bootstrap;

/// <summary>根据有限操作种类创建独立处理器租约。</summary>
/// <param name="factories">按有限操作类别登记的处理器工厂。</param>
public sealed class OperationHandlerRegistry(IReadOnlyDictionary<OperationKind, Func<IOperationHandler>> factories)
    : IOperationHandlerRegistry
{
    /// <inheritdoc />
    public Result<HandlerLease> Create(OperationKind kind)
    {
        return factories.TryGetValue(kind, out var factory)
            ? Result<HandlerLease>.Ok(new HandlerLease(factory()))
            : Result<HandlerLease>.Fail("UnsupportedOperationKind", "未注册该操作处理器。");
    }
}

/// <summary>装配同一 Desktop 运行时、保护登记及操作和直接提交协议。</summary>
public sealed class DesktopCompositionRoot : IAsyncDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly List<Task> observers = [];
    /// <summary>当前独立工作空间的位置。</summary>
    public WorkspaceLocation Location { get; }
    /// <summary>组合根拥有的配置存储；页面不直接访问该适配器。</summary>
    public JsonWorkspaceStore Store { get; }
    /// <summary>页面使用的公开配置目录端口。</summary>
    public ICatalogService Catalog { get; }
    /// <summary>已装配的桌面外壳页面模型。</summary>
    public ShellViewModel Shell { get; }
    /// <summary>组合根拥有的持久操作协调器。</summary>
    public OperationCoordinator Operations { get; }
    /// <summary>工作空间中各个许可连接的独立投影。</summary>
    public ResourceSupervisor Resources { get; }
    private readonly ITaskQueries queries;
    private readonly IResourceMonitor resourceEvents;
    /// <summary>仅记录接口、实现和次数的可选诊断。</summary>
    public PortDiagnostics? Diagnostics { get; }

    /// <summary>解析工作空间并装配桌面用例、平台端口、页面模型和后台观察任务。</summary>
    /// <include file="../Comments.xml" path="doc/Options/*"/>
    /// <param name="executableDirectory">EXE目录，用于解析便携相对路径。</param>
    /// <include file="../Comments.xml" path="doc/Dialogs/*"/>
    /// <param name="registryDirectory">保护根注册及互斥的独立目录。</param>
    /// <param name="diagnostics">只记录名称与计数的可选端口诊断。</param>
    /// <param name="adapters">隔离验证用的可选外部副作用替身。</param>
    public DesktopCompositionRoot(StartupOptions options, string executableDirectory,
        IPresentationDialogs? dialogs = null, string? registryDirectory = null,
        PortDiagnostics? diagnostics = null, DesktopAdapters? adapters = null)
    {
        Diagnostics = diagnostics;
        T Bind<T>(T target) where T : class => diagnostics?.Bind(target) ?? target;
        var resolver = Bind<IWorkspaceLocationResolver>(new WorkspaceLocationResolver());
        var resolved = resolver.Resolve(options, executableDirectory);
        if (!resolved.IsSuccess) throw new InvalidDataException(resolved.Error!.SafeMessage);
        Location = resolved.Value;
        var guard = Bind<IControlDataGuard>(new WindowsControlDataGuard(registryDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LaunchDesk", "ControlV3")));
        var registered = guard.RegisterWorkspace(Location);
        if (!registered.IsSuccess) throw new InvalidDataException(registered.Error!.SafeMessage);
        Store = new JsonWorkspaceStore(Location);
        var workspace = Bind<IWorkspaceStore>(Store);
        var vault = Bind<ISecretVault>(adapters?.Secrets ?? new WindowsSecretVault());
        Catalog = Bind<ICatalogService>(new WorkspaceCatalogService(workspace, vault,
            Bind<IEntryDiscovery>(new WindowsEntryDiscovery(workspace)), Location.MachineId));
        Resources = new ResourceSupervisor(Bind<IResourceProbe>(adapters?.Probe ?? new WindowsResourceProbe(Location.MachineId)),
            new ConnectionHealthPolicy(), workspace);
        resourceEvents = Bind<IResourceMonitor>(Resources);
        var journal = Bind<IRunJournal>(new JsonRunJournal(Location));
        queries = Bind<ITaskQueries>(new OperationProjectionService(journal));
        var reader = Bind<IPackageReader>(new PackageReader(Path.Combine(Location.ControlRoot, "inspection"),
            Path.Combine(executableDirectory, "Tools", "7zr.exe")));
        var staging = Bind<IStagingStore>(new PrivateStagingStore(Location, reader));
        var delivery = new FileDeliveryPort(Location, staging, guard);
        var files = Bind<IFileDeliveryPort>(delivery);
        var variables = Bind<IEnvironmentStore>(adapters?.Environment ?? new WindowsEnvironmentStore(registryDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LaunchDesk", "ControlV3")));
        var privilege = Bind<IPrivilegeBroker>(adapters?.Privilege ?? new WindowsPrivilegeBroker(Path.Combine(executableDirectory, "LaunchDesk.Worker.exe")));
        var migration = Bind<IMigrationStore>(new JsonMigrationStore(Location, Store, guard));
        var leases = Bind<ITargetLeaseProvider>(new CanonicalTargetLeaseProvider(guard));
        var processes = Bind<IProcessGateway>(adapters?.Processes ?? new WindowsProcessGateway());
        var registry = Bind<IOperationHandlerRegistry>(new OperationHandlerRegistry(new Dictionary<OperationKind, Func<IOperationHandler>>
        {
            [OperationKind.Launch] = () =>
                Bind<IOperationHandler>(new LaunchOperationHandler(new LaunchPlanBuilder(new ProcessEnvironmentPolicy()), resourceEvents, vault,
                    processes)),
            [OperationKind.Deploy] = () => Bind<IOperationHandler>(new DeliveryOperationHandler(staging, files,
                leases, privilege, Location.ControlRoot)),
            [OperationKind.ApplyVariables] =
                () => Bind<IOperationHandler>(new VariableOperationHandler(variables, privilege, Location.ControlRoot)),
            [OperationKind.MigrateConfiguration] = () => Bind<IOperationHandler>(new WorkspaceMigrationService(migration))
        }));
        Operations = new OperationCoordinator(journal, registry, workspace, Location.MachineId,
            context => Bind<IOperationContext>(context));
        var commands = Bind<IOperationCommands>(Operations);
        var state = new WorkspacePresentationState(Environment.MachineName, Location);
        var presentation = dialogs ?? new PresentationDialogs();
        var sharing = Bind<IConfigurationShareService>(new ConfigurationShareService(workspace, Location.MachineId));
        var documents = Bind<IConfigurationDocuments>(new ConfigurationDocuments());
        Shell = new ShellViewModel(state, new ProfilesPageViewModel(Catalog, commands, state),
            new ProfileEditorViewModel(Catalog, new PasswordEntryBehavior(), state),
            new ApplicationsPageViewModel(Catalog, state, presentation),
            new LicensePageViewModel(Catalog, resourceEvents, state, presentation),
            new ToolPageViewModel(Catalog, Bind<IToolSourceCatalog>(new ToolSourceCatalog(reader)), commands,
                Bind<IDeploymentPlanningService>(new DeploymentPlanner(reader, staging, files, guard)),
                Bind<IVariablePlanningService>(new VariablePlanner(variables)),
                Bind<IDeploymentHistoryQueries>(new DeploymentHistoryQueries(delivery, workspace)), state, presentation, sharing, documents),
            new SharingPageViewModel(sharing, documents, state, Catalog, presentation),
            new SettingsPageViewModel(state, resolver, Bind<IMigrationPlanningService>(new MigrationPlanningService(Location, migration)), commands, Catalog,
                presentation, Bind<IMigrationQueries>(new MigrationQueries(journal, migration))), Catalog, commands, queries, presentation);
    }

    /// <summary>在装配完成后初始化独立工作空间并按选项启动观察订阅。</summary>
    /// <param name="dispatcher">拥有页面模型的 WPF Dispatcher。</param>
    /// <param name="observe">是否启动任务与资源投影订阅；隔离文档与元数据检查可关闭。</param>
    /// <returns>异步完成任务。</returns>
    public async Task InitializeAsync(Dispatcher dispatcher, bool observe = true)
    {
        var initialized = await Store.InitializeEmptyAsync(default);
        if (!initialized.IsSuccess) throw new InvalidDataException(initialized.Error!.SafeMessage);
        var current = await Catalog.ReadAsync(default);
        if (!current.IsSuccess) throw new InvalidDataException(current.Error!.SafeMessage);
        Shell.Workspace.Update(current.Value);
        Shell.Workspace.Message = "工作空间已就绪。新增应用与登录，或从设置迁移旧版配置。";
        await Shell.Tools.RefreshHistoryAsync();
        if (!observe) return;
        observers.Add(Task.Run(async () =>
        {
            try
            {
                await foreach (var run in queries.WatchAsync(lifetime.Token))
                    await dispatcher.InvokeAsync(async () =>
                    {
                        Shell.Workspace.UpdateRun(run);
                        if (run.Kind == OperationKind.MigrateConfiguration && run.Stage == RunStage.Completed)
                        {
                            var snapshot = await Catalog.ReadAsync(lifetime.Token);
                            if (snapshot.IsSuccess) Shell.Workspace.Update(snapshot.Value);
                        }

                        if (run.Kind == OperationKind.Deploy &&
                            run.Stage is RunStage.Completed or RunStage.Failed or RunStage.NeedsVerification)
                            await Shell.Tools.RefreshHistoryAsync();
                    }).Task.Unwrap();
            }
            catch (OperationCanceledException)
            {
            }
        }));
        observers.Add(Task.Run(async () =>
        {
            try
            {
                await foreach (var probe in resourceEvents.WatchAsync(lifetime.Token))
                    await dispatcher.InvokeAsync(() => Shell.Workspace.UpdateProbe(probe));
            }
            catch (OperationCanceledException)
            {
            }
        }));
        observers.Add(Task.Run(async () =>
        {
            try
            {
                await Resources.PollAsync(lifetime.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }));
    }

    /// <summary>停止本桌面运行时的观察和操作协调任务，清理页面临时秘密及自有取消令牌。</summary>
    /// <returns>表示自有后台任务和临时资源结束的任务。</returns>
    public async ValueTask DisposeAsync()
    {
        await lifetime.CancelAsync();
        await Operations.DisposeAsync();
        await Task.WhenAll(observers);
        Shell.ProfileEditor.Password.Dispose();
        lifetime.Dispose();
    }
}
