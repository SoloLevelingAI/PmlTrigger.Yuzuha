using System.IO;
using System.Text.Json;
using YuzuhaToolkit.Mcp;
using YuzuhaToolkit.Pml;

namespace YuzuhaToolkit.Desktop;

/// <summary>桌面状态测试接缝；直接使用已有 C# 请求与响应，不增加 RPC 契约。</summary>
internal interface IWorkbenchHost
{
    /// <summary>只读发现，不选择目标。</summary>
    AvevaSessionListResponse Discover();
    /// <summary>可异步发现，避免独立通信进程等待占用界面线程。</summary>
    Task<AvevaSessionListResponse> DiscoverAsync(CancellationToken token = default) => Task.FromResult(Discover());
    /// <summary>显式选择发现到的会话。</summary>
    Task<YuzuhaConnectionStatus> SelectAsync(int pid, string? model, CancellationToken token);
    /// <summary>重新核验锁定目标。</summary>
    Task<YuzuhaConnectionStatus> StatusAsync(CancellationToken token);
    /// <summary>沿用原生窗体查询。</summary>
    Task<RunPmlCommandResponse> QueryAsync(PmlFormQueryRequest request, CancellationToken token);
    /// <summary>沿用已有数组窗口。</summary>
    Task<RunPmlCommandResponse> ReadArrayAsync(string name, int start, int items, int chars, CancellationToken token);
    /// <summary>沿用三种内置对象遍历函数和现有列表工具。</summary>
    Task<DesktopObjectReadReply> ReadObjectAsync(DesktopObjectQuery query, CancellationToken token);
    /// <summary>沿用注册函数调用和信任提示。</summary>
    Task<RunPmlCommandResponse> InvokeAsync(string name, List<DynamicParameterInput> parameters, CancellationToken token);
}

/// <summary>一个查询条件下的原生窗口；空页也可有续读索引。</summary>
internal sealed class FormWindow
{
    /// <summary>本轮已返回的条目，保留原生索引。</summary>
    public List<PmlFormQueryItem> Items { get; } = [];
    /// <summary>最近窗口的真实元数据。</summary>
    public PmlFormQueryResult? Last { get; private set; }
    /// <summary>原生续读位置，0 表示结束。</summary>
    public int NextIndex => Last?.NextIndex ?? 0;
    /// <summary>当前查询种类、名称和预算的拼接身份。</summary>
    internal string? Key { get; private set; }
    /// <summary>清理窗口与续读位置，不保留过期成功状态。</summary>
    internal void Clear() { Items.Clear(); Last = null; Key = null; }
    /// <summary>接收同一查询下的一页，保留原生索引。</summary>
    internal void Accept(string key, PmlFormQueryResult result) { Key = key; Last = result; Items.AddRange(result.Items ?? []); }
}

/// <summary>三条 M1 流程的桌面状态；目标核验、权限和原生读取均复用既有服务。</summary>
internal sealed class SessionWorkbench(IWorkbenchHost host, PmlFunctionRegistry registry)
{
    private AvevaSessionCandidate[] discovered = [];
    private AvevaSessionCandidate? selected;
    private string? arrayKey;
    private bool dispatched;
    private long targetEpoch;
    private string invalidationReason = "目标身份已失效，请重新发现并明确选择。";
    /// <summary>最近核验状态，不以发现到窗口代替连接成功。</summary>
    public YuzuhaConnectionStatus Status { get; private set; } = new();
    /// <summary>阻止同一界面并行操作或切换执行目标。</summary>
    public bool IsBusy { get; private set; }
    /// <summary>idle、running、succeeded、rejected 或 unknown。</summary>
    public string Outcome { get; private set; } = "idle";
    /// <summary>用户可读的结果与下一步。</summary>
    public string Message { get; private set; } = "请发现并显式选择工程会话。函数登记和授权可离线完成。";
    /// <summary>已显示或已加载窗体的当前查询。</summary>
    public FormWindow Forms { get; } = new();
    /// <summary>所选窗体的 GADGET 查询。</summary>
    public FormWindow Gadgets { get; } = new();
    /// <summary>单个控件的原始详情及读取状态。</summary>
    public RunPmlCommandResponse? GadgetDetails { get; private set; }
    /// <summary>最近请求回执，包含真实请求编号。</summary>
    public RunPmlCommandResponse? LastResponse { get; private set; }
    /// <summary>本窗口最近一次对象遍历结果；不能用作其他会话的结果。</summary>
    public DesktopObjectReadReply? ObjectResult { get; private set; }
    /// <summary>有界数组原生显示行。</summary>
    public List<string> ArrayRows { get; } = [];
    /// <summary>最近数组窗口及原索引。</summary>
    public PmlArrayWindowResult? ArrayLast { get; private set; }

    /// <summary>发现不会自动连接；保存候选身份的副本。</summary>
    public AvevaSessionListResponse Discover()
    {
        EnsureIdle();
        return AcceptDiscovery(host.Discover());
    }
    /// <summary>异步发现期间禁止并发选择/执行；只接收本次完整候选。</summary>
    public async Task<AvevaSessionListResponse> DiscoverAsync(CancellationToken token = default)
    {
        EnsureIdle(); IsBusy = true;
        try { return AcceptDiscovery(await host.DiscoverAsync(token).ConfigureAwait(false)); }
        finally { IsBusy = false; }
    }
    private AvevaSessionListResponse AcceptDiscovery(AvevaSessionListResponse response)
    {
        discovered = response.Sessions.Select(Clone).ToArray();
        Outcome = response.Error is null ? "idle" : "rejected";
        Message = response.Error ?? (response.Sessions.Count == 0 ? "未发现运行中的 AVEVA 工程会话。已安装的 AM / PDMS / E3D 入口请到启动工程扫描；安装入口不等于运行会话。" : $"发现 {response.Sessions.Count} 个运行会话。请选择目标并核验；无 Host 的工程窗口也会列出。");
        return response;
    }

    /// <summary>明确选择候选并核对 PID 与启动身份；切换立即清理旧查询。</summary>
    public Task SelectAsync(AvevaSessionCandidate candidate, string? model = null, CancellationToken token = default) => Run(async () =>
    {
        selected = null; Status = new(); ClearQueries();
        var saved = discovered.SingleOrDefault(c => c.ProcessId == candidate.ProcessId && c.ProcessStartTimeUtcTicks == candidate.ProcessStartTimeUtcTicks);
        if (saved is null || !candidate.PipeDetected || !saved.PipeDetected) throw new InvalidOperationException("请重新发现并选择带 Host 专用管道的会话。");
        selected = Clone(saved); Status = new(); ClearQueries();
        try
        {
            var result = await host.SelectAsync(candidate.ProcessId, model, token);
            ValidateIdentity(result);
            Status = result;
            Message = "目标身份已核验。可查看能力、登记函数或执行有界查询。";
        }
        catch (Exception error) { InvalidateTarget(error.Message); throw; }
    });

    /// <summary>重新核验，不执行 PML。</summary>
    public Task VerifyAsync(CancellationToken token = default) => Run(async () => { await RequireTarget(token); Message = "目标身份仍有效；各查询按 Host 能力分别判断。"; });

    /// <summary>离线列出真实注册表，损坏文件保持错误而不覆盖。</summary>
    public List<PmlFunctionRegistration> Functions() => registry.List();
    /// <summary>离线登记真实源文件；重登记撤销原权限，不加载 PML。</summary>
    public PmlFunctionRegistration Register(string source, string name) { EnsureIdle(); return registry.Register(source, name); }
    /// <summary>人工明确授予或撤销当前源码版本的注册入口许可。</summary>
    public PmlFunctionRegistration SetPermission(string name, bool allow) { EnsureIdle(); return registry.SetPermission(name, allow); }
    /// <summary>按已有签名顺序预览；源码变化或未授权时保持拒绝。</summary>
    public string Preview(string name, List<DynamicParameterInput> parameters) => registry.PrepareCall(name, parameters);

    /// <summary>核验源码、权限与目标后调用一次；丢失回执不自动重试。</summary>
    public Task InvokeAsync(string name, List<DynamicParameterInput> parameters, CancellationToken token = default) => Run(async () =>
    {
        _ = registry.PrepareCall(name, parameters);
        await RequireTarget(token); LastResponse = null;
        dispatched = true; LastResponse = await host.InvokeAsync(name, parameters, token);
        RequireSuccess(LastResponse);
        Message = "Host 调度成功。此回执不证明函数业务返回值或 AVEVA 已加载的源码版本。";
        if (!string.IsNullOrWhiteSpace(LastResponse.FunctionTrustWarning)) Message += "\n人工信任提示：" + LastResponse.FunctionTrustWarning;
    });

    /// <summary>读取或续读原生窗口；条件变化不能拼接旧窗口。</summary>
    public Task QueryAsync(PmlFormQueryRequest input, bool append = false, CancellationToken token = default) => Run(async () =>
    {
        var request = new PmlFormQueryRequest { Kind = input.Kind, FormName = input.FormName, GadgetName = input.GadgetName, StartIndex = 1, MaxItems = input.MaxItems, MaxCharacters = input.MaxCharacters };
        PmlFormQuery.Validate(request);
        await RequireTarget(token);
        if (Status.Host?.FormQueryVersion != 1) throw new NotSupportedException("当前 Host 不支持 Form/Gadget 查询。请更新对应 Host 后重新核验；不会回退到原始命令。");
        var key = $"{request.Kind}|{request.FormName}|{request.GadgetName}|{request.MaxItems}|{request.MaxCharacters}";
        var page = request.Kind is "forms" or "shown-forms" ? Forms : Gadgets;
        if (append)
        {
            if (request.Kind == "gadget" || page.Key != key || page.NextIndex == 0) throw new InvalidOperationException("查询条件已变化或已到末端，请开始新查询。");
            request.StartIndex = page.NextIndex;
        }
        else
        {
            if (request.Kind != "gadget") page.Clear();
            GadgetDetails = null;
            if (request.Kind is "forms" or "shown-forms") Gadgets.Clear();
        }
        LastResponse = null;
        dispatched = true; var response = await host.QueryAsync(request, token); LastResponse = response; RequireSuccess(response);
        var result = response.FormQueryResult ?? throw new InvalidDataException("缺少原生查询窗口，不能显示为查询通过。");
        if (result.Kind != request.Kind || result.StartIndex != request.StartIndex) throw new InvalidDataException("查询回执与请求不匹配，请检查原始证据。");
        LastResponse = response;
        if (request.Kind == "gadget") GadgetDetails = response; else page.Accept(key, result);
        Message = $"本页返回 {result.Items?.Length ?? 0} 项，扫描 {result.Scanned} 个槽位；NextIndex={result.NextIndex}。" + (result.NextIndex == 0 ? "本次枚举到达末端。" : "仍有后续，空页也可继续读取。跨请求不是冻结快照。");
    });

    /// <summary>窗口读取现有全局数组，不执行生产表达式或删除源数组。</summary>
    public Task ReadArrayAsync(string name, int items, int characters, bool append = false, CancellationToken token = default) => Run(async () =>
    {
        PmlArrayWindow.Validate(1, items, characters); await RequireTarget(token);
        if (Status.Host?.BoundedArrayReadVersion != 1) throw new NotSupportedException("当前 Host 不支持有界数组读取，请更新后重新核验。");
        var key = $"{name}|{items}|{characters}";
        if (append && (arrayKey != key || ArrayLast?.NextIndex is null or 0)) throw new InvalidOperationException("数组或预算已变化，请开始新查询。");
        var start = append ? ArrayLast!.NextIndex : 1;
        if (!append) { ArrayRows.Clear(); ArrayLast = null; arrayKey = null; }
        LastResponse = null;
        dispatched = true; var response = await host.ReadArrayAsync(name, start, items, characters, token); LastResponse = response; RequireSuccess(response);
        var window = response.ArrayWindow ?? throw new InvalidDataException("缺少数组窗口回执。");
        if (window.StartIndex != start) throw new InvalidDataException("数组窗口起点不匹配。");
        ArrayLast = window; arrayKey = key; ArrayRows.AddRange(window.Rows ?? []); LastResponse = response;
        Message = $"本页返回 {window.Rows?.Length ?? 0} 行；索引上界 {window.IndexUpperBound} 不是条目总数。NextIndex={window.NextIndex}。";
    });

    private void EnsureIdle() { if (IsBusy) throw new InvalidOperationException("当前操作尚未结束，请等待结果；不要重复发送。"); }
    /// <summary>显式读取当前对象、指定元素或全局变量，逐次核验目标且不自动重试。</summary>
    public Task ReadObjectAsync(DesktopObjectQuery query, CancellationToken token = default) => Run(async () =>
    {
        ObjectResult = null; await RequireTarget(token);
        dispatched = true; var result = await host.ReadObjectAsync(query, token);
        LastResponse = result.Receipt; RequireSuccess(result.Receipt); ObjectResult = result;
        Message = $"对象遍历返回 {result.Items.Length} 项，未解析 {result.Unparsed.Length} 行。受每层数量和深度限制，不代表完整对象快照。";
    });
    private async Task Run(Func<Task> action)
    {
        EnsureIdle(); IsBusy = true; dispatched = false; LastResponse = null; Outcome = "running";
        var epoch = targetEpoch;
        try
        {
            await action();
            if (epoch != targetEpoch) throw new IOException(invalidationReason);
            Outcome = "succeeded";
        }
        catch (Exception error)
        {
            Outcome = dispatched && error is (OperationCanceledException or IOException or TimeoutException) ? "unknown" : "rejected";
            Message = error.Message + (Outcome == "unknown" ? "\n结果未知：取消等待或传输失败不能撤回已经发出的操作。请检查回执；不会自动重试。" : "\n操作未完成；请修正条件后再执行。");
            throw;
        }
        finally
        {
            // A background invalidation may arrive while the Host owns an already-sent request.
            // Its eventual response must not refill results or restore the former verified target.
            if (epoch != targetEpoch) { selected = null; Status = new(); ClearQueries(); }
            IsBusy = false;
        }
    }
    private async Task RequireTarget(CancellationToken token)
    {
        if (selected is null) throw new InvalidOperationException("尚未选择目标，请到工程会话发现并核验。");
        try
        {
            var result = await host.StatusAsync(token);
            ValidateIdentity(result); Status = result;
        }
        catch (Exception error) { InvalidateTarget(error.Message); throw; }
    }
    private void ValidateIdentity(YuzuhaConnectionStatus result)
    {
        if (selected is null || !result.TargetVerified || !result.Connected || result.Host is null || result.Host.ProcessId != selected.ProcessId || result.Host.ProcessStartTimeUtcTicks != selected.ProcessStartTimeUtcTicks || !string.Equals(result.Host.Model, result.ExpectedModel, StringComparison.OrdinalIgnoreCase))
        {
            InvalidateTarget(result.Error ?? "目标身份失效，请重新发现并明确选择。");
            throw new InvalidOperationException(result.Error ?? "目标身份失效，已清除旧查询。请重新发现并核验，输入会保留。");
        }
    }
    /// <summary>界面线程撤销失效目标及旧查询；进行中的回执还会由操作末尾代次门禁丢弃，不撤销或重发已派发操作。</summary>
    internal void InvalidateTarget(string reason)
    {
        invalidationReason = string.IsNullOrWhiteSpace(reason) ? "目标身份已失效，请重新发现并明确选择。" : reason;
        targetEpoch++;
        selected = null; Status = new(); ClearQueries();
        if (!IsBusy) { Outcome = "rejected"; Message = invalidationReason; }
    }
    private void ClearQueries() { Forms.Clear(); Gadgets.Clear(); GadgetDetails = null; LastResponse = null; ObjectResult = null; ArrayRows.Clear(); ArrayLast = null; arrayKey = null; }
    /// <summary>窗体选择变化时清理控件和详情。</summary>
    internal void ResetGadgets() { EnsureIdle(); Gadgets.Clear(); ResetGadgetDetails(); }
    /// <summary>控件选择变化后清除此前属性与回执，不触发查询。</summary>
    internal void ResetGadgetDetails() { EnsureIdle(); GadgetDetails = null; LastResponse = null; }
    /// <summary>仅相同数组与预算允许继续读取。</summary>
    internal bool CanContinueArray(string name, int items, int characters) => ArrayLast?.NextIndex > 0 && arrayKey == $"{name}|{items}|{characters}";
    private static void RequireSuccess(RunPmlCommandResponse response) { if (!response.Success) throw new InvalidOperationException($"{response.Code}: {response.ErrorMessage}"); }
    private static AvevaSessionCandidate Clone(AvevaSessionCandidate c) => new() { ProcessId = c.ProcessId, ProcessStartTimeUtcTicks = c.ProcessStartTimeUtcTicks, ProcessName = c.ProcessName, ExecutablePath = c.ExecutablePath, WindowTitle = c.WindowTitle, Product = c.Product, Project = c.Project, PipeName = c.PipeName, PipeDetected = c.PipeDetected };
}

/// <summary>适配已有注册工具的 JSON 成功回执和文本传输错误。</summary>
internal static class WorkbenchReply
{
    /// <summary>不可解析或缺失的回执保持未知，不能误报业务失败或通过。</summary>
    internal static RunPmlCommandResponse ParseRegisteredCall(string text)
    {
        if (text.StartsWith("PML RPC failed:", StringComparison.Ordinal)) throw new IOException(text);
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.EnumerateObject().FirstOrDefault(p => p.Name.Equals("Success", StringComparison.OrdinalIgnoreCase)).Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new IOException("注册调用回执缺少明确的 Success 状态，结果未知。");
            return JsonSerializer.Deserialize<RunPmlCommandResponse>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new IOException("注册调用缺少回执。");
        }
        catch (JsonException error) { throw new IOException("无法解析注册调用回执，执行结果未知。", error); }
    }
}

/// <summary>将已有信任文件约定适配到安装器现有 runtime/desktop 布局。</summary>
internal static class WorkbenchPaths
{
    /// <summary>保留已有环境覆盖优先级；托管安装与 MCP 共用包根下的人工信任记录。</summary>
    internal static string TrustFile(string baseDirectory, string? trustOverride, string? rootOverride)
    {
        if (!string.IsNullOrWhiteSpace(trustOverride)) return Path.GetFullPath(trustOverride.Trim());
        var directory = new DirectoryInfo(Path.GetFullPath(baseDirectory));
        var root = !string.IsNullOrWhiteSpace(rootOverride) ? Path.GetFullPath(rootOverride.Trim()) : directory.Name.Equals("desktop", StringComparison.OrdinalIgnoreCase) && directory.Parent?.Name.Equals("runtime", StringComparison.OrdinalIgnoreCase) == true ? directory.Parent.Parent!.FullName : directory.FullName;
        return Path.Combine(root, "trust", "pml-function-trust.json");
    }
}
