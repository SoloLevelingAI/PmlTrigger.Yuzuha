using System.Text.Json;
using Xunit;
using YuzuhaToolkit.Mcp;

namespace YuzuhaToolkit.Tests;

public sealed class FunctionRegistryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "yuzuha-registry-" + Guid.NewGuid().ToString("N"));
    private string Source(string text = "define function !!Sample(!text is STRING, !number is REAL) is STRING\nreturn !text\nendfunction")
    {
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "Sample.pmlfnc");File.WriteAllText(file,text);return file;
    }
    private PmlFunctionRegistry Registry() => new(Path.Combine(root,"functions.json"));

    [Fact]
    public void RegistrationParsesSourceAndStartsDeniedAcrossStoreInstances()
    {
        var entry = Registry().Register(Source(),"Sample");
        Assert.Equal(new[]{"STRING","REAL"},entry.ParameterTypes);
        Assert.False(entry.AllowInvoke);
        Assert.False(Assert.Single(Registry().List()).AllowInvoke);
        Assert.Throws<UnauthorizedAccessException>(()=>Registry().PrepareCall("Sample",Arguments()));
    }
    [Fact]
    public void ExplicitPermissionProducesTypedEscapedCallAndCanBeRevoked()
    {
        var store=Registry();store.Register(Source(),"Sample");store.SetPermission("sample",true);
        Assert.Equal("!!Sample('O''Brien',1.5)",store.PrepareCall("SAMPLE",Arguments()));
        store.SetPermission("Sample",false);
        Assert.Throws<UnauthorizedAccessException>(()=>store.PrepareCall("Sample",Arguments()));
    }
    [Fact]
    public void ChangedSourceRequiresRegistrationAndFreshPermission()
    {
        var source=Source();var store=Registry();store.Register(source,"Sample");store.SetPermission("Sample",true);
        File.AppendAllText(source,"\n-- changed");
        Assert.Throws<InvalidDataException>(()=>store.PrepareCall("Sample",Arguments()));
        Assert.Throws<InvalidDataException>(()=>store.SetPermission("Sample",true));
        Assert.False(store.Register(source,"Sample").AllowInvoke);
    }
    [Theory]
    [InlineData("Sample\nDELETE WORLD")]
    [InlineData("!!Sample()")]
    [InlineData("Sample;Other")]
    public void InvalidNamesNeverRegister(string name) => Assert.Throws<ArgumentException>(()=>Registry().Register(Source(),name));

    [Fact]
    public void MissingAndCommentOnlyDeclarationsAreRejected()
    {
        Assert.Throws<InvalidDataException>(()=>Registry().Register(Source("-- define function !!Sample()"),"Sample"));
        Assert.Throws<InvalidDataException>(()=>Registry().Register(Source(),"Other"));
    }
    [Fact]
    public void SignatureMismatchAndMalformedPersistenceFailClosed()
    {
        var store=Registry();store.Register(Source(),"Sample");store.SetPermission("Sample",true);
        Assert.Throws<ArgumentException>(()=>store.PrepareCall("Sample",[]));
        Assert.Throws<ArgumentException>(()=>store.PrepareCall("Sample",[new(){Type="bool",Value=true},new(){Type="real",Value=1}]));
        File.WriteAllText(Path.Combine(root,"functions.json"),"{broken");
        Assert.Throws<JsonException>(()=>store.List());
    }
    private static List<DynamicParameterInput> Arguments()=>[new(){Type="string",Value="O'Brien"},new(){Type="real",Value=1.5}];
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
