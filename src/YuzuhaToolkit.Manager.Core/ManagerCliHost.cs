using System.Text.Json;
using PmlTwin.Phayton;

namespace YuzuhaToolkit.Manager;

/// <summary>管理 CLI 的凭据边界；进程参数只放操作/目标及非秘密输入。</summary>
public static class ManagerCliHost
{
    /// <summary>启动、插件调用与批处理的 JSON 只能来自 stdin/本机文件，避免密码进入 argv。</summary>
    public static async Task<int> RunAsync(string[] args, OperationExecutor executor, TextWriter output,
        CancellationToken token = default, TextReader? inputReader = null)
    {
        if (args.Length >= 3 && args[0] == "op" && args.Contains("--input", StringComparer.Ordinal) &&
            (args[1] == "batch" || args[1] == "invoke" && (args[2] is "launch.profile.start" or "launch.execute" or "plugin.invoke")))
        {
            var result = OperationResult.Fail(args[1] == "batch" ? "op.batch" : args[2], 2,
                "SECRET_INPUT_SOURCE_REQUIRED", "启动、插件或批处理输入请通过 --input-stdin 或 --input-file 提供。");
            await output.WriteLineAsync(JsonSerializer.Serialize(result, OperationJsonContext.Default.OperationResult));
            return result.ExitCode;
        }
        return await OperationCli.RunAsync(args, executor, output, token, inputReader).ConfigureAwait(false);
    }
}
