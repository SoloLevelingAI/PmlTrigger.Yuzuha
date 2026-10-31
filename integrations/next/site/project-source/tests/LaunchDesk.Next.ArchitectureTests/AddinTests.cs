using LaunchDesk.Next.Bootstrap.Addins;
using NUnit.Framework;
namespace LaunchDesk.Next.ArchitectureTests;

public sealed class AddinTests
{
    private static string Folder()
    {
        var path=Path.Combine(TestContext.CurrentContext.WorkDirectory,"addins-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);return path;
    }
    [Test] public void MissingManifestDoesNotComposeEvenIfFeatureFactoryExists()
    {
        var catalog=AddinCatalog.Load(Folder());int calls=0;
        Assert.That(catalog.CreateIfEnabled("login.launch",()=>{calls++;return new object();}),Is.Null);
        Assert.That(calls,Is.Zero);
        Assert.Throws<AddinDisabledException>(()=>catalog.Require("login.launch"));
    }
    [TestCase("true",true)]
    [TestCase("false",false)]
    public void EnabledFlagControlsComposition(string flag,bool enabled)
    {
        var path=Folder();File.WriteAllText(Path.Combine(path,"login.addin.xml"),$"<Addin id=\"login.launch\" enabled=\"{flag}\" contractVersion=\"1\" />");
        var catalog=AddinCatalog.Load(path);var feature=new object();
        Assert.That(catalog.IsEnabled("login.launch"),Is.EqualTo(enabled));
        Assert.That(catalog.CreateIfEnabled("login.launch",()=>feature),enabled?Is.SameAs(feature):Is.Null);
        Assert.That(catalog.IsEnabled("development.remote-project-lab"),Is.False);
    }
    [TestCase("<Addin id='login.launch' enabled='maybe' contractVersion='1'/>")]
    [TestCase("<Addin id='login.launch' enabled='true' contractVersion='2'/>")]
    [TestCase("<Addin id='login.launch' enabled='true' contractVersion='1' assembly='anything.dll'/>")]
    [TestCase("<Addin id='login.launch' enabled='true' contractVersion='1'><Run/></Addin>")]
    [TestCase("<Addin id='../escape' enabled='true' contractVersion='1'/>")]
    [TestCase("<!DOCTYPE Addin [<!ENTITY x SYSTEM 'file:///C:/missing'>]><Addin id='login.launch' enabled='true' contractVersion='1'>&x;</Addin>")]
    [TestCase("<broken>")]
    public void InvalidDeclarationsFailClosed(string xml)
    {
        var path=Folder();File.WriteAllText(Path.Combine(path,"bad.addin.xml"),xml);
        Assert.Throws<InvalidDataException>(()=>AddinCatalog.Load(path));
    }
    [Test] public void DuplicateFeatureIdsAreNotResolvedByFileOrder()
    {
        var path=Folder();var xml="<Addin id='login.launch' enabled='true' contractVersion='1'/>";
        File.WriteAllText(Path.Combine(path,"one.addin.xml"),xml);File.WriteAllText(Path.Combine(path,"two.addin.xml"),xml);
        Assert.Throws<InvalidDataException>(()=>AddinCatalog.Load(path));
    }
    [Test] public void DisabledFilenameDoesNotRegisterAFeature()
    {
        var path=Folder();File.WriteAllText(Path.Combine(path,"login.addin.xml.disabled"),"<Addin id='login.launch' enabled='true' contractVersion='1'/>");
        Assert.That(AddinCatalog.Load(path).EnabledIds,Is.Empty);
    }
}
