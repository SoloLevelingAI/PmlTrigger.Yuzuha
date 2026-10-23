using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PmlTwin.Phayton;
using YuzuhaToolkit.Mcp;

namespace YuzuhaToolkit.WebApps;

/// <summary>一个动作请求固定目录版本和会话，不接收浏览器提供的 PML 文本或文件路径。</summary>
public sealed record InvokeRequest(string RequestId, int CatalogRevision, string AppId, string ActionId,
    Dictionary<string, JsonElement> Parameters);
/// <summary>每一阶段的原始操作回执；失败或未知后不会自动继续。</summary>
public sealed record RunStep(string Name, OperationResult Response);
/// <summary>持久化的调用证据，包含输入、会话、来源指纹、执行状态和返回结果。</summary>
public sealed class RunRecord
{
    /// <summary>客户端生成且在本地保留的唯一调用标识。</summary>
    public required string RequestId { get; init; }
    /// <summary>已序列化请求的指纹，阻止同一标识被用于其他参数。</summary>
    public required string InputSha256 { get; init; }
    /// <summary>实际使用的请求快照。</summary>
    public required InvokeRequest Request { get; init; }
    /// <summary>实际使用的动作定义，不随目录后续修改变化。</summary>
    public required ActionBinding Binding { get; init; }
    /// <summary>已锁定的 AVEVA 进程身份。</summary>
    public required SessionPin Session { get; init; }
    /// <summary>开始时间。</summary>
    public DateTime StartedUtc { get; init; } = DateTime.UtcNow;
    /// <summary>Prepared、Dispatching、Succeeded、Failed 或 Unknown；未知不能视为未执行。</summary>
    public string Status { get; set; } = "Prepared";
    /// <summary>执行失败或状态未知的原因。</summary>
    public string? Error { get; set; }
    /// <summary>已部署目标源码与指纹；不等同于已加载版本证明。</summary>
    public CompiledAction? Compiled { get; set; }
    /// <summary>各阶段原始回执。</summary>
    public List<RunStep> Steps { get; set; } = [];
    /// <summary>最终数组操作结果，保留 Items、Summary 和未解析内容。</summary>
    public OperationResult? Result { get; set; }
}

/// <summary>本地目录、会话与动作编排；宏执行和数组读取均复用现有 IPC。</summary>
public sealed class ManagerService(CatalogStore catalog, IWebIpc ipc, string directory, string pmlRoot)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private SessionPin? selected;
    /// <summary>只读发现当前 AVEVA 会话。</summary>
    public AvevaSessionListResponse Sessions() => ipc.Discover();
    /// <summary>核验并保存用户显式选择的会话身份。</summary>
    public async Task<SessionPin> SelectAsync(int processId, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try { return selected = await ipc.SelectAsync(processId, token); }
        finally { gate.Release(); }
    }
    /// <summary>当前会话选择，尚不能代替执行时身份核验。</summary>
    public SessionPin? Selected => selected;
    /// <summary>读取已有请求记录；不会重发 PML。</summary>
    public RunRecord ReadRun(string id) => JsonSerializer.Deserialize<RunRecord>(File.ReadAllText(RunFile(id)), CatalogStore.Json)!;
    /// <summary>只列出摘要，模型数据按请求详情读取。</summary>
    public object[] Runs() => Directory.Exists(Path.Combine(directory, "runs"))
        ? Directory.EnumerateFiles(Path.Combine(directory, "runs"), "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(50)
            .Select(file => { var r = ReadRun(Path.GetFileNameWithoutExtension(file)); return (object)new { r.RequestId, r.StartedUtc, r.Status, r.Request.AppId, r.Request.ActionId }; }).ToArray() : [];

    /// <summary>派发前写入证据；同一请求只读回原记录，失败和超时均不自动重试。</summary>
    public async Task<RunRecord> InvokeAsync(InvokeRequest request, CancellationToken token)
    {
        var file = RunFile(request.RequestId);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, CatalogStore.Json)));
        await gate.WaitAsync(token);
        try
        {
            if (File.Exists(file))
            {
                var previous = ReadRun(request.RequestId);
                if (previous.InputSha256 != hash) throw new InvalidOperationException("请求标识已用于另一组参数。");
                return previous;
            }
            var pin = selected ?? throw new InvalidOperationException("请先选择并连接 AVEVA 会话。");
            var snapshot = catalog.Read();
            if (snapshot.Revision != request.CatalogRevision) throw new InvalidOperationException("绑定已更新，请刷新后再调用。");
            var app = snapshot.Apps.SingleOrDefault(a => a.Id == request.AppId && a.Enabled)
                ?? throw new ArgumentException("应用不存在或已停用。");
            var binding = app.Actions.SingleOrDefault(a => a.Id == request.ActionId)
                ?? throw new ArgumentException("按钮未登记。");
            var compiled = ActionCompiler.Compile(binding, request.Parameters, pmlRoot, request.RequestId);
            var macro = Path.Combine(directory, "macros", request.RequestId + ".pmlmac");
            Directory.CreateDirectory(Path.GetDirectoryName(macro)!);
            File.WriteAllText(macro, compiled.Macro, new UTF8Encoding(false));
            var run = new RunRecord { RequestId = request.RequestId, InputSha256 = hash, Request = request,
                Binding = binding, Session = pin, Compiled = compiled };
            Save(run);
            try
            {
                // Record before sending. On process loss this remains uncertain and cannot be replayed.
                run.Status = "Dispatching"; Save(run);
                var executed = await ipc.InvokeAsync(YuzuhaOperationIds.Execute,
                    JsonSerializer.SerializeToElement(new { pmlCommand = "$M '" + macro.Replace("'", "''") + "'" }), pin, token);
                run.Steps.Add(new("execute-macro", executed)); Save(run);
                if (!executed.Success)
                {
                    run.Status = executed.Error?.MayHaveExecuted == true ? "Unknown" : "Failed";
                    run.Error = executed.Error?.Message; Save(run); return run;
                }
                var result = await ipc.InvokeAsync(YuzuhaOperationIds.ReadArray,
                    JsonSerializer.SerializeToElement(new { pmlCommand = "!!" + compiled.ResultGlobal,
                        globalVar = compiled.ResultGlobal, deleteGlobalVar = true, includeEmpty = true }), pin, token);
                run.Result = result; run.Steps.Add(new("read-results", result));
                run.Status = result.Success ? "Succeeded" : result.Error?.MayHaveExecuted == true ? "Unknown" : "Failed";
                run.Error = result.Error?.Message;
                if (result.Success && result.Data is { } data &&
                    data.GetProperty("UnparsedCount").GetInt32() != 0)
                { run.Status = "Failed"; run.Error = "结果包含未解析或读取失败的行。"; }
                Save(run); return run;
            }
            catch (Exception e)
            {
                run.Status = "Unknown"; run.Error = e.Message; Save(run); return run;
            }
        }
        finally { gate.Release(); }
    }

    private string RunFile(string id)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-f0-9]{32}$")) throw new ArgumentException("请求标识无效。");
        return Path.Combine(directory, "runs", id + ".json");
    }
    private void Save(RunRecord record)
    {
        var file = RunFile(record.RequestId);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temporary = file + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(record, CatalogStore.Json));
        File.Move(temporary, file, true);
    }
}
