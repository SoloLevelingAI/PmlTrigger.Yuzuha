using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Core30.Requests;

/// <summary>Requires generated factories at the desktop-to-Core request boundary.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class CoreRequestAnalyzer : DiagnosticAnalyzer
{
    private const string GeneratorName = "Core30.Requests.RequestGenerator";

    private static readonly DiagnosticDescriptor GeneratedFactoryRequired = new(
        "PMT001",
        "Create Core requests with a generated factory",
        "Use a generated factory to construct this Core request",
        "Contracts",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(GeneratedFactoryRequired);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Razor emits application C# as generated files. Analyze those files as well.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(start =>
        {
            var requestType = start.Compilation.GetTypeByMetadataName("Core30.Core.CoreRequest");
            if (requestType is null)
                return;

            var generatedCode = start.Compilation.GetTypeByMetadataName("System.CodeDom.Compiler.GeneratedCodeAttribute");
            start.RegisterOperationAction(operation =>
                AnalyzeCreation(operation, requestType, generatedCode), OperationKind.ObjectCreation);
        });
    }

    private static void AnalyzeCreation(
        OperationAnalysisContext context, INamedTypeSymbol requestType, INamedTypeSymbol? generatedCode)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (!SymbolEqualityComparer.Default.Equals(creation.Constructor?.ContainingType, requestType))
            return;

        var containingType = context.ContainingSymbol.ContainingType;
        if (SymbolEqualityComparer.Default.Equals(containingType, requestType) || IsGeneratedFactory(containingType, generatedCode))
            return;

        context.ReportDiagnostic(Diagnostic.Create(GeneratedFactoryRequired, creation.Syntax.GetLocation()));
    }

    private static bool IsGeneratedFactory(INamedTypeSymbol? type, INamedTypeSymbol? generatedCode)
    {
        if (type is null || generatedCode is null)
            return false;

        foreach (var attribute in type.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, generatedCode) &&
                attribute.ConstructorArguments.Length == 2 &&
                attribute.ConstructorArguments[0].Value is GeneratorName)
                return true;
        }

        return false;
    }

}
