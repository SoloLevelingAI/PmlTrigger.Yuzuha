namespace Core30.Core;

internal sealed record RunSettings(string Log, string Source)
{
    private const string Contract = "urn:core30:settings:v1";

    internal static RunSettings Load(FeatureContext context)
    {
        var (stream, source) = SelectSource(context);
        using var input = stream;
        using var schema = OpenDefault("settings.xsd");
        var document = XmlContracts.Read(input);
        XmlContracts.Validate(document, schema, Contract, "settings");
        return new RunSettings(document.Root!.Attribute("log")!.Value, source);
    }

    private static (Stream Stream, string Source) SelectSource(FeatureContext context)
    {
        const string userFile = ".core30/settings.xml";
        var path = context.FilePath(userFile);
        return File.Exists(path) ? (File.OpenRead(path), userFile)
            : (OpenDefault("settings.xml"), "embedded-default");
    }

    private static Stream OpenDefault(string name) => EmbeddedFile.Open(typeof(RunSettings).Assembly, name);
}
