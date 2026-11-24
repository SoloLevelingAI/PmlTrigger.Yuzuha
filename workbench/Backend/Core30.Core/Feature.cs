using System.Reflection;
using System.Xml.Linq;

namespace Core30.Core;

public interface IFeature
{
    string Id { get; }
    string Title { get; }
    string Version { get; }
    string ActivityCategory => "runtime";
    string? GetOperationName(XElement request) => null;
    Stream OpenSchema();
    XElement Execute(FeatureContext context, XElement request);
}

public sealed record Feature(
    string Id,
    string Title,
    Assembly SchemaAssembly,
    Func<FeatureContext, XElement, XElement> Handler,
    string Version = "v1") : IFeature
{
    public string ActivityCategory { get; init; } = "runtime";
    public Func<XElement, string>? OperationName { get; init; }

    private string SchemaName => Version == "v1" ? $"{Id}.xsd" : $"{Id}.{Version}.xsd";

    public Stream OpenSchema() => EmbeddedFile.Open(SchemaAssembly, SchemaName);
    public string? GetOperationName(XElement request) => OperationName?.Invoke(request);

    public XElement Execute(FeatureContext context, XElement request) => Handler(context, request);
}

/// <summary>A built-in feature with an in-memory contract and an XML adapter for external callers.</summary>
public sealed record TypedFeature(
    string Id,
    string Title,
    Assembly SchemaAssembly,
    Func<FeatureContext, CoreData, CoreData> Handler,
    string Version = "v1") : IFeature
{
    public string ActivityCategory { get; init; } = "runtime";
    public Func<CoreData, string>? OperationName { get; init; }

    public Stream OpenSchema() => EmbeddedFile.Open(SchemaAssembly, Version == "v1" ? $"{Id}.xsd" : $"{Id}.{Version}.xsd");
    public string? GetOperationName(XElement request) => OperationName?.Invoke(CoreXml.Read(request));
    public CoreData Execute(FeatureContext context, CoreData request) => Handler(context, request);
    public XElement Execute(FeatureContext context, XElement request) => CoreXml.Write(Execute(context, CoreXml.Read(request)));
}

internal static class EmbeddedFile
{
    internal static Stream Open(Assembly assembly, string fileName)
    {
        var resource = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith("." + fileName, StringComparison.Ordinal));
        return resource is null
            ? throw new InvalidDataException($"找不到内置契约或配置 {fileName}。")
            : assembly.GetManifestResourceStream(resource)!;
    }
}
