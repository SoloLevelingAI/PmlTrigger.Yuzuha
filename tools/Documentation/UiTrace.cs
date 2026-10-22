using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Produce static UI relationships from actual compiler inputs; never claim runtime execution.</summary>
internal static class UiTrace
{
    internal sealed record Project(string Name, string[] Sources, string[] References, string Defines, string? OutputType = null);
    internal sealed record Manifest(Project[] Projects, string[] Xaml);
    internal sealed record Node(string Id, string Name, string? File, int? Line, bool Generated, string? SourceSha256);
    internal sealed record Edge(string From, string To, string Kind, string File, int Line);
    internal sealed record Entry(string Id, string View, string Control, string Member, string Expression, string? Target, string Resolution, string? Reason, string File, int Line);
    internal sealed record Boundary(string? From, string Kind, string Expression, string Reason, string File, int Line);

    internal static void Write(string repository, string manifestPath, string destination)
    {
        repository = Path.GetFullPath(repository);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath), options)!;
        var compilations = manifest.Projects.Select(project =>
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: project.Defines.Split(';', StringSplitOptions.RemoveEmptyEntries));
            var trees = project.Sources.Distinct(StringComparer.OrdinalIgnoreCase).Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), parse, file));
            var paths = project.References.Length > 0 ? project.References : ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
            var kind = project.OutputType switch { "Exe" => OutputKind.ConsoleApplication, "WinExe" => OutputKind.WindowsApplication, _ => OutputKind.DynamicallyLinkedLibrary };
            return CSharpCompilation.Create(project.Name, trees, paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(path => MetadataReference.CreateFromFile(path)), new CSharpCompilationOptions(kind, allowUnsafe: true, nullableContextOptions: NullableContextOptions.Enable));
        }).ToArray();
        var errors = compilations.SelectMany(c => c.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => c.AssemblyName + ": " + d)).ToArray();
        if (errors.Length > 0) throw new InvalidDataException("UI semantic inputs do not compile:\n" + string.Join('\n', errors.Take(30)));
        var nodes = new Dictionary<string, Node>();
        var edges = new HashSet<Edge>();
        var boundaries = new List<Boundary>();
        var entries = new List<Entry>();
        var symbols = new Dictionary<string, ISymbol>();
        var types = new List<INamedTypeSymbol>();
        string Relative(string file) => Path.GetRelativePath(repository, file).Replace('\\', '/');
        static ISymbol Canonical(ISymbol symbol) => symbol is IMethodSymbol method ? (method.ReducedFrom ?? method).OriginalDefinition : symbol.OriginalDefinition;
        static string Id(ISymbol symbol) { symbol = Canonical(symbol); return symbol.ContainingAssembly.Name + "::" + (symbol.GetDocumentationCommentId() ?? symbol.ToDisplayString()); }
        string Add(ISymbol symbol)
        {
            symbol = Canonical(symbol); var id = Id(symbol);
            var declaration = symbol is IMethodSymbol { PartialImplementationPart: { } implementation } ? implementation : symbol;
            var syntax = declaration.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            if (!nodes.ContainsKey(id) || nodes[id].File is null && syntax is not null)
            {
                var file = syntax?.SyntaxTree.FilePath;
                nodes[id] = new(id, symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), file is null ? null : Relative(file), syntax is null ? null : Line(syntax), file?.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) == true || file?.Contains("ui-generated", StringComparison.OrdinalIgnoreCase) == true,
                    file is null ? null : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant());
            }
            return id;
        }
        void Connect(ISymbol owner, ISymbol target, string kind, SyntaxNode at) => edges.Add(new(Add(owner), Add(target), kind, Relative(at.SyntaxTree.FilePath), Line(at)));
        ISymbol? Owner(SyntaxNode node, SemanticModel model)
        {
            foreach (var ancestor in node.Ancestors())
            {
                if (ancestor is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax) return model.GetDeclaredSymbol(ancestor);
                if (ancestor is PropertyDeclarationSyntax property) return property.ExpressionBody is not null ? ((IPropertySymbol)model.GetDeclaredSymbol(property)!).GetMethod : model.GetDeclaredSymbol(property);
                if (ancestor is VariableDeclaratorSyntax variable && variable.Parent?.Parent is FieldDeclarationSyntax) return model.GetDeclaredSymbol(variable);
            }
            return null;
        }
        foreach (var compilation in compilations)
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree); var root = tree.GetRoot();
            foreach (var declaration in root.DescendantNodes().Where(n => n is BaseTypeDeclarationSyntax or BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or PropertyDeclarationSyntax or LocalFunctionStatementSyntax))
            {
                if (model.GetDeclaredSymbol(declaration) is not { } symbol) continue;
                Add(symbol); symbols[Id(symbol)] = symbol;
                if (symbol is INamedTypeSymbol type) types.Add(type);
            }
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (Owner(invocation, model) is not { } owner) continue;
                if (model.GetSymbolInfo(invocation).Symbol is IMethodSymbol target)
                {
                    var kind = target.ContainingType.TypeKind == TypeKind.Interface ? "interface-call" : target.IsVirtual || target.IsAbstract || target.IsOverride ? "virtual-dispatch" : target.MethodKind == MethodKind.DelegateInvoke ? "delegate-invoke" : "call";
                    if (kind is "virtual-dispatch" or "delegate-invoke") boundaries.Add(new(Add(owner), kind, invocation.ToString(), "只确认源码中的声明/委托调用；运行时重写方法或委托目标未做数据流推导。", Relative(tree.FilePath), Line(invocation)));
                    if (invocation.Ancestors().TakeWhile(n => n is not BaseMethodDeclarationSyntax and not AccessorDeclarationSyntax).Any(n => n is AnonymousFunctionExpressionSyntax)) kind = "callback-body:" + kind;
                    Connect(owner, target, kind, invocation);
                    ExpressionSyntax? receiver = invocation.Expression is MemberAccessExpressionSyntax member ? member.Expression : invocation.Expression is MemberBindingExpressionSyntax ? invocation.Ancestors().OfType<ConditionalAccessExpressionSyntax>().FirstOrDefault()?.Expression : invocation.Expression;
                    if (receiver is not null && model.GetSymbolInfo(receiver).Symbol is IEventSymbol raised)
                    {
                        Connect(owner, raised, "event-raise", invocation);
                        boundaries.Add(new(Add(raised), "event-runtime-state", invocation.ToString(), "订阅关系只是候选；无法证明此时已订阅、未解除或实际触发。", Relative(tree.FilePath), Line(invocation)));
                    }
                }
                else boundaries.Add(new(Add(owner), "unresolved-call", invocation.ToString(), "Roslyn 未给出唯一方法符号；动态调用、错误或候选歧义不按名称猜测。", Relative(tree.FilePath), Line(invocation)));
            }
            foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (model.GetSymbolInfo(assignment.Left).Symbol is IPropertySymbol { SetMethod: { } setter } && Owner(assignment, model) is { } owner)
                    Connect(owner, setter, "property-write", assignment);
                if (model.GetSymbolInfo(assignment.Left).Symbol is IEventSymbol subscription)
                {
                    if (assignment.IsKind(SyntaxKind.AddAssignmentExpression) && model.GetSymbolInfo(assignment.Right).Symbol is IMethodSymbol handler)
                        Connect(subscription, handler, "possible-event-handler", assignment);
                    else boundaries.Add(new(Add(subscription), assignment.IsKind(SyntaxKind.SubtractAssignmentExpression) ? "event-unsubscription" : "event-handler-unresolved", assignment.ToString(), "解除订阅或动态/匿名处理器不推导为确定执行。", Relative(tree.FilePath), Line(assignment)));
                }
            }
            foreach (var creation in root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                var owner = creation.Parent is AssignmentExpressionSyntax assignment ? model.GetSymbolInfo(assignment.Left).Symbol : Owner(creation, model);
                if (owner is null || creation.ArgumentList is null || model.GetSymbolInfo(creation).Symbol is not IMethodSymbol constructor) continue;
                foreach (var argument in creation.ArgumentList.Arguments)
                {
                    if (model.GetSymbolInfo(argument.Expression).Symbol is not IMethodSymbol callback) continue;
                    var index = creation.ArgumentList.Arguments.IndexOf(argument);
                    var parameter = argument.NameColon?.Name.Identifier.ValueText ?? constructor.Parameters.ElementAtOrDefault(index)?.Name ?? index.ToString();
                    Connect(owner, callback, "delegate-registration:" + parameter, argument);
                }
            }
            foreach (var attribute in root.DescendantNodes().OfType<AttributeSyntax>().Where(a => a.Name.ToString().Split('.').Last() is "ObservableProperty" or "ObservablePropertyAttribute" or "RelayCommand" or "RelayCommandAttribute"))
                boundaries.Add(new(null, "generator-input", attribute.ToString(), "特性本身不是调用证据；仅分析本次编译导出的生成源码中实际存在的调用。", Relative(tree.FilePath), Line(attribute)));
        }
        // Link interface contracts to all source implementations, without asserting which instance is used.
        foreach (var method in symbols.Values.OfType<IMethodSymbol>().Where(m => m.ContainingType.TypeKind == TypeKind.Interface).ToArray())
        foreach (var type in types.Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default))
        foreach (var contract in type.AllInterfaces)
        foreach (var member in contract.GetMembers(method.Name).OfType<IMethodSymbol>().Where(m => Id(m) == Id(method)))
            if (type.FindImplementationForInterfaceMember(member) is IMethodSymbol implementation && implementation.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is { } syntax)
                Connect(method, implementation, "possible-implementation", syntax);

        var events = new HashSet<string>(["Click", "Checked", "Unchecked", "SelectionChanged", "TextChanged", "Loaded", "KeyDown", "MouseDown", "Closing", "Closed", "SizeChanged"], StringComparer.Ordinal);
        foreach (var file in manifest.Xaml)
        {
            var document = XDocument.Load(file, LoadOptions.SetLineInfo);
            var view = (string?)document.Root?.Attribute(XName.Get("Class", "http://schemas.microsoft.com/winfx/2006/xaml"));
            if (view is null) continue;
            var owners = compilations.Select(c => c.GetTypeByMetadataName(view)).Where(t => t?.DeclaringSyntaxReferences.Length > 0).ToArray();
            foreach (var attribute in document.Descendants().Attributes().Where(a => a.Name.NamespaceName.Length == 0 && (events.Contains(a.Name.LocalName) || a.Name.LocalName == "Command" || a.Name.LocalName != "DataContext" && a.Value.StartsWith("{Binding", StringComparison.Ordinal))))
            {
                var control = attribute.Parent!; var line = ((IXmlLineInfo)attribute).LineNumber;
                ISymbol? target = null; string? reason = null;
                if (events.Contains(attribute.Name.LocalName))
                {
                    var candidates = owners.SelectMany(t => t!.GetMembers(attribute.Value).OfType<IMethodSymbol>()).Where(m => m.Parameters.Length == 2).ToArray();
                    if (candidates.Length == 1) target = candidates[0];
                    else reason = "事件处理器缺失或有歧义；未按同名方法猜测。";
                }
                else
                {
                    var binding = attribute.Value;
                    var match = System.Text.RegularExpressions.Regex.Match(binding, @"^\{Binding\s+(?:Path=)?([A-Za-z_][A-Za-z0-9_]*)(?:\s*,\s*Mode\s*=\s*(TwoWay))?\s*\}$");
                    var context = RuntimeContext(control);
                    if (!match.Success || context is null) reason = "绑定或运行时 DataContext 静态不可确定（模板、动态上下文、ElementName/RelativeSource 等）。";
                    else
                    {
                        var space = context.GetNamespaceOfPrefix(context.Name.NamespaceName.Length == 0 ? "" : context.GetPrefixOfNamespace(context.Name.Namespace) ?? "");
                        var ns = space?.NamespaceName ?? context.Name.NamespaceName;
                        if (!ns.StartsWith("clr-namespace:", StringComparison.Ordinal)) reason = "DataContext 命名空间无法解析。";
                        else
                        {
                            var fullName = ns[14..].Split(';')[0] + "." + context.Name.LocalName;
                            var contextAssembly = ns.Split(';').FirstOrDefault(p => p.StartsWith("assembly=", StringComparison.Ordinal))?[9..] ?? (owners.Length == 1 ? owners[0]!.ContainingAssembly.Name : null);
                            var candidates = compilations.Where(c => c.AssemblyName == contextAssembly).Select(c => c.GetTypeByMetadataName(fullName)).Where(t => t?.DeclaringSyntaxReferences.Length > 0).SelectMany(t => t!.GetMembers(match.Groups[1].Value).OfType<IPropertySymbol>()).ToArray();
                            if (candidates.Length == 1 && attribute.Name.LocalName == "Command") target = candidates[0];
                            else if (candidates.Length == 1 && match.Groups[2].Success && candidates[0].SetMethod is { } setter) target = setter;
                            else reason = "属性缺失/歧义或未明确 TwoWay；未猜测默认绑定方向或生成器成员。";
                        }
                    }
                }
                entries.Add(new("xaml:" + Relative(file) + ":" + line + ":" + entries.Count, view, (string?)control.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) ?? (string?)control.Attribute("Content") ?? control.Name.LocalName, attribute.Name.LocalName, attribute.Value, target is null ? null : Add(target), target is null ? "unresolved" : "resolved", reason, Relative(file), line));
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        File.WriteAllText(destination, JsonSerializer.Serialize(new { schemaVersion = 1, kind = "static-source-trace", scope = "Potential source relationships; not runtime execution, ordering, coverage or AVEVA evidence", projects = manifest.Projects.Select(p => p.Name), nodes = nodes.Values.OrderBy(n => n.Id), edges = edges.OrderBy(e => e.From).ThenBy(e => e.Line).ThenBy(e => e.To), entries, boundaries }, options));
        Console.WriteLine($"UI static trace: {entries.Count} XAML entries, {edges.Count} relationships, {boundaries.Count} explicit boundaries.");
    }

    private static int Line(SyntaxNode node) => node.SyntaxTree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
    private static XElement? RuntimeContext(XElement element)
    {
        foreach (var ancestor in element.AncestorsAndSelf())
        {
            if (ancestor.Name.LocalName.Contains("Template", StringComparison.Ordinal) || ancestor.Attribute("DataContext") is not null) return null;
            var context = ancestor.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith(".DataContext", StringComparison.Ordinal));
            if (context is not null) return context.Elements().Count() == 1 ? context.Elements().Single() : null;
        }
        return null;
    }
}
