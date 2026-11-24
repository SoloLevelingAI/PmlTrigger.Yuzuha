using System.Xml;
using System.Xml.Linq;

namespace Core30.Core;

/// <summary>Edits the XML string list consumed at startup by AVEVA modules.</summary>
public static class AvevaAddinManifest
{
    public static IReadOnlyList<string> ReadEntries(string path)
    {
        var (scope, file) = Select(path);
        return Entries(LoadConfigurationChange.Read(scope.FilePath(file)), out _)
            .Select(entry => entry.Value.Trim()).ToArray();
    }

    public static LoadConfigurationChange Preview(string path, string name, bool load)
    {
        var (scope, file) = Select(path);
        name = name.Trim();
        if (name.Length == 0 || name.Length > 260 || name.Any(char.IsControl))
            throw new InvalidDataException("请选择清单中的 Addin 名称，或输入程序集发布方提供的名称。");
        FileContent.XmlText(name);
        var original = LoadConfigurationChange.Read(scope.FilePath(file));
        var entries = Entries(original, out var document);
        var matches = entries.Where(entry => string.Equals(entry.Value.Trim(), name, StringComparison.Ordinal)).ToArray();
        if (matches.Length > 1)
            throw new InvalidDataException("清单中存在多条同名 Addin，请先确定要处理的记录。");
        var loaded = matches.Length != 0;
        var proposed = loaded == load ? original : Edit(original, document, matches.SingleOrDefault(), name);
        if (loaded != load)
            Entries(proposed, out _);
        return new(LoadConfigurationKind.AvevaAddin, name, loaded, load, scope, file, original, proposed);
    }

    public static void Restore(string path, LoadConfigurationRevision revision)
    {
        if (revision.BeforeSha256 is null)
            throw new InvalidDataException("Addins 回执须记录写入前的清单摘要。");
        var (scope, file) = Select(path);
        LoadConfigurationChange.Restore(scope, file, revision);
    }

    private static (FeatureContext Scope, string File) Select(string path)
    {
        var full = LocalSource.RequireAbsolutePath(path);
        var file = Path.GetFileName(full);
        if (!file.EndsWith("Addins.xml", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("请选择 AVEVA 的 *Addins.xml 清单。");
        return (LocalSource.OpenParent(full), file);
    }

    private static XElement[] Entries(byte[] bytes, out XDocument document)
    {
        document = LoadConfigurationChange.Parse(bytes, preserveWhitespace: true);
        var root = document.Root;
        if (root?.Name != "ArrayOfString" || root.Elements().Any(entry => entry.Name != "string" || entry.HasElements))
            throw new InvalidDataException("当前 AVEVA 清单应使用 ArrayOfString 和 string 项。");
        return root.Elements("string").ToArray();
    }

    private static byte[] Edit(byte[] original, XDocument document, XElement? match, string name)
    {
        var text = LocalText.Decode(original);
        var newLine = text.Content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        if (match is null)
        {
            var tail = document.Root!.LastNode;
            var indent = document.Root.Elements().LastOrDefault()?.PreviousNode as XText;
            var spacing = indent is { Value: var whitespace } && string.IsNullOrWhiteSpace(whitespace)
                ? whitespace : newLine + "  ";
            if (tail is XText ending && string.IsNullOrWhiteSpace(ending.Value))
                tail.AddBeforeSelf(new XText(spacing), new XElement("string", name));
            else
                document.Root.Add(new XText(spacing), new XElement("string", name), new XText(newLine));
        }
        else
        {
            if (match.PreviousNode is XText whitespace && string.IsNullOrWhiteSpace(whitespace.Value))
                whitespace.Remove();
            match.Remove();
        }

        using var buffer = new StringWriter();
        using (var writer = XmlWriter.Create(buffer, new XmlWriterSettings
        {
            OmitXmlDeclaration = true, Indent = false, NewLineHandling = NewLineHandling.Replace, NewLineChars = newLine
        }))
            document.WriteTo(writer);
        var declaration = OriginalDeclaration(text.Content);
        return text.Encode(original, declaration + buffer);
    }

    private static string OriginalDeclaration(string text)
    {
        if (!text.StartsWith("<?xml ", StringComparison.Ordinal))
            return "";
        var end = text.IndexOf("?>", StringComparison.Ordinal);
        return text[..(end + 2)];
    }
}
