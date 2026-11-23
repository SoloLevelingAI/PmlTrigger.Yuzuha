using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.PmlTwin;

public static partial class PmlTwinFeatures
{
    private const int FunctionFileLimit = 1024 * 1024;
    private const int FunctionScanLimit = 12_000;

    private static CoreData RunInstalledPmlTools(FeatureContext context, CoreData request, IEvarsIntegration integration)
    {
        var command = request.Children().Single();
        return command.Name.LocalName == "authorize" ? AuthorizeFunction(context, command) : UseInstalledEvars(context, command, integration);
    }

    private static CoreData UseInstalledEvars(FeatureContext context, CoreData command, IEvarsIntegration integration)
    {
        var path = InstalledEvarsChange.Open(context, Value(command, "evars"));
        var product = (string?)command.Field("product");
        var library = (string?)command.Field("library");
        var uiDirectory = (string?)command.Field("uiDirectory");
        return command.Name.LocalName switch
        {
            "environment" => new CoreData("result", InstalledEvarsChange.Inspect(path, product).Describe()),
            "functions" => DiscoverFunctions(context, InstalledEvarsChange.Inspect(path, product), command),
            "evars-preview" => InstalledEvarsChange.Preview(context, path, library, uiDirectory, product, integration),
            "evars-apply" => InstalledEvarsChange.Apply(context, path, library, uiDirectory, product,
                Value(command, "expectedSha256"), Value(command, "proposedSha256"), integration),
            _ => throw new InvalidDataException("未知的 PML 工具操作。")
        };
    }

    /// <summary>Uses the same discovered rows for function authorization and knowledge lookup.</summary>
    private static CoreData DiscoverFunctions(FeatureContext context, PmlEnvironment environment, CoreData command)
    {
        var registry = ReadFunctionRegistry(context);
        var paths = FunctionFiles(context, registry, environment).Take(FunctionScanLimit + 1).ToArray();
        var query = Value(command, "query").Trim().TrimStart('!');
        var limit = (int?)command.Field("limit") ?? 25;
        var inventory = ReadFunctionInventory(context, registry, paths.Take(FunctionScanLimit), query, limit);
        return new CoreData("result", new CoreField("examined", Math.Min(paths.Length, FunctionScanLimit)),
            new CoreField("encrypted", inventory.Encrypted), new CoreField("skipped", inventory.Skipped),
            new CoreField("matches", inventory.Matches), new CoreField("truncated", paths.Length > FunctionScanLimit || inventory.Matches > limit),
            environment.Describe(), inventory.Rows);
    }

    private static CoreData ReadFunctionRegistry(FeatureContext context) =>
        new("registry", LoadRegistry(context.FilePath(FunctionRegistryPath)).Elements("function").Select(DescribeFunction));

    private static IEnumerable<string> FunctionFiles(FeatureContext context, CoreData registry, PmlEnvironment environment)
    {
        var registered = registry.Children("function").Select(entry => context.FilePath(Value(entry, "source")));
        var installed = environment.Roots.Where(root => root.State == "available").SelectMany(root => EnumerateFunctions(root.Path));
        return registered.Concat(installed).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateFunctions(string path)
    {
        try
        {
            var root = new FeatureContext(path);
            return Directory.EnumerateFiles(root.Root, "*.pmlfnc", new EnumerationOptions
            {
                RecurseSubdirectories = true, MaxRecursionDepth = 24,
                AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true
            });
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error)) { return []; }
    }

    private static FunctionInventory ReadFunctionInventory(FeatureContext context, CoreData registry, IEnumerable<string> paths, string query, int limit)
    {
        var inventory = new FunctionInventory();
        var registered = registry.Children("function").ToLookup(entry => Value(entry, "name"), StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
            inventory.Add(ReadDiscoveredFunction(path), context, registered, query, limit);
        return inventory;
    }

    private static DiscoveredFunction? ReadDiscoveredFunction(string path)
    {
        try
        {
            var bytes = ReadBounded(path, FunctionFileLimit);
            return DescribeSource(path, bytes);
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error)) { return null; }
    }

    private static DiscoveredFunction DescribeSource(string path, byte[] bytes)
    {
        var algorithm = PmlSourceHeader.ReadAlgorithm(bytes);
        var name = Path.GetFileNameWithoutExtension(path);
        var digest = Sha256(bytes);
        return algorithm.Length != 0 && algorithm != "000"
            ? new DiscoveredFunction(name, path, digest, "encrypted", algorithm, "", "", 0)
            : ReadPlainFunction(name, path, digest, algorithm, bytes);
    }

    private static DiscoveredFunction ReadPlainFunction(string name, string path, string digest, string algorithm, byte[] bytes)
    {
        var text = LocalText.Decode(bytes).Content;
        var (sourceName, status, signature, summary, line) = ReadDeclaration(text, name);
        return new DiscoveredFunction(sourceName, path, digest, status, algorithm, signature, summary, line);
    }

    private static (string Name, string State, string Signature, string Summary, int Line) ReadDeclaration(string text, string name)
    {
        var declaration = Declaration.Match(text);
        if (!declaration.Success)
            return (name, "unknown", "", "", 0);
        var line = text.Take(declaration.Index).Count(character => character == '\n') + 1;
        return (declaration.Groups[1].Value, "plain", CleanExcerpt(declaration.Value.Trim(), 320), FunctionSummary(text), line);
    }

    private static string FunctionSummary(string text)
    {
        var comments = text.Split('\n', 33).Take(32).Select(line => line.Trim())
            .Where(line => line.StartsWith("$*", StringComparison.Ordinal) || line.StartsWith("-- ", StringComparison.Ordinal))
            .Select(line => line[2..].Trim()).Where(line => line.Length > 0).Take(3);
        return CleanExcerpt(string.Join(" ", comments), 360);
    }

    private static string CleanExcerpt(string value, int limit) =>
        new(value.Take(limit).Where(XmlConvert.IsXmlChar).ToArray());

    private static CoreData AuthorizeFunction(FeatureContext context, CoreData command)
    {
        var name = Value(command, "name");
        var allowed = (bool)command.Field("allowInvoke")!;
        var entry = UpdateRegistry(context, registry => UpdateFunctionPermission(context, registry, command, name, allowed));
        return new CoreData("result", DescribeFunction(entry));
    }

    private static XElement UpdateFunctionPermission(FeatureContext context, XElement registry, CoreData command, string name, bool allowed)
    {
        var entry = RequireFunctionRevision(registry, name, Value(command, "revision"));
        VerifyPermissionSource(context, entry, allowed);
        entry.SetAttributeValue("allowInvoke", allowed);
        return entry;
    }

    private static void VerifyPermissionSource(FeatureContext context, XElement entry, bool allowed)
    {
        if (!allowed)
            return;
        var source = RequirePmlSource(context.FilePath(Value(entry, "source")));
        var file = ReadDiscoveredFunction(source);
        if (file is not { Encryption: "plain" } || file.Digest != Value(entry, "sha256"))
            throw new InvalidDataException("须先登记并审阅当前明文来源，才能保存授权。");
    }

    private sealed record DiscoveredFunction(string Name, string Path, string Digest, string Encryption, string Algorithm,
        string Signature, string Summary, int Line)
    {
        public bool Matches(string query, CoreData? entry) =>
            new[] { Name, Path, Signature, Summary, AuthorizedNote(entry), entry is null ? "" : Value(entry, "displayName") }
                .Any(text => text.Contains(query, StringComparison.OrdinalIgnoreCase));

        public CoreData? FindRegistration(FeatureContext context, IEnumerable<CoreData> candidates) =>
            candidates.FirstOrDefault(item => context.FilePath(Value(item, "source")).Equals(Path, StringComparison.OrdinalIgnoreCase));

        private string AuthorizedNote(CoreData? entry) =>
            entry is not null && Value(entry, "sha256") == Digest ? (string?)entry.Child("note") ?? "" : "";

        public CoreData Describe(CoreData? entry)
        {
            var (authorization, allowed) = ReadAuthorization(entry);
            return new CoreData("function", new CoreField("name", Name), new CoreField("source", Path),
                new CoreField("sha256", Digest), new CoreField("line", Line), new CoreField("encryption", Encryption),
                new CoreField("algorithm", Algorithm), new CoreField("allowInvoke", allowed), new CoreField("authorization", authorization),
                entry?.Field("revision"),
                entry is null ? null : new CoreField("registrySource", Value(entry, "source")),
                entry is null ? null : new CoreField("displayName", Value(entry, "displayName")),
                entry?.Children("parameter"),
                new CoreData("signature", Signature), new CoreData("summary", Summary),
                new CoreData("note", AuthorizedNote(entry)));
        }

        private (string State, bool Allowed) ReadAuthorization(CoreData? entry)
        {
            if (entry is null)
                return ("unregistered", false);
            if (Value(entry, "sha256") != Digest)
                return ("source-changed", false);
            var allowed = (bool?)entry.Field("allowInvoke") == true && Encryption == "plain";
            return (allowed ? "allowed" : "denied", allowed);
        }
    }

    private sealed class FunctionInventory
    {
        public int Encrypted { get; private set; }
        public int Skipped { get; private set; }
        public int Matches { get; private set; }
        public List<CoreData> Rows { get; } = [];

        public void Add(DiscoveredFunction? function, FeatureContext context, ILookup<string, CoreData> registered, string query, int limit)
        {
            if (function is null)
            {
                Skipped++;
                return;
            }
            Encrypted += function.Encryption == "encrypted" ? 1 : 0;
            var entry = function.FindRegistration(context, registered[function.Name]);
            if (function.Matches(query, entry))
                AddMatch(function, entry, limit);
        }

        private void AddMatch(DiscoveredFunction function, CoreData? entry, int limit)
        {
            Matches++;
            if (Rows.Count < limit)
                Rows.Add(function.Describe(entry));
        }
    }
}
