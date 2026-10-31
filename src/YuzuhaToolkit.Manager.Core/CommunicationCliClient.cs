using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PmlTwin.Phayton;
using YuzuhaToolkit.Communication;

namespace YuzuhaToolkit.Manager;

/// <summary>明确的通信程序、开发 host 及资源预算；不经 shell 查找程序。</summary>
public sealed record CommunicationCliOptions(string ExecutablePath, string? DotnetHostPath = null,
    TimeSpan? Timeout = null, int MaxOutputCharacters = CommunicationProtocol.MaxCharacters);

/// <summary>管理器通过单次 CLI 子进程通信，不直接引用或持有 AVEVA Bridge。</summary>
public sealed class CommunicationCliClient
{
    private readonly CommunicationCliOptions options;
    private readonly string fingerprint;
    private readonly string protocolArgument;
    private readonly Func<string, bool> permitted;
    private readonly bool worker;

    /// <summary>锁定所选程序身份；开发 DLL 必须提供明确的 dotnet host。</summary>
    public CommunicationCliClient(CommunicationCliOptions options) : this(options, "--manager-protocol", CommunicationProtocol.IsCommunicationOperation, false) { }

    internal CommunicationCliClient(CommunicationCliOptions options, string[] allowedOperations)
        : this(options, "--manager-plugin-protocol", new HashSet<string>(allowedOperations, StringComparer.Ordinal).Contains, true) { }

    private CommunicationCliClient(CommunicationCliOptions options, string argument, Func<string, bool> permit, bool isWorker)
    {
        if (!Path.IsPathFullyQualified(options.ExecutablePath)) throw new ArgumentException("通信 CLI 必须使用绝对路径。");
        if (options.MaxOutputCharacters is < 1024 or > 16 * 1024 * 1024 ||
            options.Timeout is { } timeout && (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromHours(1)))
            throw new ArgumentException("通信预算无效。");
        if (!new[] { ".exe", ".dll" }.Contains(Path.GetExtension(options.ExecutablePath), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("通信入口必须是受管 EXE 或开发 DLL。");
        if (options.ExecutablePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
            (options.DotnetHostPath is null || !Path.IsPathFullyQualified(options.DotnetHostPath) || !File.Exists(options.DotnetHostPath)))
            throw new ArgumentException("开发 DLL 需要存在的绝对 dotnet host 路径。");
        this.options = options;
        protocolArgument = argument; permitted = permit; worker = isWorker;
        using var file = File.OpenRead(options.ExecutablePath);
        fingerprint = Convert.ToHexString(SHA256.HashData(file));
    }

    /// <summary>从显式环境覆盖或当前安装包解析通信 CLI，不启动进程。</summary>
    public static CommunicationCliClient FromEnvironment()
    {
        var path = Environment.GetEnvironmentVariable("YUZUHA_COMMUNICATION_CLI");
        if (string.IsNullOrWhiteSpace(path)) path = Path.Combine(Workspace.UserDataPaths.InstallRoot,
            "runtime", "cli", "PmlTwin.Phayton.Cli.exe");
        return new(new(path, Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")));
    }

    /// <summary>发送一次有界请求；任何传输失败均不会自动重发。</summary>
    public async Task<CommunicationResponse> InvokeAsync(string operationId, JsonElement input,
        OperationContext context, CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var request = new CommunicationRequest(CommunicationProtocol.Version, requestId, operationId, input, context);
        var started = false;
        var overflow = 0;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Timeout ?? TimeSpan.FromSeconds(30));
        using var process = new Process();
        CommunicationResponse Failure(int exit, string code, string message) => new(CommunicationProtocol.Version,
            requestId, OperationResult.Fail(operationId, exit, code, message + " RequestId=" + requestId,
                mayHaveExecuted: started && (context.AllowExecution || worker && (context.AllowLaunch || context.AllowWrite))), "unavailable");
        try
        {
            deadline.Token.ThrowIfCancellationRequested();
            if (!permitted(operationId))
                return Failure(3, worker ? "UNDECLARED_PLUGIN_OPERATION" : "MANAGEMENT_OPERATION", worker ? "插件清单没有声明此操作。" : "此入口只允许通信操作，请使用管理器 CLI 执行管理任务。");
            using var lease = new FileStream(options.ExecutablePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (Convert.ToHexString(SHA256.HashData(lease)) != fingerprint)
                return Failure(5, "CLI_CHANGED", "通信 CLI 已变化，请重新核验版本并创建客户端。");
            var json = JsonSerializer.Serialize(request, CommunicationJsonContext.Default.CommunicationRequest);
            if (json.Length > CommunicationProtocol.MaxCharacters)
                return Failure(2, "REQUEST_TOO_LARGE", "通信请求超过预算。");
            process.StartInfo = new()
            {
                FileName = options.ExecutablePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    ? options.DotnetHostPath! : options.ExecutablePath,
                UseShellExecute = false, CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(options.ExecutablePath)!,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            if (options.ExecutablePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                process.StartInfo.ArgumentList.Add(options.ExecutablePath);
            process.StartInfo.ArgumentList.Add(protocolArgument);
            if (!process.Start()) return Failure(7, "CLI_START_FAILED", "通信进程未创建。");
            started = true;
            void Overflow() { Interlocked.Exchange(ref overflow, 1); deadline.Cancel(); }
            var output = ReadBoundedAsync(process.StandardOutput, options.MaxOutputCharacters, Overflow, deadline.Token);
            var diagnostics = ReadBoundedAsync(process.StandardError, Math.Min(options.MaxOutputCharacters, 256 * 1024), Overflow, deadline.Token);
            try
            {
                await process.StandardInput.WriteAsync(json.AsMemory(), deadline.Token).ConfigureAwait(false);
                process.StandardInput.Close();
                await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
                var text = await output.ConfigureAwait(false);
                await diagnostics.ConfigureAwait(false);
                var response = JsonSerializer.Deserialize(text, CommunicationJsonContext.Default.CommunicationResponse)
                    ?? throw new JsonException("缺少通信回执。");
                if (response.ProtocolVersion != CommunicationProtocol.Version || response.RequestId != requestId ||
                    response.Result is null || response.Result.OperationId != operationId || response.Result.SchemaVersion != 1 ||
                    response.Result.ExitCode != process.ExitCode || response.Result.Success != (response.Result.ExitCode == 0))
                    return Failure(7, "INVALID_CLI_RESPONSE", "通信回执的协议、请求标识或退出码不匹配。");
                return response;
            }
            finally
            {
                if (!process.HasExited)
                {
                    try { process.Kill(); }
                    catch (InvalidOperationException) when (process.HasExited) { }
                    // Kill requests termination; it does not wait for DLL handles to close.
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await process.WaitForExitAsync(cleanup.Token).ConfigureAwait(false);
                }
                try { await Task.WhenAll(output, diagnostics).ConfigureAwait(false); }
                catch (Exception error) when (error is OperationCanceledException or IOException) { }
            }
        }
        catch (OperationCanceledException)
        {
            return Failure(cancellationToken.IsCancellationRequested ? 8 : 7,
                overflow != 0 ? "CLI_OUTPUT_LIMIT" : cancellationToken.IsCancellationRequested ? "CANCELLED" : "CLI_TIMEOUT",
                overflow != 0 ? "通信输出超过预算。" : "通信等待已结束；已发出的 AVEVA 操作可能继续执行，不会自动重发。");
        }
        catch (JsonException) { return Failure(7, "INVALID_CLI_RESPONSE", "通信进程未返回有效的版本化 JSON 回执。"); }
        catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { return Failure(7, started ? "CLI_TRANSPORT_FAILED" : "CLI_START_FAILED", string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message); }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int limit, Action overflow, CancellationToken token)
    {
        var result = new StringBuilder();
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) > 0)
        {
            if (result.Length + read > limit) { overflow(); throw new OperationCanceledException(token); }
            result.Append(buffer, 0, read);
        }
        return result.ToString();
    }
}
