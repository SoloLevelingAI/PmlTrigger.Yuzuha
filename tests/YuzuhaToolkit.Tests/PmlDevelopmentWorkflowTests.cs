using System.Diagnostics;
using System.Text.Json;
using YuzuhaToolkit.Workspace;
using Xunit;

namespace YuzuhaToolkit.Tests;

public sealed class PmlDevelopmentWorkflowTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "YuzuhaDesktopFlow-" + Guid.NewGuid().ToString("N"));
    private static readonly DevelopmentTarget Target = new(123, 456, "Design");
    private readonly string source;
    private readonly string root;
    private readonly string manifest;

    public PmlDevelopmentWorkflowTests()
    {
        source = Path.Combine(folder,"source"); root = Path.Combine(folder,"PMLLIB"); manifest = Path.Combine(folder,"manifest.json");
        Directory.CreateDirectory(source); Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(source,"Probe.pmlobj"),"object-v2");
        File.WriteAllText(Path.Combine(source,"ProbeForm.pmlfrm"),"form-v2");
        File.WriteAllText(Path.Combine(source,"validate.pmlmac"),"validation");
        File.WriteAllText(manifest,"""
        {"schemaVersion":1,"pluginName":"ProbePlugin","objects":["Probe"],"forms":["ProbeForm"],
         "validationMacro":"validate.pmlmac","resultGlobal":"ProbeResult","expected":{"Revision":"v2"}}
        """);
    }
    private DevelopmentPreview Preview() => PmlDevelopmentWorkflow.Preview(PluginDeployment.Preview(source,root,"ProbePlugin"),manifest);
    private Task<DevelopmentRun> Run(FakeHost host) => PmlDevelopmentWorkflow.RunAsync(Preview(),Target,Path.Combine(folder,"evidence"),host);

    [Fact]
    public async Task CopyRehashReloadAndActualResultAreOneOrderedRun()
    {
        var host = new FakeHost(root);
        var result = await Run(host);
        Assert.Equal("Passed",result.Status);
        Assert.Equal(new[]{"query","PML REHASH ALL","pml reload object Probe","pml reload form !!ProbeForm","validation"},host.Commands);
        Assert.Equal("object-v2",File.ReadAllText(Path.Combine(root,"ProbePlugin","Probe.pmlobj")));
        Assert.All(result.Steps,s=>Assert.Equal("Passed",s.Status));
        Assert.Contains("verify-results",File.ReadAllText(Path.Combine(folder,"evidence","run.json")));
    }
    [Fact]
    public async Task WrongSessionSearchRootStopsBeforeCopyAndRehash()
    {
        var host = new FakeHost(Path.Combine(folder,"another"));
        var result = await Run(host);
        Assert.Equal("FailedOrUnknown",result.Status); Assert.Single(host.Commands);
        Assert.False(Directory.Exists(Path.Combine(root,"ProbePlugin")));
    }
    [Fact]
    public async Task UnknownRehashStopsWithoutAutomaticRetryOrReload()
    {
        var host = new FakeHost(root) { FailCommand="PML REHASH ALL" };
        var result = await Run(host);
        Assert.Equal("FailedOrUnknown",result.Status);
        Assert.Equal(new[]{"query","PML REHASH ALL"},host.Commands); Assert.Equal(1,host.Reads);
        Assert.NotNull(result.Deployment);
    }
    [Theory]
    [InlineData("wrong",0)]
    [InlineData("v2",1)]
    public async Task CommandSuccessDoesNotHideWrongOrUnparsedBusinessResult(string value,int unparsed)
    {
        var result = await Run(new FakeHost(root) { Result=value, Unparsed=unparsed });
        Assert.Equal("FailedOrUnknown",result.Status);
    }
    [Fact]
    public async Task ChangedPreviewStopsBeforeAnyDispatch()
    {
        var preview = Preview(); var host = new FakeHost(root);
        File.AppendAllText(Path.Combine(source,"Probe.pmlobj"),"changed");
        await Assert.ThrowsAsync<IOException>(()=>PmlDevelopmentWorkflow.RunAsync(preview,Target,Path.Combine(folder,"evidence"),host));
        Assert.Empty(host.Commands);
    }
    [Fact]
    public async Task ChangedSessionStopsBeforeAnyCommand()
    {
        var result = await Run(new FakeHost(root) { ChangedIdentity=true });
        Assert.Equal("FailedOrUnknown",result.Status); Assert.False(Directory.Exists(Path.Combine(root,"ProbePlugin")));
    }
    [Fact]
    public void InvalidManifestIsRejectedDuringPreview()
    {
        File.WriteAllText(manifest,File.ReadAllText(manifest).Replace("\"validate.pmlmac\"","null"));
        Assert.Throws<ArgumentException>(()=>Preview());
    }
    [Fact]
    public void StartupExplicitNoneOverridesEnvironmentAndClearsStaleCorrelation()
    {
        var start = new ProcessStartInfo(); start.Environment[StartupPolicy.Variable]="rehash-all";
        start.Environment[StartupPolicy.OverrideVariable]="none"; start.Environment["YUZUHA_STARTUP_ID"]="stale";
        StartupPolicy.Prepare(start); Assert.False(start.Environment.ContainsKey("YUZUHA_STARTUP_ID"));
    }
    [Fact]
    public void SpecialStartupGetsIndependentCorrelationAndUnknownPolicyIsRejected()
    {
        var one = new ProcessStartInfo(); one.Environment[StartupPolicy.Variable]="rehash-all"; one.Environment.Remove(StartupPolicy.OverrideVariable);
        var two = new ProcessStartInfo(); two.Environment[StartupPolicy.Variable]="rehash-all"; two.Environment.Remove(StartupPolicy.OverrideVariable);
        StartupPolicy.Prepare(one); StartupPolicy.Prepare(two);
        Assert.NotEqual(one.Environment["YUZUHA_STARTUP_ID"],two.Environment["YUZUHA_STARTUP_ID"]);
        Assert.EndsWith(".log",one.Environment["YUZUHA_STARTUP_LOG"]);
        one.Environment[StartupPolicy.Variable]="arbitrary-command"; Assert.Throws<ArgumentException>(()=>StartupPolicy.Prepare(one));
    }

    private sealed class FakeHost(string root) : IPmlDevelopmentHost
    {
        public List<string> Commands { get; } = [];
        public string? FailCommand { get; init; }
        public string Result { get; init; } = "v2";
        public int Unparsed { get; init; }
        public int Reads { get; private set; }
        public bool ChangedIdentity { get; init; }
        private static DevelopmentReply Reply(bool ok=true) => new(ok,JsonSerializer.SerializeToElement(new { Success=ok }),ok?null:"unknown",!ok);
        public Task<DevelopmentTarget> VerifyAsync(CancellationToken token) => Task.FromResult(ChangedIdentity?Target with { StartTicks=999 }:Target);
        public Task<DevelopmentReply> ExecuteAsync(string command,CancellationToken token)
        {
            Commands.Add(command.StartsWith("$M ") ? Commands.Count==0?"query":"validation" : command);
            return Task.FromResult(Reply(command!=FailCommand));
        }
        public Task<DevelopmentRows> ReadAsync(string global,CancellationToken token)
        {
            Reads++;
            return Task.FromResult(global.StartsWith("YzDevPath") ? new DevelopmentRows(Reply(),0,[new("PmlLib",root)])
                : new DevelopmentRows(Reply(),Unparsed,[new("Revision",Result)]));
        }
    }
    public void Dispose() { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
}
