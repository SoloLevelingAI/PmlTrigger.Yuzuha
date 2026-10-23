using System.Text.Json;
using YuzuhaToolkit.Workspace;
using Xunit;

namespace YuzuhaToolkit.Tests;

public sealed class LaunchDraftTests : IDisposable
{
    private readonly string folder=Path.Combine(Path.GetTempPath(),"YuzuhaLaunchDraft-"+Guid.NewGuid().ToString("N"));
    public LaunchDraftTests()=>Directory.CreateDirectory(folder);
    [Fact]
    public void SelectingProductFillsItsInitializationAndClearsStaleInitializationForBatch()
    {
        File.WriteAllText(Path.Combine(folder,"launch.init"),"fixture");
        var monitor=LaunchDrafts.FromApplication(Path.Combine(folder,"mon.exe"));
        var marine=LaunchDrafts.FromApplication(Path.Combine(folder,"marine.bat"));
        Assert.Equal(Path.Combine(folder,"launch.init"),monitor.Initialization); Assert.Equal("Monitor",monitor.Kind);
        Assert.Equal("",marine.Initialization); Assert.Equal("MarineBatch",marine.Kind); Assert.Equal("Outfitting",marine.Module);
    }
    [Fact]
    public void AnonymousDraftNameUsesProjectModuleAndProductDirectory()
    {
        var name=LaunchDrafts.SuggestName(new LaunchProfile { Project="DEMO",Module="Design",Executable=Path.Combine(folder,"mon.exe") });
        Assert.Equal("DEMO · Design · "+Path.GetFileName(folder),name);
    }
    [Fact]
    public void LaunchDeskImportKeepsLoginFieldsButNeverCopiesCredentialsOrToolEnvironment()
    {
        var file=Write(); var result=Assert.Single(LaunchDrafts.ReadLaunchDesk(file));
        Assert.Equal(Path.Combine(folder,"mon.exe"),result.Executable); Assert.Equal("TEST",result.Project);
        Assert.Equal("DESIGNER",result.User); Assert.Equal("/ALL",result.Mdb); Assert.Equal("Paragon",result.Module);
        Assert.Equal("",result.CredentialKey); Assert.Empty(result.Environment); Assert.Equal("",result.License);
        Assert.NotEqual("original-profile",result.Id);
    }
    [Fact]
    public void MissingApplicationReferenceIsRejectedInsteadOfGuessingAnotherProduct()
    {
        var file=Write(); File.WriteAllText(file,File.ReadAllText(file).Replace("\"app-one\"","\"bad-app\"",StringComparison.Ordinal).Replace("\"Id\":\"bad-app\"","\"Id\":\"app-one\""));
        Assert.Throws<InvalidDataException>(()=>LaunchDrafts.ReadLaunchDesk(file));
    }
    [Fact]
    public void ArbitraryJsonAndUnsupportedEntryCannotBecomeALaunchDraft()
    {
        var file=Path.Combine(folder,"bad.json"); File.WriteAllText(file,"{}");
        Assert.Throws<InvalidDataException>(()=>LaunchDrafts.ReadLaunchDesk(file));
        file=Write(); File.WriteAllText(file,File.ReadAllText(file).Replace("Monitor","ShellCommand"));
        Assert.Throws<InvalidDataException>(()=>LaunchDrafts.ReadLaunchDesk(file));
    }
    private string Write()
    {
        var file=Path.Combine(folder,"workspace.json");
        var payload=new { Applications=new[]{new { Id="app-one",Kind="Monitor",ExecutablePath=Path.Combine(folder,"mon.exe"),InitializationPath=Path.Combine(folder,"launch.init"),WorkingDirectory=folder,ProductCode="E3D" }},
            Profiles=new[]{new { Id="original-profile",Name="Existing",AppId="app-one",Project="TEST",Username="DESIGNER",Mdb="/ALL",Module="Paragon",CredentialKey="must-not-copy",ToolIds=new[]{"another-tool"},Password="must-not-copy" }},
            Tools=new[]{new { Id="another-tool",EnvironmentVariables=new { SECRET="must-not-copy" }}} };
        File.WriteAllText(file,JsonSerializer.Serialize(payload)); return file;
    }
    public void Dispose(){ if(Directory.Exists(folder))Directory.Delete(folder,true); }
}
