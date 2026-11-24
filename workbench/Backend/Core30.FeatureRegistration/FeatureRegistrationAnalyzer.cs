using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Core30.FeatureRegistration;

/// <summary>Keeps opted-in feature registrations in the generated catalog.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class FeatureRegistrationAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor UseCatalog = new(
        "C30F003", "Register features in the feature catalog",
        "Declare the feature in FeatureCatalog.xml and bind its handler through FeatureCatalog.Bind",
        "Feature Catalog", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(UseCatalog);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilation =>
        {
            if (!compilation.Options.AdditionalFiles.Any(file =>
                string.Equals(Path.GetFileName(file.Path), "FeatureCatalog.xml", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var feature = compilation.Compilation.GetTypeByMetadataName("Core30.Core.Feature");
            var typedFeature = compilation.Compilation.GetTypeByMetadataName("Core30.Core.TypedFeature");

            compilation.RegisterOperationAction(operation =>
            {
                var type = operation.Operation.Type;
                if (type is not null && (SymbolEqualityComparer.Default.Equals(type, feature) ||
                    SymbolEqualityComparer.Default.Equals(type, typedFeature)))
                    operation.ReportDiagnostic(Diagnostic.Create(UseCatalog, operation.Operation.Syntax.GetLocation()));
            }, OperationKind.ObjectCreation);
        });
    }
}
