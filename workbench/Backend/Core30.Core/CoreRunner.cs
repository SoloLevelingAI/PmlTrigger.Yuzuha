using System.Diagnostics;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace Core30.Core;

public sealed class CoreRunner(
    IEnumerable<IFeature> features,
    IEnumerable<IRunLogProvider> logProviders,
    Action<string>? onDiagnostic = null)
{
    private readonly Dictionary<(string Id, string Contract), IFeature> _features =
        features.ToDictionary(feature => (feature.Id, ContractName(feature)));
    private readonly ImplementationCatalog<string, IRunLogProvider> _logs = new(
        logProviders.Select(provider => new ImplementationRegistration<string, IRunLogProvider>(provider.Name, provider)),
        StringComparer.Ordinal);
    private readonly long _processStartTicks = ReadProcessStart();
    private static readonly string? EntryPoint = Assembly.GetEntryAssembly()?.GetName().Name;

    public IEnumerable<IFeature> Features => _features.Values.OrderBy(feature => feature.Id)
        .ThenBy(feature => feature.Version);

    public XElement Run(string featureId, string workspace, Stream xmlInput) =>
        Run(featureId, new FeatureContext(workspace), xmlInput);

    internal XElement Run(string featureId, FeatureContext context, Stream xmlInput)
        => CoreXml.WriteReceipt(RunLogged(featureId, context, () =>
        {
            var request = XmlContracts.Read(xmlInput);
            var feature = ResolveFeature(featureId, request.Root!.Name.NamespaceName);
            using var schema = feature.OpenSchema();
            XmlContracts.Validate(request, schema, ContractName(feature), "request");
            var root = request.Root;
            var operation = feature.GetOperationName(root) ?? root.Elements().FirstOrDefault()?.Name.LocalName
                ?? (string?)root.Attribute("mode") ?? "request";
            return (feature, operation, new Func<XElement>(() => feature.Execute(context, root)));
        }), static result => result);

    public CoreReceipt<CoreData> Run(string workspace, CoreRequest request, RunInput? input = null) =>
        Run(new FeatureContext(workspace) { Input = input ?? new RunInput() }, request);

    internal CoreReceipt<CoreData> Run(FeatureContext context, CoreRequest request) =>
        RunLogged(request.Feature, context, () =>
        {
            var data = request.Data;
            var feature = ResolveFeature(request.Feature, data.Name.NamespaceName) as TypedFeature
                ?? throw new InvalidDataException("该模块通过外部 XML 接口提供此功能。");
            using var schema = feature.OpenSchema();
            CoreXml.Validate(data, schema, ContractName(feature));
            var operation = feature.OperationName?.Invoke(data) ?? data.Children().FirstOrDefault()?.Name.LocalName
                ?? (string?)data.Field("mode") ?? "request";
            return ((IFeature)feature, operation, new Func<CoreData>(() => feature.Execute(context, data)));
        });

    private CoreReceipt<T> RunLogged<T>(string featureId, FeatureContext context,
        Func<(IFeature Feature, string Operation, Func<T> Execute)> prepare)
    {
        RequireRegisteredId(featureId);
        var settings = RunSettings.Load(context);
        var runId = Guid.NewGuid().ToString("N");
        var log = OpenLog(settings.Log, context, runId);
        return Execute(featureId, settings.Source, log, runId, prepare);
    }

    private void RequireRegisteredId(string id)
    {
        if (!_features.Keys.Any(key => key.Id == id))
            throw new InvalidDataException("Feature 未登记或所属工具已关闭。");
    }

    private CoreReceipt<T> Execute<T>(string featureId, string settingsSource, IRunLog log, string runId,
        Func<(IFeature Feature, string Operation, Func<T> Execute)> prepare)
    {
        var entry = new RunEvent(DateTimeOffset.UtcNow, runId, featureId, "started", settingsSource)
        {
            ProcessId = Environment.ProcessId,
            StartTicks = _processStartTicks,
            EntryPoint = EntryPoint
        };
        var started = false;
        try
        {
            var (feature, operation, execute) = prepare();
            entry = entry with
            {
                Category = feature.ActivityCategory,
                Operation = operation
            };
            Record(log, entry);
            started = true;
            var result = execute();
            Record(log, Finished(entry, "completed", "ok"));
            return new CoreReceipt<T>(feature.Id, feature.Version, settingsSource, log.RelativePath, result);
        }
        catch (Exception error) when (IsOperationalError(error))
        {
            RecordFailure(log, entry, started, error);
            throw;
        }
    }

    private IFeature ResolveFeature(string id, string contract) =>
        _features.TryGetValue((id, contract), out var feature) ? feature
            : throw new InvalidDataException("Feature 与 XML 契约未登记或版本不匹配。");

    private IRunLog OpenLog(string name, FeatureContext context, string runId) =>
        _logs.Find(name) is { } provider ? provider.Open(context, runId)
            : throw new InvalidDataException($"日志实现 {name} 未登记。");

    private void RecordFailure(IRunLog log, RunEvent entry, bool started, Exception error)
    {
        if (!started)
            Record(log, entry);
        Record(log, Finished(entry, "failed", error.GetType().Name));
    }

    private static RunEvent Finished(RunEvent entry, string phase, string detail) =>
        entry with { At = DateTimeOffset.UtcNow, Phase = phase, Detail = detail };

    private static long ReadProcessStart()
    {
        using var process = Process.GetCurrentProcess();
        return process.StartTime.ToUniversalTime().Ticks;
    }

    private void Record(IRunLog log, RunEvent entry)
    {
        try
        {
            log.Record(entry);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            onDiagnostic?.Invoke($"无法写入过程日志（{error.GetType().Name}）。");
        }
    }

    public static bool IsOperationalError(Exception error) => error is InvalidDataException or IOException
        or UnauthorizedAccessException or ArgumentException or XmlException
        or System.Xml.Schema.XmlSchemaException or System.Text.Json.JsonException
        or InvalidOperationException or TimeoutException or System.ComponentModel.Win32Exception;

    private static string ContractName(IFeature feature) => $"urn:core30:{feature.Id}:{feature.Version}";
}
