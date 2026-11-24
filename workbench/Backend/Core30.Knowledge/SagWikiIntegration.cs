using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Core30.Core;
using static Core30.Core.FileContent;

namespace Core30.Knowledge;

/// <summary>Deploys the external provider and runs its CLI against a user-selected knowledge folder.</summary>
internal sealed class SagWikiIntegration(FeatureContext home, ISagWikiProcess process)
{
    private const string ActiveVersion = "active-version.txt";
    private readonly SagWikiCli _cli = new(home, process);
    private static readonly TimeSpan VersionTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(8);

    public static CoreData Run(FeatureContext workspace, CoreData request, ISagWikiProcess process, ISagWikiReleaseFeed feed)
    {
        var home = OpenHome(workspace, (string?)request.Field("home"));
        var integration = new SagWikiIntegration(home, process);
        var operation = request.Children().Single();
        return operation.Name.LocalName == "deploy"
            ? integration.Deploy(operation, feed)
            : integration.Bridge(workspace, operation);
    }

    private CoreData Deploy(CoreData command, ISagWikiReleaseFeed feed)
    {
        RequireWindowsX64();
        var release = feed.Select(command).GetAwaiter().GetResult();
        using var gate = OpenGate();
        var current = SagWikiCli.ManagedVersion(home);
        var preview = (bool?)command.Field("preview") == true;
        if (current is not null && current >= release.Version && File.Exists(Runtime(current)))
            return DescribeCurrent(current, preview);
        var mirror = (string?)command.Field("gitee");
        var payload = GetInstaller(feed, release, mirror);
        return preview ? InstallReceipt(payload, "Verified") : Install(payload, current);
    }

    private SagWikiInstaller GetInstaller(ISagWikiReleaseFeed feed, SagWikiRelease release, string? mirror)
    {
        if (mirror is not null)
            SagWikiReleaseFeed.ValidateMirror(mirror);

        var cache = home.FilePath($"packages/{release.Version}/{release.Digest.ToLowerInvariant()}.exe");
        if (File.Exists(cache))
        {
            try
            {
                var bytes = ReadBounded(cache, SagWikiReleaseFeed.MaxInstallerBytes);
                if (Sha256(bytes).Equals(release.Digest, StringComparison.OrdinalIgnoreCase))
                    return new SagWikiInstaller(release, "已校验的本地缓存", bytes);
            }
            catch (InvalidDataException)
            {
                // An invalid cached file never enters the installation path.
            }
        }

        var payload = feed.Download(release, mirror).GetAwaiter().GetResult();
        Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
        StoreAtomically(cache, payload.Bytes);
        return payload;
    }

    private CoreData DescribeCurrent(Version version, bool preview)
    {
        var result = new CoreData("result", new CoreField("state", "Current"), new CoreField("version", version),
            new CoreField("cli", Runtime(version)));
        return preview ? result : WithIndexReadiness(result, Runtime(version));
    }

    private CoreData Install(SagWikiInstaller installer, Version? previous)
    {
        var setup = home.FilePath($".setup-{Guid.NewGuid():N}.exe");
        var app = home.FilePath("app");
        try
        {
            File.WriteAllBytes(setup, installer.Bytes);
            process.Run(new ProcessStartInfo(setup) { Arguments = $"/S /D={app}", WorkingDirectory = home.Root }, TimeSpan.FromMinutes(5));
            var native = FindBundledCli();
            RequireVersion(native, installer.Release.Version);
            return Activate(installer, native, previous);
        }
        finally { File.Delete(setup); }
    }

    private CoreData Activate(SagWikiInstaller installer, string native, Version? previous)
    {
        var version = installer.Release.Version;
        var runtime = Runtime(version);
        Directory.CreateDirectory(Path.GetDirectoryName(runtime)!);
        StoreAtomically(runtime, ReadBounded(native, 64 * 1024 * 1024));
        if (previous is not null && File.Exists(Runtime(previous)))
            InvokeCli(Runtime(previous), ["service", "stop"]);
        StoreAtomically(home.FilePath(ActiveVersion), Encoding.ASCII.GetBytes(version.ToString()));
        var result = InstallReceipt(installer, "Installed");
        result.Add(new CoreField("cli", runtime));
        return previous is null ? result : WithIndexReadiness(result, runtime);
    }

    /// <summary>Lets the new service settle its registered indexes before handing back the update receipt.</summary>
    private CoreData WithIndexReadiness(CoreData result, string executable, bool waitForReady = true)
    {
        try { result.Add(ReadIndexReadiness(executable, waitForReady)); }
        catch (Exception error) when (error is KeyNotFoundException || CoreRunner.IsOperationalError(error))
        {
            result.Add(new CoreData("indexes", new CoreField("state", "Unavailable"),
                new CoreField("reason", error.GetType().Name)));
        }
        return result;
    }

    private CoreData ReadIndexReadiness(string executable, bool waitForReady)
    {
        var timer = Stopwatch.StartNew();
        var timeout = TimeSpan.FromSeconds(waitForReady ? 6 : 15);
        while (true)
        {
            var json = InvokeCli(executable, ["list", "--json"], timeout);
            using var catalog = JsonDocument.Parse(json);
            var states = catalog.RootElement.EnumerateArray().Select(root => root.GetProperty("status").GetString()).ToArray();
            if (!waitForReady || timer.Elapsed >= TimeSpan.FromSeconds(12) || !states.Any(IsIndexPending))
                return new CoreData("indexes", new CoreField("state", states.All(state => state == "ready") ? "Ready" : "NotReady"),
                    new CoreData("json", XmlText(json)));
            Thread.Sleep(400);
        }
    }

    private static bool IsIndexPending(string? state) => state is not (null or "ready" or "error" or "failed");

    private CoreData Bridge(FeatureContext workspace, CoreData command)
    {
        if (command.Name.LocalName == "status")
            return Status(FindCli());
        using var gate = OpenGate();
        var executable = FindCli() ?? throw new InvalidDataException("未找到 SAG-Wiki CLI。请先运行 sag-deploy。");
        var root = new FeatureContext(LocalSource.Resolve(workspace, (string)command.Field("root")!)).Root;
        return command.Name.LocalName == "index" ? Index(executable, root) : Search(executable, root, command);
    }

    private CoreData Status(string? executable) => executable is null
        ? new CoreData("result", new CoreField("state", "Missing"), new CoreField("data", DataDirectory))
        : WithIndexReadiness(new CoreData("result", new CoreField("state", "Available"), new CoreField("cli", executable),
            new CoreField("version", InvokeCli(executable, ["--version"], VersionTimeout)), new CoreField("data", DataDirectory),
            new CoreData("output", XmlText(InvokeCli(executable, ["status"], StatusTimeout)))), executable, waitForReady: false);

    private CoreData Index(string executable, string root) =>
        JsonResult("index", _cli.Index(executable, root));

    private CoreData Search(string executable, string root, CoreData request)
    {
        var semantic = (bool?)request.Field("semantic") ?? false;
        var limit = (int?)request.Field("limit") ?? 10;
        var query = (string)request.Field("query")!;
        var json = _cli.Search(executable, root, query, limit, semantic);
        return JsonResult(semantic ? "semantic" : "literal", json);
    }

    private string InvokeCli(string executable, IEnumerable<string> arguments, TimeSpan? timeout = null) =>
        _cli.Run(executable, arguments, timeout);

    private void RequireVersion(string executable, Version version)
    {
        if (!InvokeCli(executable, ["--version"]).Equals($"sag {version}", StringComparison.Ordinal))
            throw new InvalidDataException($"外部安装的 SAG-Wiki CLI 版本与发布资产 {version} 不一致。");
    }

    private string FindBundledCli() => new[] { "app/sag.exe", "app/resources/sag.exe", "app/bin/sag.exe" }
        .Select(home.FilePath).FirstOrDefault(File.Exists)
        ?? throw new FileNotFoundException("已运行外部 SAG-Wiki 安装器，指定目录下未发现 sag.exe。");

    private string? FindCli() => SagWikiCli.FindInstalled(home);

    private string Runtime(Version version) => home.FilePath($"runtime/{version}/sag.exe");
    private string DataDirectory => _cli.DataDirectory;
    private FileStream OpenGate() => File.Open(home.FilePath("sag-wiki.lock"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);

    private static FeatureContext OpenHome(FeatureContext workspace, string? configured)
    {
        var local = new FeatureContext(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        var directory = configured is null ? local.FilePath("AVEVA-Toolbox/SAG-Wiki") : LocalSource.Resolve(workspace, configured);
        return new FeatureContext(Directory.CreateDirectory(directory).FullName);
    }

    private static CoreData JsonResult(string mode, string output)
    {
        using var json = JsonDocument.Parse(output);
        return new CoreData("result", new CoreField("state", "Completed"), new CoreField("mode", mode),
            new CoreData("json", XmlText(json.RootElement.GetRawText())));
    }

    private static CoreData InstallReceipt(SagWikiInstaller installer, string state) => new("result",
        new CoreField("state", state), new CoreField("source", installer.Source),
        new CoreField("version", installer.Release.Version), new CoreField("sha256", installer.Release.Digest));

    private static void StoreAtomically(string path, byte[] bytes)
    {
        var pending = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(pending, bytes);
            File.Move(pending, path, overwrite: true);
        }
        finally { File.Delete(pending); }
    }

    private static void RequireWindowsX64()
    {
        if (!OperatingSystem.IsWindows() || System.Runtime.InteropServices.RuntimeInformation.OSArchitecture
            != System.Runtime.InteropServices.Architecture.X64)
            throw new InvalidDataException("SAG-Wiki 外部安装器适用于 Windows x64。");
    }
}
