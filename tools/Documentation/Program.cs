using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length == 3 && args[0] == "--read-docx")
{
    DesignDocument.Write(args[1], args[2]);
    return 0;
}

if (args.Length == 4 && args[0] == "--ui-trace")
{
    UiTrace.Write(args[1], args[2], args[3]);
    return 0;
}

if (args.Length == 3 && args[0] == "--tests")
{
    TestInventory.Write(args[1], args[2]);
    return 0;
}

var root = Path.GetFullPath(args.Length > 0 ? args[0] : "../..");
var projects = new List<object>();
var types = new List<object>();
var missing = new List<object>();
var failures = new List<string>();
var annotations = new List<object>();
var comparisonOption = Array.IndexOf(args, "--compare-source");
var baseline = comparisonOption >= 0 ? Path.GetFullPath(args[comparisonOption + 1]) : null;
var comparisons = new Dictionary<string, string>();
foreach (var project in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
    .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
    .Concat([Path.Combine(root, "installer/SetupBridge/SetupBridge.csproj")]).Order())
{
    var directory = Path.GetDirectoryName(project)!;
    var document = XDocument.Load(project);
    IEnumerable<XElement> Elements(string name) => document.Descendants().Where(e => e.Name.LocalName == name);
    var id = Path.GetFileNameWithoutExtension(project);
    var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (document.Root!.Attribute("Sdk") is not null)
        files.UnionWith(Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj")));
    foreach (var include in Elements("Compile").Select(e => e.Attribute("Include")?.Value).Where(p => p is not null))
        files.Add(Path.GetFullPath(Path.Combine(directory, include!.Replace('\\', '/'))));
    projects.Add(new { id, path = Relative(project), assembly = Elements("AssemblyName").FirstOrDefault()?.Value ?? id,
        framework = Elements("TargetFramework").FirstOrDefault()?.Value ?? Elements("TargetFrameworkVersion").FirstOrDefault()?.Value,
        references = Elements("ProjectReference").Select(e => Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value.Replace('\\', '/'))).ToArray(),
        files = files.Select(Relative).Order().ToArray() });
    foreach (var file in files.Order())
    {
        if (!File.Exists(file)) { failures.Add($"Missing compile input: {Relative(file)}"); continue; }
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), new CSharpParseOptions(LanguageVersion.Preview), path: file);
        if (baseline is not null && !comparisons.ContainsKey(Relative(file)))
        {
            var original = Path.Combine(baseline, Relative(file));
            if (!File.Exists(original)) comparisons[Relative(file)] = "new";
            else if (File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(original))) comparisons[Relative(file)] = "unchanged";
            else
            {
                var originalRoot = CSharpSyntaxTree.ParseText(File.ReadAllText(original), new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
                static IEnumerable<(int, string)> Tokens(SyntaxNode node) => node.DescendantTokens().Select(token => (token.RawKind, token.Text));
                static IEnumerable<string> Directives(SyntaxNode node) => node.DescendantTrivia().Where(trivia => trivia.IsDirective || trivia.IsKind(SyntaxKind.DisabledTextTrivia)).Select(trivia => trivia.ToString());
                comparisons[Relative(file)] = Tokens(tree.GetRoot()).SequenceEqual(Tokens(originalRoot)) && Directives(tree.GetRoot()).SequenceEqual(Directives(originalRoot)) ? "comments-only" : "syntax-changed";
            }
        }
        failures.AddRange(tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));
        foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>()
            .Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax))
        {
            var name = declaration switch { BaseTypeDeclarationSyntax t => t.Identifier.Text, DelegateDeclarationSyntax d => d.Identifier.Text, _ => "" };
            var ns = string.Join('.', declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString()));
            var nesting = declaration.Ancestors().OfType<TypeDeclarationSyntax>().Reverse().Select(t => t.Identifier.Text);
            var uid = string.Join('.', new[] { ns }.Where(n => n.Length > 0).Concat(nesting).Append(name));
            var generic = declaration is TypeDeclarationSyntax type ? type.TypeParameterList?.Parameters.Count : declaration is DelegateDeclarationSyntax del ? del.TypeParameterList?.Parameters.Count : null;
            if (generic > 0) uid += "`" + generic;
            var summary = Summary(declaration);
            Annotate(declaration, uid, true);
            if (summary.Length == 0) missing.Add(new { project = id, file = Relative(file), kind = "type", name = uid, line = Line(declaration), signature = Signature(declaration) });
            var members = declaration is TypeDeclarationSyntax td ? td.Members.SelectMany(MemberEntries).ToArray() : [];
            types.Add(new { project = id, uid, name, @namespace = ns, kind = declaration.Kind().ToString(), summary,
                signature = Signature(declaration), file = Relative(file), line = Line(declaration),
                bases = declaration is BaseTypeDeclarationSyntax bt ? bt.BaseList?.Types.Select(b => b.Type.ToString()).ToArray() ?? [] : [], members });

            IEnumerable<object> MemberEntries(MemberDeclarationSyntax member)
            {
                if (member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax) yield break;
                var names = member switch
                {
                    MethodDeclarationSyntax m => new[] { m.Identifier.Text },
                    ConstructorDeclarationSyntax c => [c.Identifier.Text],
                    PropertyDeclarationSyntax p => [p.Identifier.Text],
                    FieldDeclarationSyntax f => f.Declaration.Variables.Select(v => v.Identifier.Text).ToArray(),
                    EventFieldDeclarationSyntax e => e.Declaration.Variables.Select(v => v.Identifier.Text).ToArray(),
                    IndexerDeclarationSyntax => ["this"],
                    OperatorDeclarationSyntax o => ["operator " + o.OperatorToken.Text],
                    ConversionOperatorDeclarationSyntax c => [c.ImplicitOrExplicitKeyword.Text + " operator"],
                    _ => []
                };
                var parameters = member switch
                {
                    BaseMethodDeclarationSyntax m => m.ParameterList.Parameters,
                    IndexerDeclarationSyntax i => i.ParameterList.Parameters,
                    _ => default
                };
                var text = Summary(member);
                var exposed = member.Modifiers.Any(t => t.IsKind(SyntaxKind.PublicKeyword) || t.IsKind(SyntaxKind.InternalKeyword) || t.IsKind(SyntaxKind.ProtectedKeyword))
                    || declaration is InterfaceDeclarationSyntax;
                if (names.Length > 0) Annotate(member, uid + "." + names[0], exposed);
                if (text.Length == 0 && exposed && names.Length > 0)
                    missing.Add(new { project = id, file = Relative(file), kind = "member", name = uid + "." + names[0], line = Line(member), signature = Signature(member) });
                foreach (var memberName in names)
                    yield return new { name = memberName, kind = member.Kind().ToString(), summary = text, signature = Signature(member), line = Line(member), exposed,
                        parameters = parameters.Select(p => new { name = p.Identifier.Text, type = p.Type?.ToString(), description = ParameterDescription(member, p) }).ToArray() };
            }
            void Annotate(MemberDeclarationSyntax node, string identity, bool required)
            {
                var parameters = node switch
                {
                    BaseMethodDeclarationSyntax method => method.ParameterList.Parameters,
                    TypeDeclarationSyntax primary => primary.ParameterList?.Parameters ?? default,
                    DelegateDeclarationSyntax callback => callback.ParameterList.Parameters,
                    IndexerDeclarationSyntax indexer => indexer.ParameterList.Parameters,
                    _ => default
                };
                var generics = node switch { TypeDeclarationSyntax type => type.TypeParameterList, MethodDeclarationSyntax method => method.TypeParameterList, DelegateDeclarationSyntax callback => callback.TypeParameterList, _ => null };
                annotations.Add(new { project = id, file = Relative(file), name = identity, kind = node.Kind().ToString(), position = node.SpanStart,
                    line = Line(node), required, summary = Summary(node), xml = Documentation(node)?.ToString(), description = Description(node.AttributeLists),
                    parameters = parameters.Select(p => new { name = p.Identifier.Text, type = p.Type?.ToString(), description = Description(p.AttributeLists) }).ToArray(),
                    typeParameters = generics?.Parameters.Select(p => p.Identifier.Text).ToArray() ?? [],
                    returnType = node is MethodDeclarationSyntax m ? m.ReturnType.ToString() : node is OperatorDeclarationSyntax o ? o.ReturnType.ToString() : null });
            }
        }
    }
}
var output = Path.Combine(root, "artifacts/docfx/source-index.json");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, JsonSerializer.Serialize(new { projects, types, missing, failures, annotations }, new JsonSerializerOptions { WriteIndented = true }));
if (baseline is not null)
{
    var expected = new[] { "src/YuzuhaToolkit.Mcp/Program.cs", "src/YuzuhaToolkit.Knowledge/Program.cs", "src/YuzuhaToolkit.Mcp/YuzuhaRpcBridge.cs", "src/YuzuhaToolkit.Mcp/PmlCallTools.cs", "src/YuzuhaToolkit.Mcp/McpUsageInstructions.cs" };
    foreach (var entry in comparisons.Where(entry => entry.Value == "syntax-changed" && !expected.Contains(entry.Key)))
        failures.Add("Unexpected non-comment source change: " + entry.Key);
    File.WriteAllText(Path.Combine(root, "artifacts/source-comparison.json"), JsonSerializer.Serialize(new { method = "Roslyn token kind/text, preprocessor directives and disabled code; comments excluded", comparisons, failures }, new JsonSerializerOptions { WriteIndented = true }));
}
Console.WriteLine(JsonSerializer.Serialize(new { projects = projects.Count, typeDeclarations = types.Count, missingComments = missing.Count, syntaxErrors = failures.Count }));
return failures.Count > 0 || (args.Contains("--check-comments") && missing.Count > 0) ? 1 : 0;

string Relative(string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
static string Signature(MemberDeclarationSyntax node)
{
    var text = node.WithoutLeadingTrivia().ToString();
    var end = node switch
    {
        BaseMethodDeclarationSyntax m when m.Body is not null => m.Body.SpanStart - node.SpanStart,
        BaseMethodDeclarationSyntax m when m.ExpressionBody is not null => m.ExpressionBody.SpanStart - node.SpanStart,
        TypeDeclarationSyntax t when t.OpenBraceToken.RawKind != 0 => t.OpenBraceToken.SpanStart - node.SpanStart,
        PropertyDeclarationSyntax p when p.ExpressionBody is not null => p.ExpressionBody.SpanStart - node.SpanStart,
        _ => text.Length
    };
    // Retain contract attributes and stop the displayed declaration at the body boundary.
    return text[..Math.Clamp(end, 0, text.Length)].Trim();
}
static XElement? Documentation(SyntaxNode node)
{
    var trivia = node.GetLeadingTrivia().Select(t => t.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().FirstOrDefault();
    if (trivia is null) return null;
    var xml = string.Join('\n', trivia.ToFullString().Split('\n').Select(line => line.TrimStart().TrimStart('/').TrimStart()));
    try { return XElement.Parse("<doc>" + xml + "</doc>"); } catch { return null; }
}
static string Summary(SyntaxNode node) => Documentation(node)?.Element("summary")?.Value.Trim()
    ?? (Documentation(node)?.Element("inheritdoc") is not null ? "继承接口契约。" : "");
static string ParameterDescription(SyntaxNode member, ParameterSyntax parameter) =>
    Documentation(member)?.Elements("param").FirstOrDefault(e => e.Attribute("name")?.Value == parameter.Identifier.Text)?.Value.Trim() ?? "";
static string Description(SyntaxList<AttributeListSyntax> lists)
{
    var attribute = lists.SelectMany(list => list.Attributes).FirstOrDefault(a => a.Name.ToString().EndsWith("Description") || a.Name.ToString().EndsWith("DescriptionAttribute"));
    return attribute?.ArgumentList?.Arguments.FirstOrDefault() is { } argument ? Constant(argument.Expression) : "";
    static string Constant(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax literal => literal.Token.ValueText,
        BinaryExpressionSyntax addition when addition.IsKind(SyntaxKind.AddExpression) => Constant(addition.Left) + Constant(addition.Right),
        _ => ""
    };
}
