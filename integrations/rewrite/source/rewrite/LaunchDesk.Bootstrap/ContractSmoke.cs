using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;

namespace LaunchDesk.Bootstrap;

/// <summary>在隔离工作空间经WPF按钮对等体验证生产装配。</summary>
internal static class ContractSmoke
{
    internal static async Task RunAsync(string destination)
    {
        var output = Path.GetFullPath(destination);
        Directory.CreateDirectory(output);
        var diag = new PortDiagnostics();
        var dialogs = new FixtureDialogs();
        var environment = new MemoryEnvironment();
        var gateway = new FixtureProcesses();
        var checks = new List<object>();
        var source = Path.Combine(output,"packages");
        var files = Path.Combine(output,"files");
        Directory.CreateDirectory(source); Directory.CreateDirectory(files);
        File.WriteAllText(Path.Combine(files,"test.pmlfnc"),"fixture-v1");
        var zip = Path.Combine(source,"tool-v1.zip"); ZipFile.CreateFromDirectory(files,zip);
        var target = Path.Combine(output,"delivered"); Directory.CreateDirectory(target);
        await using var root = new DesktopCompositionRoot(new StartupOptions(DataDirectory:Path.Combine(output,"workspace")),
            AppContext.BaseDirectory, dialogs, Path.Combine(output,"registry"),diag,
            new DesktopAdapters(new FixtureVault(),new FixtureProbe(),gateway,environment,new NoElevation()));
        await root.InitializeAsync(Application.Current.Dispatcher);
        var vm = root.Shell;
        var window = new MainWindow(vm);
        Application.Current.MainWindow = window; window.Show();

        async Task Click(string label, Func<bool> observed, params string[] methods)
        {
            await window.Dispatcher.InvokeAsync(() => {},DispatcherPriority.ContextIdle);
            window.UpdateLayout();
            var before = methods.ToDictionary(m=>m,diag.Count);
            var button = Descendants(window).OfType<Button>().FirstOrDefault(b=>b.IsVisible && b.Content?.ToString()==label)
                ?? throw new InvalidOperationException("Missing visible button: "+vm.Page+" / "+label);
            if (!button.IsEnabled) throw new InvalidOperationException("Disabled button: "+label);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            await Until(()=>observed() && methods.All(m=>diag.Count(m)>before[m]), label+" / "+vm.Workspace.Message);
            checks.Add(new { page=vm.Page.ToString(),button=label,expected=methods,calls=methods.ToDictionary(m=>m,m=>diag.Count(m)-before[m]),outcome="passed" });
            File.WriteAllText(Path.Combine(output,"progress.json"),JsonSerializer.Serialize(new {checks,diagnostics=diag.Snapshot()},new JsonSerializerOptions{WriteIndented=true}));
        }

        vm.Page=PageId.Applications;
        await Click("手动添加",()=>vm.Applications.Draft.Id==Guid.Empty);
        var exe=Path.Combine(output,"mon.exe"); File.WriteAllText(exe,"test fixture, never executed");
        var init=Path.Combine(output,"launch.init"); File.WriteAllText(init,"fixture");
        var a=vm.Applications.Draft; a.Name="Fixture E3D"; a.Version="E3D 3.1"; a.Kind=EntryKind.Monitor;
        a.Executable=exe; a.Initialization=init; a.WorkingDirectory=output; a.Roots=output;
        await Click("保存应用",()=>vm.Workspace.Snapshot.Applications.Length==1,"ICatalogService.SaveDefinitionAsync","IWorkspaceStore.CommitAsync");
        await Click("自动查找",()=>!vm.Applications.DiscoverCommand.IsRunning,"ICatalogService.RefreshDiscoveryAsync","IEntryDiscovery.FindAsync");
        Check(vm.Applications.Selected!.ExecutablePath==exe,"Discovery replaced manual binding.");

        vm.Page=PageId.Licenses;
        await Click("新增许可",()=>vm.Licenses.Draft.Id==Guid.Empty);
        vm.Licenses.Draft.Name="Fixture license"; vm.Licenses.Draft.Host="fixture.invalid"; vm.Licenses.Draft.RequireRules=false;
        await Click("保存连接",()=>vm.Workspace.Snapshot.LicenseEndpoints.Length==1,"ICatalogService.SaveDefinitionAsync");
        await Click("检测选中连接",()=>vm.Licenses.Selected?.Probe?.State==ResourceState.Reachable,"IResourceMonitor.RefreshAsync","IResourceProbe.ObserveAsync");
        var resourceRow=vm.Workspace.Resources[0]; var currentProbe=resourceRow.Probe!;
        vm.Workspace.UpdateProbe(currentProbe with {RoundSequence=currentProbe.RoundSequence+2});
        Check(!vm.Workspace.UpdateProbe(currentProbe with {RoundSequence=currentProbe.RoundSequence+1,State=ResourceState.Unreachable,NotifyDisconnect=true}) && resourceRow.Probe!.State==ResourceState.Reachable,"Late probe overwrote newer connectivity evidence.");

        vm.Page=PageId.Dashboard;
        await Click("新增登录",()=>vm.Page==PageId.Profiles);
        vm.ProfileEditor.Name="Fixture MAR Design"; vm.ProfileEditor.Group="E3D 3.1";
        vm.ProfileEditor.Project="mar"; vm.ProfileEditor.User="SYSTEM";
        vm.ProfileEditor.License=vm.Workspace.Snapshot.LicenseEndpoints[0];
        vm.ProfileEditor.Password.AttachInput("fixture-secret-only".AsSpan());
        await Click("保存配置",()=>vm.Workspace.Snapshot.Profiles.Length==1,"ICatalogService.SaveProfileAsync","ISecretVault.StoreNewAsync");
        Check(!vm.ProfileEditor.Password.HasInput,"Saved password was left in the editor.");
        vm.Page=PageId.Dashboard;
        await Click("复制配置",()=>vm.Workspace.Snapshot.Profiles.Length==2,"ICatalogService.CopyAsync");
        Check(vm.Profiles.Selected!.Profile.CredentialStatus=="未配置密码","Copy retained credential association.");
        vm.Page=PageId.Profiles;
        await Click("删除",()=>vm.Workspace.Snapshot.Profiles.Length==1,"ICatalogService.DeleteAsync");
        vm.Page=PageId.Dashboard;
        await Click("启动选中配置",()=>gateway.Starts==1,"IOperationCommands.SubmitAsync","IResourceMonitor.RequireFreshAsync","ISecretVault.ResolveAsync","IProcessGateway.StartAsync","IOperationHandler.ExecuteAsync");
        await Until(()=>vm.Workspace.Runs.Any(r=>r.Snapshot.Stage==RunStage.WindowObserved),"Window observation");
        var latestRun=vm.Workspace.Runs[0].Snapshot;
        vm.Workspace.UpdateRun(latestRun with {Sequence=latestRun.Sequence-1,Stage=RunStage.Checking});
        Check(vm.Workspace.Runs[0].Snapshot.Stage==RunStage.WindowObserved,"Late operation event regressed the UI state.");
        await Click("详情",()=>vm.IsTaskDetailsOpen,"ITaskQueries.ReadAsync");
        Check(vm.Workspace.Runs[0].Snapshot.Stage!=RunStage.SessionConfirmed,"Window was misreported as login success.");
        await Click("停止观察",()=>vm.Workspace.Runs[0].Snapshot.ReasonCode=="ObservationStopped","IOperationCommands.CancelAsync","IRunJournal.RequestCancellationAsync");
        await Click("关闭记录",()=>!vm.IsTaskDetailsOpen);

        vm.Page=PageId.Tools;
        await Click("添加工具",()=>vm.Page==PageId.ToolSettings);
        vm.Tools.Draft.Name="Fixture PML"; vm.Tools.Draft.Group="Tests"; vm.Tools.Draft.Target=target;
        vm.Tools.Draft.Kind=PackageKind.Zip; vm.Tools.Draft.Source=zip; vm.Tools.Draft.SearchDirectory=source;
        await Click("保存工具",()=>vm.Workspace.Snapshot.Tools.Length==1,"ICatalogService.SaveDefinitionAsync");
        var toolId=vm.Tools.Draft.Id;
        dialogs.SavePath=Path.Combine(output,"tool-export.json");
        await Click("导出配置",()=>File.Exists(dialogs.SavePath),"IConfigurationShareService.ExportAsync","IConfigurationDocuments.WriteShareAsync");
        using(var exported=JsonDocument.Parse(File.ReadAllText(dialogs.SavePath)))
            Check(exported.RootElement.GetProperty("Tools").GetArrayLength()==1,"Standalone tool export was empty.");
        await Click("部署更新",()=>vm.Page==PageId.Deployment);
        await Click("刷新来源",()=>vm.Tools.Sources.Count==1,"IToolSourceCatalog.RefreshAsync","IPackageReader.DiscoverAsync");
        Check(vm.Tools.Draft.Source==zip,"Refresh replaced chosen source.");
        await Click("选择最新",()=>vm.Tools.SelectedSource?.Path==zip,"IToolSourceCatalog.RefreshAsync");
        await Click("预览变更",()=>vm.Tools.Plan is not null,"IToolSourceCatalog.InspectAsync","IPackageReader.InspectAsync","IDeploymentPlanningService.PreviewAsync","IStagingStore.StageAsync","IFileDeliveryPort.InspectTargetAsync");
        Check(!File.Exists(Path.Combine(target,"test.pmlfnc")),"Preview mutated delivery target.");
        await Click("确认执行",()=>File.Exists(Path.Combine(target,"test.pmlfnc")),"IOperationCommands.SubmitAsync","IFileDeliveryPort.ApplyAsync","ITargetLeaseProvider.AcquireAsync");
        await Until(()=>vm.Tools.History.Any(r=>r.Receipt.State=="Completed"),"Deployment receipt");
        Check(File.ReadAllText(Path.Combine(target,"test.pmlfnc"))=="fixture-v1","Delivered content differs.");
        vm.Tools.SelectedReceipt=vm.Tools.History.First(r=>r.Receipt.State=="Completed");
        await Click("预览回滚",()=>vm.Tools.Plan?.Action==DeploymentAction.Rollback,"IDeploymentPlanningService.PreviewRecoveryAsync","IFileDeliveryPort.ReadReceiptAsync","IStagingStore.ReadPlanAsync");
        await Click("确认执行",()=>!File.Exists(Path.Combine(target,"test.pmlfnc")),"IFileDeliveryPort.ApplyAsync");
        await Until(()=>vm.Tools.History.Any(r=>r.Receipt.State=="RolledBack"),"Rollback receipt");

        vm.Tools.Selected=vm.Workspace.Snapshot.Tools.First(t=>t.Id==toolId); vm.Page=PageId.Deployment;
        await Click("管理普通文件",()=>vm.Tools.IsEditingFiles);
        dialogs.Files=[Path.Combine(files,"test.pmlfnc")];
        await Click("添加文件",()=>vm.Tools.Draft.Files.Count==1);
        vm.Tools.Draft.Files[0].RelativePath="scripts/test.pmlfnc";
        await Click("完成文件映射",()=>!vm.Tools.IsEditingFiles);
        await Click("预览变更",()=>vm.Tools.Plan?.Files.Any(f=>f.RelativePath==Path.Combine("scripts","test.pmlfnc"))==true,"IDeploymentPlanningService.PreviewAsync");
        await Click("确认执行",()=>File.Exists(Path.Combine(target,"scripts","test.pmlfnc")),"IFileDeliveryPort.ApplyAsync");

        vm.Tools.Selected=vm.Workspace.Snapshot.Tools.First(t=>t.Id==toolId); vm.Page=PageId.ToolSettings;
        await Click("添加变量",()=>vm.Tools.IsEditingVariable);
        vm.Tools.OldName="FixtureOld"; vm.Tools.NewName="FixtureNew"; vm.Tools.VariableValue="{Target}"; vm.Tools.VariableScope=VariableScope.User;
        await Click("生成变量预览",()=>vm.Tools.VariablePlan is not null,"IVariablePlanningService.PreviewAsync","IEnvironmentStore.ReadAsync");
        Check(environment.Values.Count==0,"Variable preview mutated environment.");
        await Click("确认应用变量",()=>environment.Values.ContainsKey("FixtureNew"),"IEnvironmentStore.ApplyVerifiedAsync","IOperationCommands.SubmitAsync");
        Check(environment.Values["FixtureNew"]==target,"Variable expansion differs.");
        await Click("关闭",()=>!vm.Tools.IsEditingVariable);

        vm.Page=PageId.Sharing; dialogs.SavePath=Path.Combine(output,"selected-export.json");
        vm.Sharing.ClearSelectionCommand.Execute(null); vm.Sharing.ProfileChoices[0].Selected=true;
        await Click("导出选中配置",()=>File.Exists(dialogs.SavePath),"IConfigurationShareService.ExportAsync","IConfigurationDocuments.WriteShareAsync");
        var shareText=File.ReadAllText(dialogs.SavePath); Check(!shareText.Contains("fixture-secret-only"),"Secret exported.");
        dialogs.OpenPath=dialogs.SavePath;
        await Click("选择并预览导入",()=>vm.Sharing.Preview.Contains("应用 1"),"IConfigurationDocuments.ReadShareAsync","IConfigurationShareService.PreviewImportAsync");
        Check(vm.Workspace.Snapshot.Profiles.Length==1,"Import preview mutated workspace.");
        vm.Sharing.Confirmed=true;
        await Click("确认导入配置",()=>vm.Workspace.Snapshot.Profiles.Length==2,"IConfigurationShareService.CommitAsync","IWorkspaceStore.CommitAsync");
        await Click("核验原导入",()=>vm.Workspace.Message.Contains("原导入已确认"),"IConfigurationShareService.InspectCommitAsync");

        // 仅迁移合成旧文件；真实旧源文件逐字节保留。
        var legacy=Path.Combine(output,"legacy.json"); var legacyId=Guid.NewGuid();
        File.WriteAllText(legacy,JsonSerializer.Serialize(new {SchemaVersion=2,Applications=new[]{new {Id=legacyId,Name="Legacy fixture",Version="E3D 2.1",Kind=0,ExecutablePath=exe,InitializationPath=init,WorkingDirectory=output,ProductCode="E3D"}},Profiles=Array.Empty<object>(),Licenses=Array.Empty<object>(),Tools=Array.Empty<object>()}));
        var original=File.ReadAllBytes(legacy);
        window.Close();
        await using var migrationRoot = new DesktopCompositionRoot(new StartupOptions(DataDirectory:Path.Combine(output,"migration-workspace")),
            AppContext.BaseDirectory,dialogs,Path.Combine(output,"registry"),diag,
            new DesktopAdapters(new FixtureVault(),new FixtureProbe(),gateway,environment,new NoElevation()));
        await migrationRoot.InitializeAsync(Application.Current.Dispatcher);
        vm=migrationRoot.Shell; vm.Settings.SourcePath=legacy; vm.Page=PageId.Settings;
        window=new MainWindow(vm); Application.Current.MainWindow=window; window.Show();
        await Click("生成迁移预览",()=>vm.Settings.Preview.Contains("应用 1"),"IMigrationPlanningService.PreviewAsync","IMigrationStore.ReadSourceAsync");
        await Click("确认迁移",()=>vm.Workspace.Snapshot.Applications.Any(app=>app.Id==legacyId),"IMigrationStore.PrepareAsync","IMigrationStore.ValidateAsync","IMigrationStore.ActivateAsync");
        await Click("核验原迁移结果",()=>vm.Workspace.Message.Contains("稳定身份"),"IMigrationQueries.InspectAsync","IMigrationStore.InspectAsync");
        Check(original.SequenceEqual(File.ReadAllBytes(legacy)),"Legacy source was changed.");

        // 核验默认生产绑定，不调用外部副作用或观察器。
        var productionBindings=new PortDiagnostics();
        await using var productionRoot=new DesktopCompositionRoot(new StartupOptions(DataDirectory:Path.Combine(output,"production-composition")),
            AppContext.BaseDirectory,dialogs,Path.Combine(output,"registry"),productionBindings);
        await productionRoot.InitializeAsync(Application.Current.Dispatcher,false);
        File.WriteAllText(Path.Combine(output,"report.json"),JsonSerializer.Serialize(new {checks,diagnostics=diag.Snapshot(),production=productionBindings.Snapshot(),effects=new {realConfigStore=true,realJournal=true,realPackageAndDelivery=true,realMigration=true,externalAdapters="in-memory vault, probe, process, environment; elevation disabled"}},new JsonSerializerOptions{WriteIndented=true}));
        window.Close();
    }
    private static void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
    private static async Task Until(Func<bool> condition,string step)
    {
        var timer=Stopwatch.StartNew();
        while(!condition()) { if(timer.Elapsed>TimeSpan.FromSeconds(15)) throw new InvalidOperationException("Timed out: "+step); await Task.Delay(30); }
        await Application.Current.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) { var child=VisualTreeHelper.GetChild(parent,i); yield return child; foreach(var nested in Descendants(child)) yield return nested; }
    }
    private sealed class FixtureDialogs : IPresentationDialogs
    {
        public string? SavePath,OpenPath; public string LastTitle=""; public string[] Files=[];
        public string? OpenFile(string title,string filter="所有文件|*.*")=>OpenPath;
        public string[] OpenFiles(string title)=>Files;
        public string? SaveFile(string title,string suggestedName)=>SavePath;
        public string? OpenFolder(string title)=>null;
        public bool Confirm(string message,string title) { LastTitle=title; return true; }
    }
    private sealed class FixtureVault : ISecretVault
    {
        public ValueTask<Result<CredentialRef>> StoreNewAsync(TransientSecret secret,CancellationToken ct)=>ValueTask.FromResult(Result<CredentialRef>.Ok(new("fixture")));
        public ValueTask<Result<SecretLease>> ResolveAsync(CredentialRef reference,CancellationToken ct)=>ValueTask.FromResult(Result<SecretLease>.Ok(new(new TransientSecret("fixture-secret-only"))));
        public ValueTask<Result<Unit>> DeleteAsync(CredentialRef reference,CancellationToken ct)=>ValueTask.FromResult(Result<Unit>.Fail("Disabled","Not invoked by this suite."));
    }
    private sealed class FixtureProbe : IResourceProbe
    {
        public ValueTask<Result<ProbeObservation>> ObserveAsync(ResourceDescriptor resource,CancellationToken ct)=>ValueTask.FromResult(Result<ProbeObservation>.Ok(new(resource,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,[new(EvidenceKind.Tcp,"fixture",true,"Ready","Fixture connectivity")] )));
    }
    private sealed class FixtureProcesses : IProcessGateway
    {
        public int Starts;
        public Result<EnvironmentSnapshot> CaptureEnvironment()=>Result<EnvironmentSnapshot>.Ok(new(ImmutableDictionary<string,string>.Empty));
        public ValueTask<Result<TrackedProcess>> StartAsync(LaunchPlan plan,SecretLease? secret,CancellationToken ct) { Starts++; return ValueTask.FromResult(Result<TrackedProcess>.Ok(new(1,new object(),()=>ValueTask.CompletedTask))); }
        public async IAsyncEnumerable<ProcessEvidence> ObserveAsync(TrackedProcess process,[EnumeratorCancellation]CancellationToken ct)
        { await Task.Yield(); yield return new(EvidenceKind.WindowObserved,DateTimeOffset.UtcNow,1,"WindowObserved","Fixture window, no independent project login evidence"); await Task.Delay(Timeout.Infinite,ct); }
    }
    private sealed class MemoryEnvironment : IEnvironmentStore
    {
        public Dictionary<string,string?> Values {get;}=new();
        public ValueTask<Result<VariableSnapshot>> ReadAsync(VariableAddress address,CancellationToken ct)=>ValueTask.FromResult(Result<VariableSnapshot>.Ok(new(address,Values.GetValueOrDefault(address.Name))));
        public ValueTask<Result<Unit>> ApplyVerifiedAsync(ConfirmedVariablePlan confirmed,IOperationContext context,CancellationToken ct) { Values.Remove(confirmed.Plan.Before.Address.Name); Values[confirmed.Plan.NewName]=confirmed.Plan.NewValue; return ValueTask.FromResult(Result<Unit>.Ok(new())); }
    }
    private sealed class NoElevation : IPrivilegeBroker
    {
        public ValueTask<Result<WorkerReceipt>> ExecuteAsync(PrivilegedRequest request,CancellationToken ct)=>ValueTask.FromResult(Result<WorkerReceipt>.Fail("Disabled","Elevation disabled in UI fixtures."));
    }
}
