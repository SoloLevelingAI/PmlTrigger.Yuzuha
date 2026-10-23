using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace YuzuhaToolkit.CommandLine;

/// <summary>同一可执行程序的 CLI 入口；通过一个本程序 MCP 子进程复用实际工具、参数校验与会话状态。</summary>
public static class DualModeCommandLine
{
    /// <summary>处理 --cli 或帮助；其他参数留给原 MCP 宿主，不向其 stdout 写入说明。</summary>
    /// <param name="args">可执行程序收到的原始参数。</param>
    /// <param name="serviceName">帮助文本中的服务名称。</param>
    /// <param name="cancellationToken">取消整个 CLI 会话；不会自动重试已发送的工具请求。</param>
    /// <returns>CLI 退出码；null 表示继续启动 MCP stdio 服务。</returns>
    public static async Task<int?> TryRunAsync(string[] args, string serviceName, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0 || args[0] == "--mcp") return null;
        if (args[0] is "--help" or "-h" || args[0] == "--cli" && (args.Length == 1 || args[1] is "--help" or "-h"))
        {
            Console.WriteLine(serviceName + " · CLI / MCP");
            Console.WriteLine("""
                默认或 --mcp                         MCP stdio（stdout 仅 JSON-RPC）
                --cli list                         列出工具及真实 JSON Schema
                --cli web open                     启动/复用本地 HTML 管理器并打开浏览器
                --cli web open --no-browser        仅启动/检查本地宿主，输出地址，不执行 PML
                --cli call <name> --arguments <json> 调用一次工具
                --cli call <name> --file <path|->     从文件或 stdin 读取参数对象
                --cli batch --file <path|->          顺序执行 [{"name":"...","arguments":{}}]
                可追加 --timeout-seconds <1..3600>；默认每次请求 120 秒。
                batch 共用同一 MCP 连接，首次失败即停止；已发送请求不会自动重试。
                退出码：0 成功，1 工具失败，2 参数错误，124 取消或超时。
                """);
            return 0;
        }
        if (args[0] != "--cli") return null;
        if (args.Skip(1).SequenceEqual(new[] { "web", "open" }) || args.Skip(1).SequenceEqual(new[] { "web", "open", "--no-browser" }))
        {
            try
            {
                var url = await YuzuhaToolkit.Workspace.HtmlAppLauncher.OpenAsync(openBrowser: !args.Contains("--no-browser"), token: cancellationToken);
                Console.WriteLine(url); return 0;
            }
            catch (Exception error) when (error is IOException or ArgumentException or InvalidOperationException)
            { Console.Error.WriteLine(error.Message); return 1; }
        }

        using var interrupted = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler handler = (_, eventArgs) => { eventArgs.Cancel = true; interrupted.Cancel(); };
        Console.CancelKeyPress += handler;
        try
        {
            var arguments = args.Skip(1).ToList();
            var timeout = ExtractTimeout(arguments);
            if (arguments.Count == 0) throw new ArgumentException("缺少 CLI 命令。");
            var command = arguments[0];
            var requests = await ReadRequestsAsync(arguments, interrupted.Token).ConfigureAwait(false);
            var process = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve the current executable.");
            var serverArguments = new List<string>();
            if (Path.GetFileNameWithoutExtension(process).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                serverArguments.Add(Environment.GetCommandLineArgs()[0]);
                // The SDK's Windows command launcher handles the executable name through PATH.
                // A space-containing full dotnet host path otherwise becomes a shell command token.
                process = "dotnet";
            }
            serverArguments.Add("--mcp");
            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = process,
                Arguments = serverArguments,
                Name = serviceName + "-cli",
                WorkingDirectory = Environment.CurrentDirectory,
                StandardErrorLines = line => Console.Error.WriteLine(line)
            });
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: interrupted.Token).ConfigureAwait(false);
            if (command == "list")
            {
                using var deadline = Deadline(timeout, interrupted.Token);
                var tools = await client.ListToolsAsync(cancellationToken: deadline.Token).ConfigureAwait(false);
                WriteJson(writer => JsonSerializer.Serialize(writer, new ListToolsResult { Tools = tools.Select(t => t.ProtocolTool).ToList() }, ProtocolInfo<ListToolsResult>()));
                return 0;
            }
            var outcomes = new List<(string Name, CallToolResult Result)>();
            var failed = false;
            var exitCode = 0;
            foreach (var request in requests)
            {
                using var deadline = Deadline(timeout, interrupted.Token);
                CallToolResult result;
                try { result = await client.CallToolAsync(request, deadline.Token).ConfigureAwait(false); }
                catch (OperationCanceledException)
                {
                    result = Failure("调用已取消或超时；该请求可能已经在 Host 执行，不会自动重试。");
                    exitCode = 124;
                }
                catch (Exception error) when (error is McpException or IOException)
                {
                    result = Failure(error.Message);
                }
                outcomes.Add((request.Name, result));
                if (ToolAuditFilter.IsFailure(result)) { failed = true; break; }
            }
            WriteJson(writer =>
            {
                if (command == "call") JsonSerializer.Serialize(writer, outcomes[0].Result, ProtocolInfo<CallToolResult>());
                else
                {
                    writer.WriteStartObject();
                    writer.WriteBoolean("success", !failed);
                    writer.WriteBoolean("stoppedOnError", failed);
                    writer.WriteNumber("completed", outcomes.Count);
                    writer.WriteStartArray("results");
                    foreach (var outcome in outcomes)
                    {
                        writer.WriteStartObject(); writer.WriteString("name", outcome.Name); writer.WritePropertyName("result");
                        JsonSerializer.Serialize(writer, outcome.Result, ProtocolInfo<CallToolResult>()); writer.WriteEndObject();
                    }
                    writer.WriteEndArray(); writer.WriteEndObject();
                }
            });
            return exitCode != 0 ? exitCode : failed ? 1 : 0;
        }
        catch (OperationCanceledException)
        {
            WriteError("cancelled_or_timed_out", "调用已取消或超时；已发送的命令可能已经执行，不会自动重试。");
            return 124;
        }
        catch (Exception error) when (error is ArgumentException or JsonException or IOException or McpException or InvalidOperationException)
        {
            WriteError(error is ArgumentException or JsonException ? "invalid_arguments" : "cli_failed", error.Message);
            return error is ArgumentException or JsonException ? 2 : 1;
        }
        finally { Console.CancelKeyPress -= handler; }
    }

    private static int ExtractTimeout(List<string> args)
    {
        var index = args.IndexOf("--timeout-seconds");
        if (index < 0) return 120;
        if (index + 1 >= args.Count || !int.TryParse(args[index + 1], out var seconds) || seconds is < 1 or > 3600)
            throw new ArgumentException("--timeout-seconds 必须在 1..3600 之间。");
        args.RemoveRange(index, 2);
        if (args.Contains("--timeout-seconds")) throw new ArgumentException("不能重复指定 --timeout-seconds。");
        return seconds;
    }

    private static async Task<List<CallToolRequestParams>> ReadRequestsAsync(List<string> args, CancellationToken cancellationToken)
    {
        if (args.Count == 1 && args[0] == "list") return [];
        var batch = args.Count > 0 && args[0] == "batch";
        var offset = batch ? 1 : 2;
        if (args.Count != offset + 2 || !batch && (args[0] != "call" || string.IsNullOrWhiteSpace(args[1])))
            throw new ArgumentException("使用 --cli list、call <name> --arguments <json> 或 batch --file <path|->。");
        var flag = args[offset];
        if (flag != "--file" && (flag != "--arguments" || batch)) throw new ArgumentException("参数输入应使用 --arguments 或 --file；batch 必须使用 --file。");
        var input = flag == "--arguments" ? args[offset + 1] : args[offset + 1] == "-"
            ? await Console.In.ReadToEndAsync(cancellationToken).ConfigureAwait(false)
            : await File.ReadAllTextAsync(args[offset + 1], cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(input);
        if (!batch) return [Request(args[1], document.RootElement)];
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            throw new ArgumentException("batch 文件必须是非空请求数组。");
        return document.RootElement.EnumerateArray().Select(item =>
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString()))
                throw new ArgumentException("每条 batch 请求必须有字符串 name。");
            return Request(name.GetString()!, item.TryGetProperty("arguments", out var parameters) ? parameters : default);
        }).ToList();
    }

    private static CallToolRequestParams Request(string name, JsonElement arguments)
    {
        if (arguments.ValueKind is not (JsonValueKind.Object or JsonValueKind.Undefined))
            throw new ArgumentException("工具 arguments 必须是 JSON 对象。");
        return new CallToolRequestParams { Name = name, Arguments = arguments.ValueKind == JsonValueKind.Undefined ? [] :
            arguments.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone()) };
    }

    // Keep prior batch outcomes even if a later request fails at the protocol boundary.
    private static CallToolResult Failure(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }]
    };

    private static CancellationTokenSource Deadline(int seconds, CancellationToken token)
    {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(seconds));
        return deadline;
    }

    private static JsonTypeInfo<T> ProtocolInfo<T>() => (JsonTypeInfo<T>)McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(T));
    private static void WriteError(string code, string message) => WriteJson(writer =>
    {
        writer.WriteStartObject(); writer.WriteBoolean("isError", true); writer.WriteString("code", code);
        writer.WriteString("message", message); writer.WriteEndObject();
    });
    private static void WriteJson(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer)) { write(writer); writer.Flush(); }
        using var output = Console.OpenStandardOutput();
        output.Write(buffer.WrittenSpan); output.WriteByte((byte)'\n'); output.Flush();
    }
}
