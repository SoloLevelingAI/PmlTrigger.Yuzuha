using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

/// <summary>Produce static UI relationships from actual compiler inputs; never claim runtime execution.</summary>
internal static class UiTrace
{
    internal sealed record Project(string Name, string[] Sources, string[] References, string Defines, string? OutputType = null);
    internal sealed record Manifest(Project[] Projects, string[] Xaml, string[]? OmittedProjects = null);
    internal sealed record Node(string Id, string Name, string? File, int? Line, bool Generated, string? SourceSha256, string? TypeId, string? Summary, string? Body);
    internal sealed record Evidence(string Role, string File, int Line);
    internal sealed record Edge(string From, string To, string Kind, string File, int Line, string Expression, string[] Context, int Offset, Evidence[]? Evidence = null, string? CallbackReceiver = null);
    internal sealed record Page(string Id, string View, string Name, string Kind, string? ParentId, string[] HostedViews, string File, int Line);
    internal sealed record Entry(string Id, string View, string Control, string Member, string Expression, string? Target, string Resolution, string? Reason, string File, int Line, string PageId, bool InitiallyDisabled, string SourceSha256);
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
        var pages = new List<Page>();
        var symbols = new Dictionary<string, ISymbol>();
        var types = new List<INamedTypeSymbol>();
        var assignedFields = new HashSet<string>();
        string Relative(string file) => Path.GetRelativePath(repository, file).Replace('\\', '/');
        static ISymbol Canonical(ISymbol symbol) => symbol is IMethodSymbol method ? (method.ReducedFrom ?? method).OriginalDefinition : symbol.OriginalDefinition;
        string Id(ISymbol symbol)
        {
            symbol = Canonical(symbol);
            if (symbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction } && symbol.DeclaringSyntaxReferences.FirstOrDefault() is { } lambda)
                return symbol.ContainingAssembly.Name + "::lambda:" + Relative(lambda.SyntaxTree.FilePath) + ":" + lambda.Span.Start;
            return symbol.ContainingAssembly.Name + "::" + (symbol.GetDocumentationCommentId() ?? symbol.ToDisplayString());
        }
        string Add(ISymbol symbol)
        {
            symbol = Canonical(symbol); var id = Id(symbol);
            var declaration = symbol is IMethodSymbol { PartialImplementationPart: { } implementation } ? implementation : symbol;
            var syntax = declaration.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            if (!nodes.ContainsKey(id) || nodes[id].File is null && syntax is not null)
            {
                var file = syntax?.SyntaxTree.FilePath;
                var owner = symbol as INamedTypeSymbol ?? symbol.ContainingType;
                var comment = symbol.GetDocumentationCommentXml();
                var summary = string.IsNullOrWhiteSpace(comment) ? null : XElement.Parse(comment).Element("summary")?.Value.Trim();
                nodes[id] = new(id, symbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction } ? symbol.ContainingSymbol.Name + ".回调 L" + Line(syntax!) : symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), file is null ? null : Relative(file), syntax is null ? null : Line(syntax), file?.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) == true || file?.Contains("ui-generated", StringComparison.OrdinalIgnoreCase) == true,
                    file is null ? null : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant(), owner is null ? null : owner.ContainingAssembly.Name + "::" + owner.OriginalDefinition.GetDocumentationCommentId()?[2..], summary,
                    syntax is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax or PropertyDeclarationSyntax ? syntax.ToString() : null);
            }
            return id;
        }
        void Connect(ISymbol owner, ISymbol target, string kind, SyntaxNode at, ISymbol? callbackReceiver = null) => edges.Add(new(Add(owner), Add(target), kind, Relative(at.SyntaxTree.FilePath), Line(at), at.ToString(), Context(at), at.SpanStart, CallbackReceiver: callbackReceiver is null ? null : Add(callbackReceiver)));
        ISymbol? Owner(SyntaxNode node, SemanticModel model)
        {
            foreach (var ancestor in node.Ancestors())
            {
                if (ancestor is AnonymousFunctionExpressionSyntax lambda) return model.GetSymbolInfo(lambda).Symbol;
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
                    Connect(owner, target, kind, invocation);
                    foreach (var argument in invocation.ArgumentList.Arguments)
                    {
                        if (model.GetOperation(argument) is not IArgumentOperation { Parameter: { Type.TypeKind: TypeKind.Delegate } parameter }) continue;
                        if (model.GetSymbolInfo(argument.Expression).Symbol is IMethodSymbol callback)
                            Connect(owner, callback, "callback-argument:" + parameter.Name, argument.Expression, target);
                    }
                    ExpressionSyntax? receiver = invocation.Expression is MemberAccessExpressionSyntax member ? member.Expression : invocation.Expression is MemberBindingExpressionSyntax ? invocation.Ancestors().OfType<ConditionalAccessExpressionSyntax>().FirstOrDefault()?.Expression : invocation.Expression;
                    if (receiver is not null && model.GetSymbolInfo(receiver).Symbol is IEventSymbol raised)
                    {
                        Connect(owner, raised, "event-raise", invocation);
                        boundaries.Add(new(Add(raised), "event-runtime-state", invocation.ToString(), "订阅关系只是候选；无法证明此时已订阅、未解除或实际触发。", Relative(tree.FilePath), Line(invocation)));
                    }
                }
                else boundaries.Add(new(Add(owner), "unresolved-call", invocation.ToString(), "Roslyn 未给出唯一方法符号；动态调用、错误或候选歧义不按名称猜测。", Relative(tree.FilePath), Line(invocation)));
            }
            // Source-defined getters can contain validation/calculation. Framework getters stay external.
            foreach (var access in root.DescendantNodes().OfType<ExpressionSyntax>().Where(n => n is IdentifierNameSyntax or MemberAccessExpressionSyntax or MemberBindingExpressionSyntax))
            {
                if (access.Parent is MemberAccessExpressionSyntax member && member.Name == access || access.Parent is MemberBindingExpressionSyntax) continue;
                if (access.Parent is AssignmentExpressionSyntax assignment && assignment.Left == access && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)) continue;
                if (access.Ancestors().OfType<InvocationExpressionSyntax>().Any(i => i.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" })) continue;
                if (model.GetSymbolInfo(access).Symbol is IPropertySymbol { GetMethod: { } getter } && compilations.Any(c => c.AssemblyName == getter.ContainingAssembly.Name) && Owner(access, model) is { } owner)
                    Connect(owner, getter, "property-read", access);
            }
            foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (model.GetSymbolInfo(assignment.Left).Symbol is IFieldSymbol assignedField) assignedFields.Add(Id(assignedField));
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
                // A discard has no assembly or declaration; construction still executes in
                // the enclosing method/lambda, even when its result is discarded.
                var assigned = creation.Parent is AssignmentExpressionSyntax assignment ? model.GetSymbolInfo(assignment.Left).Symbol : null;
                var owner = assigned is not null and not IDiscardSymbol ? assigned : Owner(creation, model);
                if (owner is null || model.GetSymbolInfo(creation).Symbol is not IMethodSymbol constructor) continue;
                Connect(owner, constructor, "constructor", creation);
                if (creation.ArgumentList is null) continue;
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

        // Cross-process correspondence is not a C# call. Require all three pieces of source evidence.
        var registrations = new List<(string Service, string Operation, IMethodSymbol Implementation, SyntaxNode Registration, SyntaxNode Contract)>();
        static string? RouteAttribute(ISymbol symbol, string name) => symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "PlantHost.Rpc." + name)?.ConstructorArguments.FirstOrDefault().Value as string;
        foreach (var compilation in compilations)
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol add || add.ContainingNamespace.ToDisplayString() != "PlantHost.Rpc") continue;
                INamedTypeSymbol? contract = null;
                if (add.Name == "AddService" && add.TypeArguments.Length == 1) contract = add.TypeArguments[0] as INamedTypeSymbol;
                if (add.Name == "AddImplementation" && invocation.Expression is MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax serviceCall } && model.GetSymbolInfo(serviceCall).Symbol is IMethodSymbol { Name: "ForService" } service && service.ContainingNamespace.ToDisplayString() == "PlantHost.Rpc" && service.TypeArguments.Length == 1) contract = service.TypeArguments[0] as INamedTypeSymbol;
                if (contract is null) continue;
                if (invocation.ArgumentList.Arguments.Count != 1 || invocation.ArgumentList.Arguments[0].Expression is not BaseObjectCreationExpressionSyntax creation || model.GetTypeInfo(creation).Type is not INamedTypeSymbol implementation) continue;
                if (RouteAttribute(contract, "RpcServiceAttribute") is not { } serviceName) continue;
                foreach (var method in contract.GetMembers().OfType<IMethodSymbol>())
                    if (RouteAttribute(method, "RpcOperationAttribute") is { } operation && implementation.FindImplementationForInterfaceMember(method) is IMethodSymbol handler && method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is { } declaration)
                        registrations.Add((serviceName, operation, handler, invocation, declaration));
            }
        }
        foreach (var compilation in compilations)
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (Owner(invocation, model) is not { } owner || model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol { Name: "InvokeAsync" } method || method.ContainingType.ToDisplayString() != "PlantHost.Rpc.RpcClient") continue;
                var argument = invocation.ArgumentList.Arguments.FirstOrDefault(a => model.GetOperation(a) is IArgumentOperation { Parameter.Ordinal: 0 });
                if (argument is null || model.GetTypeInfo(argument.Expression).Type is not INamedTypeSymbol { Name: "RpcMethod", Arity: 2 } routeType || routeType.ContainingNamespace.ToDisplayString() != "PlantHost.Rpc") continue;
                string? service = null, operation = null; SyntaxNode? routeDeclaration = null;
                if (model.GetSymbolInfo(argument.Expression).Symbol is IFieldSymbol { IsReadOnly: true } field && !assignedFields.Contains(Id(field)) && field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is VariableDeclaratorSyntax { Initializer.Value: BaseObjectCreationExpressionSyntax creation })
                {
                    var declarationModel = compilation.GetSemanticModel(creation.SyntaxTree);
                    if (declarationModel.GetOperation(creation) is IObjectCreationOperation route)
                    {
                        service = route.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 0)?.Value.ConstantValue.Value as string;
                        operation = route.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 1)?.Value.ConstantValue.Value as string;
                        routeDeclaration = creation;
                    }
                }
                var matches = registrations.Where(r => service is not null && r.Service == service && r.Operation == operation).ToArray();
                foreach (var match in matches)
                    edges.Add(new(Add(owner), Add(match.Implementation), "rpc-route", Relative(tree.FilePath), Line(invocation), service + " / " + operation,
                        [.. Context(invocation), "路由/注册源码对应；未证明 Host 已启动、实际选中该框架或负载兼容"], invocation.SpanStart,
                        [new("客户端路由常量", Relative(routeDeclaration!.SyntaxTree.FilePath), Line(routeDeclaration)), new("RPC 操作契约", Relative(match.Contract.SyntaxTree.FilePath), Line(match.Contract)), new("Host 服务注册", Relative(match.Registration.SyntaxTree.FilePath), Line(match.Registration))]));
                if (matches.Length == 0) boundaries.Add(new(Add(owner), "rpc-unmapped", service is null ? invocation.ToString() : service + " / " + operation,
                    "没有同时满足路由常量、Host 契约与服务注册的源码对应。Host 编译输入可能未提供；不按同名方法补链。", Relative(tree.FilePath), Line(invocation)));
            }
        }

        var events = new HashSet<string>(["Click", "Checked", "Unchecked", "SelectionChanged", "TextChanged", "Loaded", "KeyDown", "MouseDown", "Closing", "Closed", "SizeChanged"], StringComparer.Ordinal);
        var xmlViews = manifest.Xaml.Order().Select(file => (File: file, Xml: XDocument.Load(file, LoadOptions.SetLineInfo)))
            .Select(item => (item.File, item.Xml, View: (string?)item.Xml.Root?.Attribute(XName.Get("Class", "http://schemas.microsoft.com/winfx/2006/xaml"))))
            .Where(item => item.View is not null).ToArray();
        var viewNames = xmlViews.Select(item => item.View!).ToHashSet(StringComparer.Ordinal);
        string[] HostedViews(XElement scope) => scope.Descendants().Where(e => e.Ancestors().FirstOrDefault(a => a.Name.LocalName == "TabItem") == (scope.Name.LocalName == "TabItem" ? scope : null))
            .Select(e => e.Name.NamespaceName.StartsWith("clr-namespace:", StringComparison.Ordinal) ? e.Name.NamespaceName[14..].Split(';')[0] + "." + e.Name.LocalName : "")
            .Where(viewNames.Contains).Distinct().Order().ToArray();
        foreach (var (file, document, declaredView) in xmlViews)
        {
            var view = declaredView!;
            var sharedId = "view:" + view;
            var tabs = document.Descendants().Where(e => e.Name.LocalName == "TabItem").ToArray();
            var tabIds = tabs.Select((tab, index) => (tab, id: "page:" + Relative(file) + "#tab-" + index)).ToDictionary(item => item.tab, item => item.id);
            pages.Add(new(sharedId, view, view.Split('.').Last() + " · 共享/生命周期", "shared", null, HostedViews(document.Root!), Relative(file), 1));
            foreach (var tab in tabs)
            {
                var parent = tab.Ancestors().FirstOrDefault(e => e.Name.LocalName == "TabItem");
                var hosted = HostedViews(tab);
                pages.Add(new(tabIds[tab], view, (string?)tab.Attribute("Header") ?? "TabItem " + Array.IndexOf(tabs, tab), tab.Descendants().Any(e => e.Name.LocalName == "TabItem") || hosted.Length > 0 ? "container" : "page", parent is null ? sharedId : tabIds[parent], hosted, Relative(file), ((IXmlLineInfo)tab).LineNumber));
            }
            var fileHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
            var owners = compilations.Select(c => c.GetTypeByMetadataName(view)).Where(t => t?.DeclaringSyntaxReferences.Length > 0).ToArray();
            void AddEntry(XElement control, string member, string expression, ISymbol? target, string? reason, int line)
            {
                var tab = control.AncestorsAndSelf().FirstOrDefault(e => e.Name.LocalName == "TabItem");
                entries.Add(new("xaml:" + Relative(file) + ":" + line + ":" + entries.Count, view, (string?)control.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) ?? (string?)control.Attribute("Content") ?? control.Name.LocalName, member, expression, target is null ? null : Add(target), target is null ? "unresolved" : "resolved", reason, Relative(file), line, tab is null ? sharedId : tabIds[tab], control.AncestorsAndSelf().Any(e => string.Equals((string?)e.Attribute("IsEnabled"), "False", StringComparison.OrdinalIgnoreCase)), fileHash));
            }
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
                AddEntry(control, attribute.Name.LocalName, attribute.Value, target, reason, line);
            }
            foreach (var control in document.Descendants().Where(e => e.Name.LocalName is "Button" or "ToggleButton" or "MenuItem" or "Hyperlink" && e.Attribute("Click") is null && e.Attribute("Command") is null))
                AddEntry(control, "Unwired", "未声明 Click / Command", null, "XAML 未声明 Click / Command；运行时代码接线未静态追踪。初始禁用不证明以后不会启用。", ((IXmlLineInfo)control).LineNumber);
            foreach (var method in owners.SelectMany(t => t!.GetMembers().OfType<IMethodSymbol>()).Where(m => !m.IsImplicitlyDeclared && (m.MethodKind == MethodKind.Constructor || m.IsOverride)))
            {
                if (method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not { } syntax) continue;
                var sourceFile = syntax.SyntaxTree.FilePath;
                entries.Add(new("code:" + Id(method), view, method.Name, method.MethodKind == MethodKind.Constructor ? "C# Constructor" : "C# Override", method.Name, Add(method), "resolved", "生命周期源码入口；不证明窗口已创建或框架已调用。", Relative(sourceFile), Line(syntax), sharedId, false, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourceFile))).ToLowerInvariant()));
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        File.WriteAllText(destination, JsonSerializer.Serialize(new { schemaVersion = 3, kind = "static-source-trace", scope = "Potential source relationships; not runtime execution, ordering, coverage or AVEVA evidence", projects = manifest.Projects.Select(p => p.Name), omittedProjects = manifest.OmittedProjects ?? [], pages, nodes = nodes.Values.OrderBy(n => n.Id), edges = edges.OrderBy(e => e.From).ThenBy(e => e.Line).ThenBy(e => e.Offset).ThenBy(e => e.To), entries, boundaries }, options));
        Console.WriteLine($"UI static trace: {entries.Count} XAML entries, {edges.Count} relationships, {boundaries.Count} explicit boundaries.");
    }

    private static int Line(SyntaxNode node) => node.SyntaxTree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
    // Lexical context only: preceding guards and reachability are not data-flow proofs.
    private static string[] Context(SyntaxNode at) => at.Ancestors().TakeWhile(n => n is not BaseMethodDeclarationSyntax and not AccessorDeclarationSyntax and not LocalFunctionStatementSyntax and not AnonymousFunctionExpressionSyntax)
        .Select(n => n switch
        {
            IfStatementSyntax s when s.Statement.Span.Contains(at.Span) => "if (" + s.Condition + ")",
            IfStatementSyntax s when s.Else?.Span.Contains(at.Span) == true => "else of (" + s.Condition + ")",
            ConditionalExpressionSyntax s when s.WhenTrue.Span.Contains(at.Span) => "?: true (" + s.Condition + ")",
            ConditionalExpressionSyntax s when s.WhenFalse.Span.Contains(at.Span) => "?: false (" + s.Condition + ")",
            ForStatementSyntax s => "for (" + s.Condition + ")",
            ForEachStatementSyntax s => "foreach (" + s.Identifier + " in " + s.Expression + ")",
            WhileStatementSyntax s => "while (" + s.Condition + ")",
            CatchClauseSyntax s => "catch " + s.Declaration + " " + s.Filter,
            FinallyClauseSyntax => "finally",
            LockStatementSyntax s => "lock (" + s.Expression + ")",
            SwitchSectionSyntax s => string.Join(" ", s.Labels),
            _ => null
        }).Where(s => s is not null).Cast<string>().Reverse().ToArray();
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
