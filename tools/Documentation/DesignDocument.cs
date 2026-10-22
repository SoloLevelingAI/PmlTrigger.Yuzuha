using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>Read-only Word source extraction for versioned design references; never executes document content.</summary>
internal static class DesignDocument
{
    internal static void Write(string source, string destination)
    {
        using var zip = ZipFile.OpenRead(source);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XDocument Xml(string name)
        {
            var entry = zip.GetEntry(name) ?? throw new InvalidDataException("Missing Word part: " + name);
            if (entry.Length > 10 * 1024 * 1024) throw new InvalidDataException("Oversized Word XML: " + name);
            using var stream = entry.Open();
            return XDocument.Load(stream);
        }
        var document = Xml("word/document.xml");
        var body = document.Root?.Element(w + "body") ?? throw new InvalidDataException("Missing Word body");
        var styles = Xml("word/styles.xml").Descendants(w + "style")
            .Where(s => s.Element(w + "pPr")?.Element(w + "outlineLvl") is not null)
            .ToDictionary(s => (string)s.Attribute(w + "styleId")!, s => (int)s.Element(w + "pPr")!.Element(w + "outlineLvl")!.Attribute(w + "val")!);
        var relationships = Xml("word/_rels/document.xml.rels").Root!.Elements()
            .Where(e => (string?)e.Attribute("TargetMode") != "External")
            .ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);
        string Text(XElement element) => string.Concat(element.Descendants().Select(e =>
            e.Name == w + "t" ? e.Value : e.Name == w + "tab" ? "\t" : e.Name == w + "br" || e.Name == w + "cr" ? "\n" : ""));
        var images = new HashSet<string>();
        string[] Images(XElement element) => element.Descendants(a + "blip").Select(e => (string?)e.Attribute(r + "embed"))
            .Where(id => id is not null).Select(id =>
            {
                if (!relationships.TryGetValue(id!, out var target) || !Regex.IsMatch(target, @"^media/[A-Za-z0-9_.-]+\.(png|jpg|jpeg|gif)$", RegexOptions.IgnoreCase))
                    throw new InvalidDataException("Unsupported embedded image relationship: " + id);
                images.Add(target);
                return target;
            }).ToArray();
        var blocks = body.Elements().Select((element, index) =>
        {
            if (element.Name == w + "p")
            {
                var style = (string?)element.Element(w + "pPr")?.Element(w + "pStyle")?.Attribute(w + "val");
                return (object)new { index, kind = "paragraph", text = Text(element), heading = style is not null && styles.TryGetValue(style, out var level) ? (int?)level : null, images = Images(element) };
            }
            if (element.Name == w + "tbl") return new
            {
                index, kind = "table", rows = element.Elements(w + "tr").Select(row => row.Elements(w + "tc")
                    .Select(cell => string.Join("\n", cell.Elements(w + "p").Select(Text))).ToArray()).ToArray(), images = Images(element)
            };
            return null;
        }).Where(block => block is not null).ToArray();
        var directory = Path.GetDirectoryName(Path.GetFullPath(destination))!;
        Directory.CreateDirectory(directory);
        foreach (var name in images)
        {
            var entry = zip.GetEntry("word/" + name) ?? throw new InvalidDataException("Missing embedded image: " + name);
            if (entry.Length > 20 * 1024 * 1024) throw new InvalidDataException("Oversized image: " + name);
            var file = Path.Combine(directory, name.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            using var input = entry.Open();
            using var output = File.Create(file);
            input.CopyTo(output);
        }
        File.WriteAllText(destination, JsonSerializer.Serialize(new
        {
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant(),
            paragraphs = body.Descendants(w + "p").Count(), images = images.Count, blocks
        }));
    }
}
