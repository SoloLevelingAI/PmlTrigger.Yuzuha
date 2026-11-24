using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Core30.Core;
using Core30.Generated;
using Core30.Workbench;
using Microsoft.Win32;
using static Core30.Workbench.Views;

namespace PmlTwin.Workbench;

public partial class MainWindow
{
    private static readonly TimeSpan ParseTimeout = TimeSpan.FromSeconds(1);
    private static readonly Regex FunctionSignature = new(
        @"^(!![A-Za-z][A-Za-z0-9_]*)[ \t]*\(([^()\r\n]*)\)(?:[ \t]+is[ \t]+([A-Za-z][A-Za-z0-9_]*))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, ParseTimeout);
    private static readonly Regex SourceDeclaration = new(
        @"^[ \t]*define[ \t]+function[ \t]+(!![A-Za-z][A-Za-z0-9_]*[ \t]*\([^()\r\n]*\)(?:[ \t]+is[ \t]+[A-Za-z][A-Za-z0-9_]*)?)[ \t]*(?:--[^\r\n]*)?\r?$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant, ParseTimeout);
    private static readonly Regex ParameterDeclaration = new(
        @"^(?:!([A-Za-z][A-Za-z0-9_]*)[ \t]+is[ \t]+)?(STRING|REAL|BOOLEAN)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, ParseTimeout);
    private string _functionId = "";
    private string _functionQuery = "";
    private string _functionOrigin = "全部来源";
    private string _functionReadOnly = "全部标记";
    private string _logId = "";
    private string _logFilter = "全部状态";

    private FrameworkElement FunctionsPage()
    {
        var search = Input(_functionQuery, "FunctionSearch");
        var origins = Choice(new[] { "全部来源", "官方", "自动扫描", "手工登记" }, _functionOrigin, "FunctionOriginFilter");
        var readOnly = Choice(new[] { "全部标记", "只读", "非只读" }, _functionReadOnly, "FunctionReadOnlyFilter");
        var grid = Table(Array.Empty<PmlFunction>(), "PmlFunctionsGrid", ("函数签名", "Signature", 2.25),
            ("来源", "Origin", 1.35), ("只读标记", "ReadOnlyLabel", .9), ("调用授权", "Policy", .75));
        grid.MaxHeight = 300;
        var count = Muted("");
        var catalog = FunctionCatalog();
        var details = new ContentControl();
        var edit = Btn("编辑登记", "EditPmlFunction", () => { if (grid.SelectedItem is PmlFunction fn) EditFunction(fn); });
        void ShowSelection()
        {
            if (grid.SelectedItem is not PmlFunction fn)
            {
                edit.IsEnabled = false;
                details.Content = Card("执行函数", Muted("请在函数登记表中选择一项。"));
                return;
            }
            edit.IsEnabled = true;
            _functionId = fn.Key;
            var arguments = ParameterFields(fn, "PmlArg");
            var notice = new Notice("FunctionExecutionError");
            var execute = AsyncBtn("预览并执行", "RunPmlFunction", () => RunFunction(fn, arguments.Read, notice), "Button.Primary");
            execute.IsEnabled = CanInvokeFunction(fn);
            var content = Stack(SessionSelector(), Details([
                ("目标会话", _session?.ToString() ?? "等待 CLI 连接"),
                ("当前函数", fn.Signature), ("来源文件", fn.Source.Length > 0 ? fn.Source : "未填写"),
                ("权限", $"{fn.ReadOnlyLabel} · {fn.Policy}"),
                ("用途", fn.Detail.Length == 0 ? "—" : fn.Detail)
            ]), arguments.Panel, Below(notice, 4),
                Below(Actions(Jump(_session is not null ? "执行日志" : "查看 CLI 状态", _session is not null ? "Logs" : "Cli"), execute), 7));
            details.Content = Card("执行函数", content);
        }
        void Reload()
        {
            var query = _functionQuery.Trim();
            var rows = catalog.Where(fn => (_functionOrigin switch
                {
                    "官方" => fn.Official,
                    "自动扫描" => fn.FromScan,
                    "手工登记" => !fn.CatalogOnly,
                    _ => true
                }) && (_functionReadOnly == "全部标记" || fn.ReadOnlyLabel == _functionReadOnly) &&
                $"{fn.Signature} {fn.Detail} {fn.Source}".Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
            var chosen = rows.FirstOrDefault(fn => fn.Key == _functionId);
            count.Text = $"{rows.Length:N0} 个函数";
            grid.ItemsSource = rows;
            grid.SelectedItem = chosen ?? rows.FirstOrDefault();
            if (grid.SelectedItem is { } item) grid.ScrollIntoView(item);
            ShowSelection();
        }
        grid.SelectionChanged += (_, _) => ShowSelection();
        search.TextChanged += (_, _) => { _functionQuery = search.Text; Reload(); };
        origins.SelectionChanged += (_, _) => { _functionOrigin = (string)origins.SelectedItem; Reload(); };
        readOnly.SelectionChanged += (_, _) => { _functionReadOnly = (string)readOnly.SelectedItem; Reload(); };
        Reload();
        return SideBySide(Card("函数登记表", Stack(Prompt(search, "搜索函数签名、用途或路径"),
                Below(Form(Field("来源", origins), Field("只读标记", readOnly)), 8), Below(count, 6),
                Below(grid, 7), Below(edit, 7), Below(PmlScanNotice("FunctionsScanNotice"), 8)),
            Actions(RefreshPmlButton("RefreshFunctionDirectories"), AsyncBtn("刷新授权", "RefreshFunctions", RefreshFunctions, "Button.Quiet"),
                Primary("登记 PML 函数", "RegisterPmlFunction", () => EditFunction()))), details, 1.45, 725);
    }

    private PmlFunction[] FunctionCatalog()
    {
        var rows = _store.PmlScan.Functions.Select(item => new PmlFunction(item.Name, item.Summary, item.Path, false,
            item.Parameters.Select(parameter => new PmlParameter(parameter.Name, parameter.Type)).ToArray(), item.ReturnType)
        {
            Digest = item.Digest, DiscoveredDeclaration = item.Declaration, FromScan = true,
            CatalogOnly = true, Official = item.Official, CanRegister = item.CanRegister
        }).ToDictionary(row => row.Key);
        foreach (var registered in Data.Functions)
        {
            var scanned = rows.GetValueOrDefault(registered.Key);
            rows[registered.Key] = registered with
            {
                FromScan = scanned is not null,
                Official = scanned?.Official == true,
                Detail = registered.Detail.Length > 0 ? registered.Detail : scanned?.Detail ?? ""
            };
        }
        return rows.Values.Select(row => row with { ReviewedReadOnly = IsReviewedReadOnly(row) }).ToArray();
    }

    private bool IsReviewedReadOnly(PmlFunction function) =>
        function.SourceCurrent && function.Digest.Length > 0 &&
        (function.ReviewedReadOnly || function.CatalogOnly && HasStoredReview(function));

    private bool CanInvokeFunction(PmlFunction function) =>
        _session is not null && function.AllowInvoke && function.SourceCurrent &&
        (!_readOnly || _session.ReviewedFunctionLibraryReady && IsReviewedReadOnly(function));

    private bool HasStoredReview(PmlFunction function) =>
        string.Equals(_store.Pml.ReviewedReadOnly.GetValueOrDefault(function.Key), function.Digest, StringComparison.OrdinalIgnoreCase);

    private static PmlFunction? ParseFunction(string text, string detail = "", string source = "", bool allow = false)
    {
        var match = FunctionSignature.Match(text.Trim());
        if (!match.Success) return null;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parameters = new List<PmlParameter>();
        var list = match.Groups[2].Value;
        if (!string.IsNullOrWhiteSpace(list))
        {
            foreach (var raw in list.Split(','))
            {
                var value = ParameterDeclaration.Match(raw.Trim());
                if (!value.Success) return null;
                var name = value.Groups[1].Success ? value.Groups[1].Value : "arg" + (parameters.Count + 1);
                if (!names.Add(name)) return null;
                parameters.Add(new(name, value.Groups[2].Value.ToUpperInvariant()));
            }
        }
        return new(match.Groups[1].Value, detail, source, allow, parameters.ToArray(), match.Groups[3].Value.ToUpperInvariant());
    }

    private void EditFunction(PmlFunction? original = null)
    {
        var signature = Input(original is { CatalogOnly: true } ? original.DiscoveredDeclaration : original?.Declaration ?? "", "RegistrationSignature");
        var source = Input(original?.Source ?? "", "RegistrationSource");
        var detail = Input(original?.Detail ?? "", "RegistrationDescription");
        var allow = Check("允许调用经过核对的当前源码", "RegistrationAllowInvoke", original?.AllowInvoke == true);
        var readOnly = Check("已核对当前源码为只读", "RegistrationReadOnly", original?.ReviewedReadOnly == true);
        allow.IsEnabled = original?.CanRegister != false;
        if (!allow.IsEnabled) allow.ToolTip = "调用参数支持 STRING、REAL 和 BOOLEAN。";
        var sourceSeen = original?.Source ?? "";
        var reviewDigest = original is { SourceCurrent: true } ? original.Digest : "";
        if (original?.CatalogOnly == true) signature.IsReadOnly = source.IsReadOnly = true;
        var notice = new Notice("RegistrationNotice");
        var candidates = Choice(Array.Empty<string>(), null, "RegistrationCandidate");
        var candidateField = Field("文件中的函数", candidates);
        candidateField.Visibility = Visibility.Collapsed;
        var upload = Btn("从 .pmlfnc 读取签名…", "ChoosePmlSource", () =>
        {
            var dialog = new OpenFileDialog { Title = "选择 PML 函数文件", Filter = "PML 函数 (*.pmlfnc)|*.pmlfnc", CheckFileExists = true };
            if (dialog.ShowDialog() != true) return;
            try
            {
                if (new FileInfo(dialog.FileName).Length > 1024 * 1024)
                {
                    notice.Show("PML 函数文件超过 1 MB。", "warning");
                    return;
                }
                var signatures = SourceDeclaration.Matches(File.ReadAllText(dialog.FileName)).Cast<Match>()
                    .Select(match => ParseFunction(match.Groups[1].Value)).OfType<PmlFunction>().ToArray();
                if (signatures.Length == 0 || signatures.Select(fn => fn.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != signatures.Length)
                {
                    notice.Show("文件需要包含可识别且名称唯一的全局函数定义。", "warning");
                    return;
                }
                candidates.ItemsSource = signatures.Select(fn => fn.Declaration).ToArray();
                candidates.SelectedIndex = 0;
                candidateField.Visibility = signatures.Length > 1 ? Visibility.Visible : Visibility.Collapsed;
                signature.Text = signatures[0].Declaration;
                signature.IsReadOnly = true;
                source.Text = dialog.FileName;
                sourceSeen = dialog.FileName;
                reviewDigest = FileContent.Sha256(FileContent.ReadBounded(sourceSeen, 1024 * 1024));
                allow.IsChecked = readOnly.IsChecked = false;
                notice.Clear();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or RegexMatchTimeoutException)
            {
                notice.Show("无法读取函数定义：" + error.Message, "warning");
            }
        });
        candidates.SelectionChanged += (_, _) =>
        {
            if (candidates.SelectedItem is string declaration) signature.Text = declaration;
            allow.IsChecked = readOnly.IsChecked = false;
        };
        if (original?.CatalogOnly == true) upload.Visibility = Visibility.Collapsed;
        var preview = Btn("查看源码…", "PreviewRegistrationSource", () =>
        {
            try
            {
                var path = LocalSource.RequireAbsolutePath(source.Text.Trim());
                var bytes = FileContent.ReadBounded(path, 1024 * 1024);
                var text = LocalText.Decode(bytes).Content;
                Modal(Path.GetFileName(path), Code(text, "RegistrationSourceText", 320), "完成", () => true, width: 745, cancel: false);
                sourceSeen = path;
                reviewDigest = FileContent.Sha256(bytes);
            }
            catch (Exception error) when (WorkbenchError(error)) { notice.Show(error.Message, "warning"); }
        });
        var saved = Modal(original is null ? "登记 PML 函数" : "编辑 PML 函数登记", Stack(Actions(upload, preview), Below(candidateField, 7),
            Below(Field("函数签名", Prompt(signature, "!!FindItem(!name is STRING)")), 8),
            Below(Form(Field("来源文件或路径", source), Field("用途", detail)), 8), readOnly, Below(allow, 5), Below(notice, 7)), "保存登记", () =>
        {
            if (original is { CatalogOnly: true } && allow.IsChecked != true)
            {
                try { SaveFunctionReview(original, readOnly.IsChecked == true); }
                catch (Exception error) when (WorkbenchError(error)) { notice.Show(error.Message, "danger"); return false; }
                _functionId = original.Key;
                return true;
            }
            var fn = ParseFunction(signature.Text, detail.Text.Trim(), source.Text.Trim(), allow.IsChecked == true);
            if (fn is null) { notice.Show("请核对全局函数签名。参数支持 STRING、REAL、BOOLEAN，参数名须唯一。", "warning"); return false; }
            if (Data.Functions.Any(item => (original is null || original.CatalogOnly || item.Key != original.Key)
                && string.Equals(item.Id, fn.Id, StringComparison.OrdinalIgnoreCase)))
            {
                notice.Show("该函数已登记，请选择后编辑。", "warning");
                return false;
            }
            try
            {
                if (!Path.IsPathFullyQualified(fn.Source) || !File.Exists(fn.Source))
                    throw new InvalidDataException("请先选择可读取的 .pmlfnc 来源文件。");
                using var revision = File.Open(fn.Source, FileMode.Open, FileAccess.Read, FileShare.Read);
                var markReadOnly = readOnly.IsChecked == true;
                if (markReadOnly)
                {
                    if (!string.Equals(fn.Source, sourceSeen, StringComparison.OrdinalIgnoreCase) || reviewDigest.Length == 0)
                        throw new InvalidDataException("请先查看当前源码，再确认只读。");
                    FileContent.RequireMatchingSha256(reviewDigest, FileContent.Sha256(FileContent.ReadBounded(fn.Source, 1024 * 1024)),
                        "源码已变化，请重新查看后再确认只读。");
                }
                var name = fn.Id.TrimStart('!');
                var registration = Run(PhaytonRequests.Register(name, fn.Source));
                RequireFunctionReceipt(registration);
                var registered = registration.Child("function");
                var result = Run(PhaytonRequests.PermissionReviewed(name, fn.AllowInvoke, Value(registered, "sha256"), markReadOnly));
                RequireFunctionReceipt(result);
                fn = fn with { Digest = Value(registered, "sha256"), ReviewedReadOnly = markReadOnly };
                if (HasStoredReview(fn)) SaveFunctionReview(fn, false);
            }
            catch (Exception error) when (WorkbenchError(error)) { notice.Show(error.Message, "danger"); return false; }
            var index = original is null ? -1 : Data.Functions.FindIndex(item => item.Key == original.Key);
            if (index < 0) Data.Functions.Add(fn);
            else Data.Functions[index] = fn;
            _functionId = fn.Key;
            _functionQuery = "";
            return true;
        }, signature, 645);
        if (!saved) return;
        Render();
        Save("已保存 PML 函数登记。");
        _ = RefreshFunctions();
    }

    private void SaveFunctionReview(PmlFunction fn, bool readOnly)
    {
        if (readOnly)
        {
            var digest = FileContent.Sha256(FileContent.ReadBounded(LocalSource.RequireAbsolutePath(fn.Source), 1024 * 1024));
            FileContent.RequireMatchingSha256(fn.Digest, digest, "源码已变化，请重新扫描或登记后再标记。");
        }
        _store.UpdatePmlPreferences(settings =>
        {
            if (readOnly) settings.ReviewedReadOnly[fn.Key] = fn.Digest;
            else settings.ReviewedReadOnly.Remove(fn.Key);
        });
    }

    private static (FrameworkElement Panel, Func<(object?[] Values, string Error)> Read) ParameterFields(PmlFunction fn, string id)
    {
        var panel = new StackPanel();
        var inputs = new List<Func<string>>();
        for (var index = 0; index < fn.Parameters.Length; index++)
        {
            var parameter = fn.Parameters[index];
            var label = $"{parameter.Name} · {parameter.Type}";
            if (parameter.Type == "BOOLEAN")
            {
                var choice = Choice(new[] { "True", "False" }, null, id + (index + 1));
                panel.Children.Add(Below(Field(label, choice), 7));
                inputs.Add(() => choice.SelectedItem?.ToString() ?? "");
            }
            else
            {
                var input = Input("", id + (index + 1));
                panel.Children.Add(Below(Field(label, input), 7));
                inputs.Add(() => input.Text);
            }
        }
        (object?[] Values, string Error) Read()
        {
            var result = new object?[inputs.Count];
            for (var index = 0; index < inputs.Count; index++)
            {
                var parameter = fn.Parameters[index];
                var value = inputs[index]().Trim();
                if (parameter.Type == "STRING" && value.Length > 0) result[index] = value;
                else if (parameter.Type == "REAL" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) && double.IsFinite(real))
                    result[index] = real;
                else if (parameter.Type == "BOOLEAN" && bool.TryParse(value, out var boolean)) result[index] = boolean;
                else return ([], $"请填写有效的 {parameter.Name}（{parameter.Type}）。");
            }
            return (result, "");
        }
        return (panel, Read);
    }

    private async Task RunFunction(PmlFunction fn, Func<(object?[] Values, string Error)> read, Notice notice)
    {
        if (_session is not { } session || !fn.AllowInvoke) return;
        if (!CanInvokeFunction(fn)) return;
        var (values, error) = read();
        if (error.Length > 0) { notice.Show(error, "warning"); return; }
        var pid = session.Pid.ToString(CultureInfo.InvariantCulture);
        var ticks = session.StartTicks.ToString(CultureInfo.InvariantCulture);
        var name = fn.Id.TrimStart('!');
        var args = fn.Parameters.Select((parameter, index) => PhaytonRequests.Parameter(parameter.Type,
            values[index] switch
            {
                bool value => value ? "true" : "false",
                IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
                var value => value?.ToString() ?? ""
            })).ToArray();
        try
        {
            var preview = await Task.Run(() => Run(PhaytonRequests.Preview(name, pid, ticks, session.Model, args)));
            RequireFunctionReceipt(preview);
            var plan = preview.Child("plan");
            var token = Value(plan, "token");
            if (token.Length == 0) throw new InvalidDataException("函数调用没有返回可核对的计划。");
            if (!Confirm("确认调用已登记函数", $"{session}\n{fn.Signature}\n{plan?.Child("command")?.Value}", "执行")) return;
            if (!CanInvokeFunction(fn)) throw new InvalidDataException("当前函数不满足调用授权或只读标记要求。");
            var receipt = await Task.Run(() => Run(PhaytonRequests.Invoke(name, pid, ticks, session.Model, token, args)));
            var state = Value(receipt, "state");
            var uncertain = receipt.DescendantsAndSelf().Any(row => (bool?)row.Field("mayHaveExecuted") == true);
            var exit = receipt.DescendantsAndSelf().LastOrDefault(row => row.Field("exitCode") is not null)?.Field("exitCode");
            var log = RecordLog(fn.Id, state == "ok" ? "成功" : uncertain || state is "partial" or "cleanup-failed" ? "待核对" : "失败",
                (int?)exit, CoreXml.Write(receipt).ToString(), receipt.Child("message")?.Value ?? "");
            ShowLog(log.Id);
        }
        catch (Exception failure) when (WorkbenchError(failure))
        {
            ShowLog(RecordLog(fn.Id, "失败", null, "", failure.Message).Id);
        }
    }

    private static void RequireFunctionReceipt(CoreData result)
    {
        if (Value(result, "state") != "ok")
            throw new InvalidDataException(result.Child("message")?.Value ?? "Phayton 未返回成功回执。");
    }

    private async Task RefreshFunctions()
    {
        try
        {
            var result = await Task.Run(() => Run(PhaytonRequests.Functions()));
            RequireFunctionReceipt(result);
            var notes = Data.Functions.ToDictionary(item => item.Id, item => item.Detail, StringComparer.OrdinalIgnoreCase);
            var functions = result.Children("function").Select(item =>
            {
                var rawName = Value(item, "name");
                var name = "!!" + rawName.TrimStart('!');
                return new PmlFunction(name, notes.GetValueOrDefault(name, ""), Value(item, "source"),
                    (bool?)item.Field("allowInvoke") == true, item.Children("parameter")
                        .Select(row => new PmlParameter(Value(row, "name"), Value(row, "type"))).ToArray(), Value(item, "returnType"))
                {
                    SourceCurrent = (bool?)item.Field("sourceCurrent") == true,
                    Digest = Value(item, "sha256"),
                    ReviewedReadOnly = (bool?)item.Field("reviewedReadOnly") == true
                };
            }).ToList();
            foreach (var function in functions.Where(fn => fn.SourceCurrent && fn.Digest.Length > 0 && HasStoredReview(fn)).ToArray())
            {
                var index = functions.IndexOf(function);
                if (!function.ReviewedReadOnly)
                {
                    var migrated = await Task.Run(() => Run(PhaytonRequests.PermissionReviewed(
                        function.Id.TrimStart('!'), function.AllowInvoke, function.Digest, true)));
                    RequireFunctionReceipt(migrated);
                    functions[index] = function with { ReviewedReadOnly = true };
                }
                SaveFunctionReview(function, false);
            }
            Data.Functions = functions;
            Render();
        }
        catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
    }

    private PmlLog RecordLog(string command, string state, int? exit, string output, string error)
    {
        var entry = new PmlLog(Guid.NewGuid().ToString("N"), 1, DateTimeOffset.Now,
            command, state, exit, output, error);
        AcceptExecutionLog(entry);
        return entry;
    }

    public bool AcceptExecutionLog(PmlLog entry)
    {
        if (!Dispatcher.CheckAccess()) return Dispatcher.Invoke(() => AcceptExecutionLog(entry));
        if (string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Command) || entry.Sequence < 0) return false;
        var index = Data.Logs.FindIndex(item => string.Equals(item.Id, entry.Id, StringComparison.Ordinal));
        if (index >= 0 && Data.Logs[index].Sequence >= entry.Sequence) return false;
        if (index >= 0) Data.Logs.RemoveAt(index);
        Data.Logs.Insert(0, entry);
        Data.Logs.Sort((first, second) => second.When.CompareTo(first.When));
        if (Data.Logs.Count > 160) Data.Logs.RemoveRange(160, Data.Logs.Count - 160);
        Save();
        if (_page.Id is "Logs" or "Home") Render();
        return true;
    }

    private void ShowLog(string id)
    {
        _logId = id;
        _logFilter = "全部状态";
        Go("Logs");
    }

    private FrameworkElement LogsPage()
    {
        var filter = Choice(new[] { "全部状态", "成功", "失败", "已取消", "运行中", "待核对" }
            .Concat(Data.Logs.Select(item => item.State)).Distinct(), _logFilter, "ExecutionStateFilter");
        var grid = Table(Array.Empty<PmlLog>(), "ExecutionLogs", ("时间", "Time", .71), ("命令", "Command", 2.25), ("状态", "State", .68));
        grid.MaxHeight = 267;
        var count = Muted("");
        var details = new ContentControl();
        void Show()
        {
            if (grid.SelectedItem is not PmlLog entry)
            {
                details.Content = Card("执行详情", Muted("收到日志后，可选择一条查看回执和输出。"));
                return;
            }
            _logId = entry.Id;
            var output = $"STDOUT\n{entry.Output}\n\nSTDERR\n{entry.Error}";
            details.Content = Card("执行详情", Stack(Details([
                ("命令", entry.Command), ("状态", entry.State), ("退出码", entry.ExitCode?.ToString() ?? "—"), ("命令 ID", entry.Id)
            ]), Below(Code(output, "ExecutionOutput", 132), 7),
                Below(Actions(Btn("复制输出", "CopyExecutionLog", () => CopyText(output)),
                    Btn("查看完整输出…", "FullExecutionLog", () => Modal(entry.Command, Code(output, "FullLogOutput", 350), "完成", () => true, cancel: false, width: 690))), 8)));
        }
        void Reload()
        {
            var rows = Data.Logs.Where(item => _logFilter == "全部状态" || item.State == _logFilter).ToList();
            var selected = rows.FirstOrDefault(item => item.Id == _logId);
            grid.ItemsSource = rows;
            grid.SelectedItem = selected ?? rows.FirstOrDefault();
            count.Text = $"{rows.Count} 条命令日志";
            if (grid.SelectedItem is { } item) grid.ScrollIntoView(item);
            Show();
        }
        grid.SelectionChanged += (_, _) => Show();
        filter.SelectionChanged += (_, _) => { _logFilter = filter.SelectedItem?.ToString() ?? "全部状态"; Reload(); };
        Reload();
        return SideBySide(Card("命令记录", Stack(Field("按状态筛选", filter), Below(count, 6), Below(grid, 6)),
            AsyncBtn("测试只读查询", "TestReadOnly", TestReadOnly, "Button.Soft")), details, 1.2);
    }

    private async Task TestReadOnly()
    {
        var target = _session;
        if (target is null)
        {
            Go("Cli");
            Tell("请先选择已核验的 AVEVA 会话。");
            return;
        }
        try
        {
            var forms = await Task.Run(() => LiveHost().ShownForms(target));
            Tell($"只读窗体查询成功 · PID {target.Pid} · {forms.Count} 个可见窗体。");
        }
        catch (Exception error) when (WorkbenchError(error)) { Tell(error.Message); }
    }
}
