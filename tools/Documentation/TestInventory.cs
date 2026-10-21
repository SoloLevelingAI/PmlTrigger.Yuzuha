// Adapted from SoloLevelingAI/LaunchDesk tools/documentation/TestInventory.cs (main).
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Discover real xUnit declarations and source spans using LaunchDesk's Roslyn inventory.</summary>
internal static class TestInventory
{
    internal static void Write(string repository, string destination)
    {
        repository = Path.GetFullPath(repository);
        var entries = new List<object>();
        foreach (var project in Files(repository).Where(p => p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).Order())
        {
            var xml = XDocument.Load(project);
            if (!xml.Descendants().Any(e => e.Name.LocalName == "PackageReference" &&
                (string?)e.Attribute("Include") is "xunit" or "xunit.v3")) continue;
            var directory = Path.GetDirectoryName(project)!;
            var files = Files(directory).Where(p => p.EndsWith(".cs")).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var compile in xml.Descendants().Where(e => e.Name.LocalName == "Compile"))
            {
                if (compile.Attribute("Include") is { } include)
                {
                    if (include.Value.IndexOfAny(['*', '$']) >= 0) throw new InvalidDataException("Unsupported test Compile input: " + include.Value);
                    files.Add(Path.GetFullPath(Path.Combine(directory, include.Value.Replace('\\', '/'))));
                }
                if (compile.Attribute("Remove") is { } remove) files.Remove(Path.GetFullPath(Path.Combine(directory, remove.Value.Replace('\\', '/'))));
            }
            foreach (var file in files.Order())
            {
                var source = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest), file);
                if (tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
                    throw new InvalidDataException("Cannot index invalid test source: " + file);
                var syntax = tree.GetRoot();
                var aliases = syntax.DescendantNodes().OfType<UsingDirectiveSyntax>().Where(u => u.Alias is not null)
                    .ToDictionary(u => u.Alias!.Name.Identifier.ValueText, u => u.Name!.ToString());
                foreach (var method in syntax.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    var attributes = method.AttributeLists.SelectMany(l => l.Attributes).Select(a => a.Name.ToString()).Select(n => aliases.GetValueOrDefault(n, n).Split('.').Last()).ToArray();
                    if (!attributes.Any(n => n is "Fact" or "FactAttribute" or "Theory" or "TheoryAttribute")) continue;
                    var owner = string.Join(".", method.Ancestors().OfType<TypeDeclarationSyntax>().Reverse().Select(t => t.Identifier.ValueText));
                    var space = string.Join(".", method.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString()));
                    var qualified = string.Join(".", new[] { space, owner, method.Identifier.ValueText }.Where(n => n.Length > 0));
                    var span = tree.GetLineSpan(method.Span);
                    entries.Add(new { project = Relative(directory), projectFile = Relative(project), file = Relative(file), kind = "xunit",
                        name = method.Identifier.ValueText, method = qualified, line = span.StartLinePosition.Line + 1,
                        endLine = span.EndLinePosition.Line + 1, snippet = method.ToString(),
                        sourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant() });
                }
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        File.WriteAllText(destination, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Indexed {entries.Count} xUnit method declarations (Theory rows counted at runtime).");
        string Relative(string path) => Path.GetRelativePath(repository, path).Replace('\\', '/');
    }

    private static IEnumerable<string> Files(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory)) yield return file;
        foreach (var child in Directory.EnumerateDirectories(directory).Where(p => Path.GetFileName(p) is not (".git" or "bin" or "obj" or "node_modules" or "artifacts" or "runtime")))
            foreach (var file in Files(child)) yield return file;
    }
}
