using System.Xml;
using System.Xml.Linq;
namespace LaunchDesk.Next.Bootstrap.Addins;

// The manifest controls host registration; assembly presence never activates a feature.
public sealed class AddinCatalog
{
    private readonly HashSet<string> enabled;
    private AddinCatalog(HashSet<string> enabled) => this.enabled=enabled;
    public IReadOnlyList<string> EnabledIds => enabled.Order(StringComparer.Ordinal).ToArray();
    public bool IsEnabled(string id) => enabled.Contains(id);
    public T? CreateIfEnabled<T>(string id, Func<T> factory) where T : class =>
        IsEnabled(id) ? factory() : null;
    public void Require(string id)
    {
        if(!IsEnabled(id)) throw new AddinDisabledException(id);
    }
    public static AddinCatalog LoadDefault() => Load(Path.Combine(AppContext.BaseDirectory,"Addins"));
    public static AddinCatalog Load(string directory)
    {
        var ids=new HashSet<string>(StringComparer.Ordinal);
        var active=new HashSet<string>(StringComparer.Ordinal);
        if(!Directory.Exists(directory))return new(active);
        foreach(var path in Directory.EnumerateFiles(directory,"*.addin.xml").Order(StringComparer.Ordinal))
        {
            try
            {
                using var reader=XmlReader.Create(path,new XmlReaderSettings
                {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536});
                var root=XDocument.Load(reader).Root;
                if(root is null || root.Name!="Addin" || root.HasElements || !string.IsNullOrWhiteSpace(root.Value) ||
                    root.Attributes().Any(a=>a.Name!= "id" && a.Name!="enabled" && a.Name!="contractVersion"))
                    throw new InvalidDataException("Expected an Addin declaration with id, enabled and contractVersion only.");
                var id=(string?)root.Attribute("id");
                if(string.IsNullOrWhiteSpace(id) || id.Length>100 || id.Any(c=>!char.IsAsciiLetterOrDigit(c) && c is not '.' and not '-'))
                    throw new InvalidDataException("Invalid feature id.");
                if((string?)root.Attribute("contractVersion")!="1" || !bool.TryParse((string?)root.Attribute("enabled"),out var on))
                    throw new InvalidDataException("Unsupported contractVersion or invalid enabled value.");
                if(!ids.Add(id))throw new InvalidDataException("Duplicate feature id: "+id);
                if(on)active.Add(id);
            }
            catch(Exception e) when(e is XmlException or InvalidDataException)
            { throw new InvalidDataException("Addin could not be loaded: "+Path.GetFileName(path)+" — "+e.Message,e); }
        }
        return new(active);
    }
}

public sealed class AddinDisabledException(string id) : InvalidOperationException("Feature is not registered by an enabled Addin: "+id);
