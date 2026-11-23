using System.Reflection;
using System.Runtime.Loader;
using System.Xml.Linq;

namespace Core30.Core;

/// <summary>Reads module metadata without executing code. Only an enabled registration can activate its assembly.</summary>
internal static class InstalledModules
{
    private const string Contract = "urn:core30:module:v1";

    public static IEnumerable<InstalledModule> Manifests(string? root)
    {
        if (root is null || !Directory.Exists(root))
            return [];
        var context = new FeatureContext(root);
        return Directory.EnumerateDirectories(context.Root).Order(StringComparer.Ordinal)
            .Select(Path.GetFileName).OfType<string>()
            .Select(id => (Id: id, Path: context.FilePath(Path.Combine(id, "module.xml"))))
            .Where(manifest => File.Exists(manifest.Path))
            .Select(manifest => ReadModuleManifest(context, manifest.Id, manifest.Path));
    }

    private static InstalledModule ReadModuleManifest(FeatureContext context, string directory, string manifest)
    {
        var bytes = LoadConfigurationChange.Read(manifest);
        var descriptor = ReadDescriptor(bytes);
        var assembly = ResolveAssemblyPath(context, directory, descriptor);
        var registration = new ToolRegistration(descriptor.Id, descriptor.Title, descriptor.Category,
            () => ReadModule(descriptor.Id, assembly, ReadFeatures), EnabledByDefault: false)
        {
            Page = descriptor.Page,
            ModuleManifestPath = manifest,
            LoadCommands = () => File.Exists(assembly) ? ReadModule(descriptor.Id, assembly, ReadCommands) : []
        };
        return new InstalledModule(registration, manifest, FileContent.Sha256(bytes), assembly);
    }

    private static Descriptor ReadDescriptor(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var schema = EmbeddedFile.Open(typeof(InstalledModules).Assembly, "module.xsd");
        var document = XmlContracts.Read(input);
        XmlContracts.Validate(document, schema, Contract, "module");
        var node = document.Root!;
        var page = node.Element(XName.Get("page", Contract));
        return new Descriptor(Attribute(node, "id"), Attribute(node, "title"),
            Attribute(node, "category"), Attribute(node, "assembly"),
            page is null ? null : new ToolPage(Attribute(page, "title"), Attribute(page, "feature"), Attribute(page, "mode")));
    }

    private static string Attribute(XElement node, string name) => node.Attribute(name)!.Value.Trim();

    private static string ResolveAssemblyPath(FeatureContext context, string directory, Descriptor descriptor)
    {
        if (!StringComparer.Ordinal.Equals(descriptor.Id, directory))
            throw new InvalidDataException($"模块 ID {descriptor.Id} 须与安装目录 {directory} 一致。");
        return context.FilePath(Path.Combine(directory, descriptor.AssemblyFile));
    }

    private static IFeature[] ReadFeatures(Type entry) => ((IToolModule)Activator.CreateInstance(entry)!).GetFeatures().ToArray();

    private static IReadOnlyList<ToolCommand> ReadCommands(Type entry) =>
        typeof(IToolCommandProvider).IsAssignableFrom(entry)
            ? ((IToolCommandProvider)Activator.CreateInstance(entry)!).GetCommands()
            : [];

    private static T ReadModule<T>(string id, string path, Func<Type, T> read)
    {
        try
        {
            var context = new ModuleLoadContext(path);
            var assembly = context.LoadFromAssemblyPath(path);
            return read(FindEntry(assembly));
        }
        catch (Exception error) when (IsActivationError(error))
        {
            throw new InvalidDataException($"无法装配模块 {id}（{error.GetType().Name}）。", error);
        }
    }

    private static bool IsActivationError(Exception error) => CoreRunner.IsOperationalError(error) || error is
        BadImageFormatException or ReflectionTypeLoadException or TypeLoadException or TargetInvocationException
        or MissingMethodException;

    private static Type FindEntry(Assembly assembly)
    {
        var candidates = assembly.GetExportedTypes().Where(type => type.IsClass && !type.IsAbstract
            && !type.ContainsGenericParameters && typeof(IToolModule).IsAssignableFrom(type)).ToArray();
        return candidates is [var entry] ? entry
            : throw new InvalidDataException("模块程序集须公开一个 IToolModule 实现。");
    }

    private sealed record Descriptor(string Id, string Title, string Category, string AssemblyFile, ToolPage? Page);

    internal sealed record InstalledModule(ToolRegistration Registration, string ManifestPath,
        string ManifestSha256, string AssemblyPath);

    private sealed class ModuleLoadContext(string modulePath) : AssemblyLoadContext
    {
        private static readonly Assembly ContractAssembly = typeof(IToolModule).Assembly;
        private readonly AssemblyDependencyResolver _resolver = new(modulePath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == ContractAssembly.GetName().Name)
                return ContractAssembly;
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }

        protected override nint LoadUnmanagedDll(string name) =>
            _resolver.ResolveUnmanagedDllToPath(name) is { } path ? LoadUnmanagedDllFromPath(path) : nint.Zero;
    }
}
