using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Core30.Core;

namespace Core30.PmlTwin;

internal sealed record LaunchProjectSdk(AvevaProduct Product, string Release, string Directory);

internal sealed record ProjectReadRequest(FeatureContext Workspace, LaunchProjectSdk Sdk, LaunchProjectSource Project,
    InstalledLaunchModule[] Modules, bool Refresh, string? LicenseOverride);

internal sealed record ProjectReaderDiagnostic(string Stage, string Code, string[]? Missing = null);

internal sealed record LaunchProjectMetadata(string State, XElement? Values = null, string Cache = "", string Detail = "",
    LaunchProjectSdk? Sdk = null, string? Flow = null, ProjectReaderDiagnostic? Diagnostic = null);

internal interface ILaunchProjectMetadataReader
{
    LaunchProjectMetadata Read(ProjectReadRequest request);
}

/// <summary>Reads AVEVA login candidates through an installed SDK using disposable system-file copies.</summary>
internal sealed class AvevaProjectMetadata(ProjectReaderRegistry readers, Func<LaunchProjectSdk, bool> canUseSdk,
    string executable) : ILaunchProjectMetadataReader
{
    private const int MaxSnapshotFileSize = 64 * 1024 * 1024;
    private const int MaxMetadataSize = 1024 * 1024;
    private const string LicenseVariable = "AVEVA_LICENSE_SERVER_LIST";
    private const string SdkVariables = "CORE30_PROJECT_READER_SDK_VARIABLES";
    private static readonly string[] CompatibleSdkVariables =
    [
        "PATH", "PROJECTS_DIR", "PMLLIB", "PMLUI", "PDMSUI", "PDMSEXE", "PDMSUSER", "PDMSWK",
        "AVEVA_DESIGN_EXE", "AVEVA_DESIGN_INSTALLED_DIR", "AVEVA_DESIGN_WORK", "AVEVA_DESIGN_USER",
        "AVEVA_PRODUCT", "PDMS_INSTALLED_DIR", "PDMS_NOCONSOLE", LicenseVariable
    ];
    private static readonly TimeSpan ReaderTimeout = TimeSpan.FromSeconds(8);

    public LaunchProjectMetadata Read(ProjectReadRequest request)
    {
        request.Workspace.Input?.Cancellation.ThrowIfCancellationRequested();
        var flow = readers.Resolve(request.Sdk);
        if (flow is null)
            return Unavailable(request.Sdk, null, new("selection", "unregistered-reader"),
                $"未登记 {request.Sdk.Product} {request.Sdk.Release} 的工程读取流程。");

        try
        {
            RequireReader(request, flow);
            return ReadCurrent(request, flow, executable);
        }
        catch (ProjectReaderFailure error)
        {
            return Unavailable(request.Sdk, flow.Id, error.Diagnostic, error.Message);
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error))
        {
            return Unavailable(request.Sdk, flow.Id, new("read", "read-failed"), error.Message);
        }
    }

    private static LaunchProjectMetadata Unavailable(LaunchProjectSdk sdk, string? flow, ProjectReaderDiagnostic diagnostic,
        string detail) => new("sdk-unavailable", Detail: detail, Sdk: sdk, Flow: flow, Diagnostic: diagnostic);

    private void RequireReader(ProjectReadRequest request, IProjectReaderFlow flow)
    {
        var sdk = request.Sdk;
        if (!canUseSdk(sdk))
            throw new ProjectReaderFailure(new("preflight", "unverified-installation"),
                $"项目读取器需要已登记的 {sdk.Product} 安装或标准 AVEVA 安装目录。");
        var missing = ReaderFiles(sdk.Directory, flow.Api, executable).Where(path => !File.Exists(path))
            .Select(path => Path.GetFileName(path)).Concat(flow.MissingDependencies(request)).ToArray();
        if (missing.Length != 0)
            throw new ProjectReaderFailure(new("preflight", "missing-dependency", missing),
                $"工程读取流程缺少依赖：{string.Join("、", missing)}。");
    }

    private static IEnumerable<string> ReaderFiles(string installation, ProjectReaderApi api, string executable) =>
        api.AssemblyNames.Select(name => Path.Combine(installation, name)).Prepend(executable);

    private static LaunchProjectMetadata ReadCurrent(ProjectReadRequest request, IProjectReaderFlow flow, string reader)
    {
        var files = ProjectFiles(request.Project);
        var cache = CachePath(request.Workspace, request.Sdk.Directory, request.Project);
        var fingerprint = Fingerprint(request, flow, files, reader);
        if (!request.Refresh && TryReadCache(cache, request.Project.Code, fingerprint) is { } cached)
            return new("project-metadata", cached, "hit", Sdk: request.Sdk, Flow: flow.Id);

        var metadata = ReadSnapshot(request, flow, reader, files);
        request.Workspace.Input?.Cancellation.ThrowIfCancellationRequested();
        if (!files.All(file => file.IsCurrent()))
            return new("source-changed", Detail: "系统库读取期间发生变化。", Sdk: request.Sdk, Flow: flow.Id,
                Diagnostic: new("read", "source-changed"));
        metadata.SetAttributeValue("fingerprint", fingerprint);
        return new("project-metadata", metadata, TryStore(cache, metadata) ? "scanned" : "uncached", Sdk: request.Sdk, Flow: flow.Id);
    }

    private static LaunchSourceStamp[] ProjectFiles(LaunchProjectSource project)
    {
        var prefix = project.Code.ToLowerInvariant();
        return new[] { project.SystemDatabase, Path.Combine(project.Directory, prefix + "com"), Path.Combine(project.Directory, prefix + "mis") }
            .Select(path => LaunchSourceStamp.Read(path, directory: false)).ToArray();
    }

    private static XElement ReadSnapshot(ProjectReadRequest request, IProjectReaderFlow flow, string reader, LaunchSourceStamp[] files)
    {
        var cancellation = request.Workspace.Input?.Cancellation ?? default;
        cancellation.ThrowIfCancellationRequested();
        var temporary = Directory.CreateTempSubdirectory("core30-aveva-project-");
        try
        {
            var code = request.Project.Code;
            var copy = temporary.CreateSubdirectory(code.ToLowerInvariant() + "000");
            foreach (var file in files.Where(file => file.Exists))
                CopyReadOnly(file, copy.FullName, cancellation);

            var environment = new ProjectReaderEnvironment(request.Sdk.Directory, code, temporary.FullName, copy.FullName);
            var session = new ProjectReaderSession(environment, () => StartReader(reader, request, environment, flow),
                start => RunReader(start, environment.Scratch, cancellation));
            return ValidateMetadata(flow.Read(session), code);
        }
        finally { RemoveSnapshot(temporary); }
    }

    private static XElement RunReader(ProcessStartInfo start, string scratch, CancellationToken cancellation)
    {
        int exit;
        try { (exit, _, _) = WindowsDetachedProcess.RunIsolatedWorker(start, ReaderTimeout, cancellation); }
        catch (TimeoutException)
        {
            throw ReaderFailure(scratch, "timeout", "超时");
        }
        var output = Path.Combine(scratch, "metadata.xml");
        return exit == 0 && File.Exists(output) ? ReadMetadata(output)
            : throw ReaderFailure(scratch, "worker-failed", $"退出码 {exit}");
    }

    private static ProjectReaderFailure ReaderFailure(string scratch, string code, string reason)
    {
        var stageFile = Path.Combine(scratch, "stage.txt");
        var stage = File.Exists(stageFile) ? File.ReadAllText(stageFile).Trim() : "worker";
        var step = stage switch
        {
            "sdk-load" => "SDK 加载",
            "initialization" => "读取器初始化",
            "project" => "工程读取",
            _ => "读取进程"
        };
        var error = Path.Combine(scratch, "reader-error.txt");
        var detail = File.Exists(error) ? File.ReadAllText(error) : "";
        return new(new(stage, code), $"AVEVA {step}{reason}。{detail}");
    }

    private static void CopyReadOnly(LaunchSourceStamp file, string directory, CancellationToken cancellation)
    {
        if (file.Size > MaxSnapshotFileSize)
            throw new InvalidDataException($"项目系统文件超过 64 MiB：{Path.GetFileName(file.Path)}");
        var target = Path.Combine(directory, Path.GetFileName(file.Path));
        using var source = File.Open(file.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (source.Length != file.Size)
            throw new IOException("项目系统文件在读取期间发生变化。");
        using (var output = File.Open(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            source.CopyToAsync(output, cancellation).GetAwaiter().GetResult();
        File.SetAttributes(target, FileAttributes.ReadOnly);
    }

    internal static ProcessStartInfo StartReader(string executable, ProjectReadRequest request,
        ProjectReaderEnvironment environment, IProjectReaderFlow flow)
    {
        var api = flow.Api;
        var command = new ProcessStartInfo(executable) { WorkingDirectory = environment.Scratch, UseShellExecute = false, CreateNoWindow = true };
        string[] descriptor = [environment.Installation, environment.Project, api.StandaloneAssembly, api.StandaloneType,
            api.DatabaseAssembly, api.ProjectType];
        foreach (var argument in descriptor.Concat(request.Modules.Select(module => module.Name)))
            command.ArgumentList.Add(argument);
        SetReaderEnvironment(command, environment, flow, request.LicenseOverride);
        return command;
    }

    private static void SetReaderEnvironment(ProcessStartInfo command, ProjectReaderEnvironment environment,
        IProjectReaderFlow flow, string? licenseOverride)
    {
        foreach (var key in LaunchProjectEnvironment.ReadEnvironment().Keys)
            command.Environment.Remove(key);
        if (licenseOverride is not null)
            command.Environment[LicenseVariable] = licenseOverride;
        command.Environment["PATH"] = environment.Installation + ";" + command.Environment["PATH"];
        command.Environment["PDMSEXE"] = environment.Binary;
        command.Environment["PDMSUSER"] = command.Environment["PDMSWK"] = environment.Work;
        command.Environment["PROJECTS_DIR"] = environment.Work;
        command.Environment[environment.Project + "000"] = environment.Database + "\\";
        command.Environment["PMLLIB"] = Path.Combine(environment.Installation, "pmllib") + "\\";
        ApplyFlowVariables(command, environment, flow);
    }

    private static void ApplyFlowVariables(ProcessStartInfo command, ProjectReaderEnvironment environment, IProjectReaderFlow flow)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        flow.Configure(variables, environment);
        foreach (var (name, value) in variables)
            command.Environment[name] = value;
        string[] projectVariables = [environment.Project + "000", environment.Project + "000ID", environment.Project + "MAC"];
        command.Environment[SdkVariables] = string.Join('\n', CompatibleSdkVariables.Concat(projectVariables)
            .Concat(variables.Keys).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static void RemoveSnapshot(DirectoryInfo temporary)
    {
        try
        {
            foreach (var file in temporary.EnumerateFiles("*", SearchOption.AllDirectories))
                file.IsReadOnly = false;
            temporary.Delete(recursive: true);
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error)) { }
    }

    private static string CachePath(FeatureContext context, string installation, LaunchProjectSource project)
    {
        var user = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var key = FileContent.Sha256(Encoding.UTF8.GetBytes($"{user}\0{installation}\0{project.SystemDatabase}"));
        return context.FilePath($".core30/launch-catalog/project-{key}.xml");
    }

    private static string Fingerprint(ProjectReadRequest request, IProjectReaderFlow flow, LaunchSourceStamp[] projectFiles, string reader)
    {
        var binaries = ReaderFiles(request.Sdk.Directory, flow.Api, reader)
            .Select(path => LaunchSourceStamp.Read(path, directory: false));
        return FileContent.Sha256(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version = 3, Flow = flow.Id, FlowRevision = flow.GetType().Module.ModuleVersionId, flow.Api,
            request.Sdk.Release, Files = projectFiles.Concat(binaries),
            request.Modules, request.LicenseOverride
        }));
    }

    private static XElement? TryReadCache(string path, string code, string fingerprint)
    {
        try
        {
            var cached = ValidateMetadata(ReadMetadata(path), code);
            return (string?)cached.Attribute("fingerprint") == fingerprint ? cached : null;
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error)) { return null; }
    }

    private static XElement ReadMetadata(string path) =>
        XElement.Parse(Encoding.UTF8.GetString(FileContent.ReadBounded(path, MaxMetadataSize)).TrimStart('\uFEFF'));

    private static XElement ValidateMetadata(XElement metadata, string code) =>
        metadata.Name == "metadata" && (string?)metadata.Attribute("project") == code && (bool?)metadata.Attribute("loggedIn") == false
            ? metadata : throw new InvalidDataException("AVEVA 项目读取回执无效。");

    private static bool TryStore(string path, XElement metadata)
    {
        var pending = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            metadata.Save(pending);
            File.Move(pending, path, overwrite: true);
            return true;
        }
        catch (Exception error) when (CoreRunner.IsOperationalError(error)) { return false; }
        finally
        {
            try { File.Delete(pending); }
            catch (Exception error) when (CoreRunner.IsOperationalError(error)) { }
        }
    }

    private sealed class ProjectReaderFailure(ProjectReaderDiagnostic diagnostic, string message) : IOException(message)
    {
        public ProjectReaderDiagnostic Diagnostic { get; } = diagnostic;
    }
}
