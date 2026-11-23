using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Core30.PmlTwin;
using Core30.Workbench;
using Microsoft.Win32;
using static Core30.Workbench.Views;

namespace PmlTwin.Workbench;

public partial class MainWindow
{
    private sealed record RecordingTarget(PmlLiveSession Session, string Form, DateTimeOffset Since);
    private sealed record RecordingRow(PmlRecordedEvent Event)
    {
        public string Control => Event.Control;
        public string Time => Event.Time.ToLocalTime().ToString("HH:mm:ss");
        public string Status => Event.State switch
        {
            "completed" => "已记录", "failed" => "操作失败", "awaitingAfter" => "仅有操作前", _ => "待核对"
        };
    }

    private RecordingTarget? _recording;
    private PmlLiveSession? _formSession;
    private string[] _visibleForms = [];
    private string _recordState = "核验会话";
    private string _recordForm = "";
    private string _recordProgram = "";
    private bool _recordBusy;
    private string _recordFeedback = "";
    private IReadOnlyList<PmlRecordedEvent> _recordedEvents = [];
    private string _macroId = "";
    private string _macroQuery = "";
    private string _macroDate = "";
    private bool RecordingNeedsRestore => _recording is not null && _recordState is "正在录制" or "状态待核对" or "恢复待核对";
    private static readonly (string Node, string Detail)[] Steps =
    [
        ("核验会话", "确认会话后读取窗体"), ("无会话", "启动 AVEVA 或刷新会话"),
        ("选择窗体", "从实际可见窗体选择"), ("无窗体", "在 AVEVA 打开窗体后刷新"),
        ("正在录制", "在原生窗体执行实际操作"), ("只读已开启", "可浏览窗体和已有快照"),
        ("停止并恢复", "核对原窗体的回调"), ("状态待核对", "核对原会话后再决定"),
        ("已停止", "回看快照或保存宏草稿"), ("恢复待核对", "逐项检查恢复回执")
    ];

    private FrameworkElement RecorderPage()
    {
        var target = _recording?.Session ?? _session;
        var needsRestore = RecordingNeedsRestore;
        var attached = _formSession == target;
        var forms = Choice(attached ? _visibleForms : [], _recordForm, "RecordingForm");
        forms.IsEnabled = !needsRestore && !_recordBusy;
        forms.SelectionChanged += (_, _) => _recordForm = forms.SelectedItem as string ?? "";
        var program = Input(_recordProgram, "RecordingProgram");
        program.TextChanged += (_, _) => _recordProgram = program.Text;
        var state = needsRestore ? _recordState : _readOnly ? "只读已开启" :
            _recording is not null ? _recordState : target is null ? "无会话" :
            !attached ? "核验会话" : _visibleForms.Length == 0 ? "无窗体" : "选择窗体";
        var content = Stack(needsRestore ? Details([("录制会话", target!.ToString())]) : SessionSelector(),
            Below(Field("已打开的原生窗体", forms), 9), Below(Field("关联程序（可选）", program), 7),
            Below(Badge(state, state == "已停止" ? "success" : "primary"), 8));
        if (_readOnly) content.Children.Add(Below(Muted(needsRestore
            ? "只读已开启。回调恢复待处理，可继续读取已保存的快照。"
            : "可以查看窗体及快照。开始录制需要恢复执行。"), 6));
        if (_recordFeedback.Length > 0) content.Children.Add(Below(Muted(_recordFeedback), 6));

        var readForms = AsyncBtn("读取窗体", "DiscoverForms", () => RecordTask(async () =>
        {
            if (target is null) return;
            _visibleForms = (await Task.Run(() => LiveHost().ShownForms(target))).ToArray();
            _formSession = target;
            if (!_visibleForms.Contains(_recordForm)) _recordForm = _visibleForms.FirstOrDefault() ?? "";
            _recordFeedback = _visibleForms.Length == 0 ? "请先在 AVEVA 打开要录制的窗体。" : $"发现 {_visibleForms.Length} 个可见窗体。";
        }));
        readForms.IsEnabled = target is not null && !needsRestore && !_recordBusy;
        var start = AsyncBtn("开始录制", "StartRecording", StartRecording, "Button.Primary");
        start.IsEnabled = !needsRestore && !_readOnly && !_recordBusy && target is not null && attached && _visibleForms.Length > 0;
        var stop = AsyncBtn("停止并恢复回调", "StopRecording", StopRecording, "Button.Primary");
        stop.IsEnabled = needsRestore && !_readOnly && !_recordBusy;
        var check = AsyncBtn("核对录制状态", "CheckRecording", CheckRecording);
        check.IsEnabled = !_readOnly && !_recordBusy && (needsRestore || target is not null && attached && _visibleForms.Length > 0);
        if (needsRestore) content.Children.Add(Below(Actions(stop, check), 8));
        else if (_recording is not null)
            content.Children.Add(Below(Actions(Btn("开始新录制", "ResetRecording", () =>
            {
                _recording = null;
                _recordedEvents = [];
                _recordFeedback = "";
                _recordState = "核验会话";
                Render();
            })), 8));
        else content.Children.Add(Below(Actions(readForms, start, check), 8));

        if (_recording is not null)
        {
            var rows = Table(_recordedEvents.Select(item => new RecordingRow(item)), "RecordingEvents",
                ("控件", "Control", 2), ("时间", "Time", .8), ("状态", "Status", .9));
            rows.MaxHeight = 180;
            var snapshot = Btn("查看前后快照", "RecordingSnapshot", () =>
            {
                if (rows.SelectedItem is not RecordingRow row) return;
                var item = row.Event;
                var value = $"原回调\n{item.Callback}\n\n操作前\n{string.Join("\n", item.Before.Values)}\n\n操作后\n" +
                    (item.After is { } after ? string.Join("\n", after.Values) : "等待操作后快照");
                var beforeImage = item.Before.ImagePath;
                var afterImage = item.After?.ImagePath;
                var preview = Stack(Code(value, "RecordedValues", 212));
                if (beforeImage is not null) preview.Children.Add(Btn("打开操作前截图", "BeforeScreenshot", () => OpenExternal(beforeImage)));
                if (afterImage is not null) preview.Children.Add(Btn("打开操作后截图", "AfterScreenshot", () => OpenExternal(afterImage)));
                Modal("窗体操作快照", preview, "完成", () => true, cancel: false, width: 710);
            });
            var refresh = AsyncBtn("刷新操作记录", "RefreshRecordingEvents", () => RecordTask(LoadRecordingEvents));
            refresh.IsEnabled = !_recordBusy;
            var save = Primary("保存宏草稿…", "SaveRecording", () => SaveMacro());
            save.IsEnabled = !needsRestore && _recordedEvents.Count > 0;
            content.Children.Add(Below(rows, 9));
            content.Children.Add(Below(Actions(refresh, snapshot, save), 7));
        }
        return SideBySide(Card("录制分支", RecordingFlow(state), Jump("宏记录", "Macros")), Card("操作与回执", content), 1.15, 720);
    }

    private async Task RecordTask(Func<Task> action)
    {
        if (_recordBusy) return;
        _recordBusy = true;
        try { await action(); }
        catch (PmlHostOperationException error) when (error.MayHaveExecuted)
        {
            _recordState = "状态待核对";
            _recordFeedback = error.Message + " 请核对同一会话和原窗体后再操作。";
        }
        catch (Exception error) when (WorkbenchError(error)) { _recordFeedback = error.Message; }
        finally { _recordBusy = false; Render(); }
    }

    private async Task StartRecording()
    {
        if (_readOnly || _session is not { } session || _formSession != session ||
            !_visibleForms.Contains(_recordForm) || RecordingNeedsRestore) return;
        var selected = new RecordingTarget(session, _recordForm, DateTimeOffset.UtcNow);
        if (!Confirm("开始宏录制", $"{session}\n窗体 {selected.Form}\n录制会跟踪此窗体的回调。", "开始录制")) return;
        await RecordTask(async () =>
        {
            _recording = selected;
            _recordedEvents = [];
            try
            {
                var state = await Task.Run(() => LiveHost().StartRecording(session, selected.Form));
                if (state == "NoCallbacks")
                {
                    _recording = null;
                    _recordState = "选择窗体";
                    _recordFeedback = "窗体没有可录制的回调。";
                    return;
                }
                if (state == "AlreadyRecording") _recording = selected with { Since = new DateTimeOffset(session.StartTicks, TimeSpan.Zero) };
                _recordState = "正在录制";
                _recordFeedback = state == "AlreadyRecording" ? "已接回此窗体的录制。" : "请在 AVEVA 操作窗体，然后刷新记录。";
            }
            catch (PmlHostOperationException error) when (error.MayHaveExecuted) { throw; }
            catch { _recording = null; throw; }
        });
    }

    private Task CheckRecording() => RecordTask(async () =>
    {
        if (_readOnly) return;
        var target = _recording;
        if (target is null && _session is { } session && _formSession == session && _visibleForms.Contains(_recordForm))
            target = new(session, _recordForm, new DateTimeOffset(session.StartTicks, TimeSpan.Zero));
        if (target is null) return;
        var active = await Task.Run(() => LiveHost().IsRecording(target.Session, target.Form));
        _recordState = active ? "正在录制" : _recording is not null ? "已停止" : "选择窗体";
        if (active) _recording = target;
        _recordFeedback = active ? "已核对，目标窗体正在录制。" : "已核对，原窗体当前没有被录制的回调。";
    });

    private async Task StopRecording()
    {
        if (_readOnly || !RecordingNeedsRestore || _recording is not { } target) return;
        if (!Confirm("停止并恢复原窗体回调", $"{target.Session}\n窗体 {target.Form}", "停止并核对")) return;
        await RecordTask(async () =>
        {
            _recordState = "恢复待核对";
            var reply = await Task.Run(() => LiveHost().StopRecording(target.Session, target.Form));
            var active = await Task.Run(() => LiveHost().IsRecording(target.Session, target.Form));
            _recordState = active || reply.Any(item => item.Status is not ("restored" or "changed" or "missing"))
                ? "恢复待核对" : "已停止";
            var restored = reply.Count(item => item.Status == "restored");
            _recordFeedback = $"已恢复 {restored}/{reply.Count} 项。" + string.Concat(reply.Where(item => item.Status != "restored")
                .Select(item => "\n" + item.Control + " · " + (item.Status switch
                {
                    "changed" => "回调已变更，保留当前值", "missing" => "控件已消失", "failed" => "恢复失败", _ => item.Status
                })));
            await LoadRecordingEvents();
        });
    }

    private async Task LoadRecordingEvents()
    {
        if (_recording is not { } target) return;
        _recordedEvents = await Task.Run(() => PmlFormRecordingLog.Read(target.Session, target.Form, target.Since));
    }

    private static FrameworkElement RecordingFlow(string current)
    {
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        for (var index = 0; index < 5; index++) layout.RowDefinitions.Add(new RowDefinition());
        for (var index = 0; index < Steps.Length; index++)
        {
            var (title, text) = Steps[index];
            var active = title == current;
            var badge = Views.Text(title);
            badge.FontWeight = FontWeights.SemiBold;
            var node = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
                BorderBrush = Brush(active ? "primary" : "border"),
                Background = Brush(active ? "selected" : title is "无会话" or "无窗体" or "恢复待核对" or "只读已开启" or "状态待核对" ? "warning-bg" :
                    title == "已停止" ? "success-bg" : "subtle"),
                Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 7), Child = Stack(badge, Muted(text)) };
            Grid.SetColumn(node, index % 2 == 0 ? 0 : 2);
            Grid.SetRow(node, index / 2);
            layout.Children.Add(node);
            if (index % 2 == 1)
            {
                var arrow = Muted("→");
                arrow.VerticalAlignment = VerticalAlignment.Center;
                arrow.HorizontalAlignment = HorizontalAlignment.Center;
                Grid.SetColumn(arrow, 1);
                Grid.SetRow(arrow, index / 2);
                layout.Children.Add(arrow);
            }
        }
        return Id(layout, "RecordingFlow");
    }

    private void SaveMacro(RecordedMacro? previous = null)
    {
        var form = previous?.Form ?? _recording?.Form ?? _recordForm;
        var name = Input(previous?.Name ?? form + " 宏", "MacroName");
        var date = Input(previous?.DateText ?? DateTime.Today.ToString("yyyy-MM-dd"), "MacroDate");
        var program = previous?.Program ?? _recordProgram;
        var code = previous?.Code ?? RecordedCode(form);
        var notice = new Notice("MacroArchiveNotice");
        if (!Modal(previous is null ? "保存宏草稿" : "修改宏名称和日期",
            Stack(Form(Field("宏名称", name), Field("归档日期（yyyy-MM-dd）", date)),
                Details([("录制窗体", form), ("关联程序", program.Length > 0 ? program : "—")]), Below(Code(code, "RecordingMacroCode", 115), 7), Below(notice, 6)),
            "保存宏", () =>
            {
                if (name.Text.Trim().Length == 0 || !DateTime.TryParseExact(date.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                {
                    notice.Show("请填写名称，并用 yyyy-MM-dd 输入日期。", "warning");
                    return false;
                }
                if (Data.Macros.Any(item => item.Id != previous?.Id && item.Date == day &&
                    string.Equals(item.Name, name.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    notice.Show("所选日期已有同名宏。", "warning");
                    return false;
                }
                var record = new RecordedMacro(previous?.Id ?? Guid.NewGuid().ToString("N"), name.Text.Trim(), day, form, program, code, previous?.Review);
                if (previous is null) Data.Macros.Insert(0, record);
                else Data.Macros[Data.Macros.IndexOf(previous)] = record;
                _macroId = record.Id;
                _macroQuery = _macroDate = "";
                return true;
            }, name)) return;
        if (previous is null) Go("Macros");
        else Render();
        Save("宏草稿已保存。");
    }

    private string RecordedCode(string form)
    {
        var lines = _recordedEvents.OrderBy(item => item.Time).SelectMany(item =>
            new[] { $"-- {item.Time:O} | {item.Control} | {item.State}" }
                .Concat(item.Callback.Split('\n').Select(line => "-- " + line.TrimEnd('\r'))));
        return string.Join("\n", new[] { $"-- {form} native recording", "-- Review and select callbacks before replay." }.Concat(lines));
    }

    private FrameworkElement MacrosPage(bool sag)
    {
        var search = Input(_macroQuery, "MacroNameFilter");
        var date = Input(_macroDate, "MacroDateFilter");
        var grid = Table(Array.Empty<RecordedMacro>(), "RecordedMacroGrid", ("宏名称", "Name", 1.8), ("日期", "DateText", 1),
            ("评审", "ReviewStatus", .9));
        grid.MaxHeight = 230;
        var selected = new ContentControl();
        var edit = Btn("改名或改日期", "EditMacro", () => { if (grid.SelectedItem is RecordedMacro macro) SaveMacro(macro); });
        var download = Btn("下载宏", "ExportMacro", () =>
        {
            if (grid.SelectedItem is RecordedMacro macro) ExportText("保存宏原件", SafeName(macro.Name) + ".mac", "PML 宏|*.mac", macro.Code);
        });
        void Show()
        {
            var macro = grid.SelectedItem as RecordedMacro;
            edit.IsEnabled = download.IsEnabled = macro is not null;
            if (macro is null)
            {
                selected.Content = Card("读取宏与流程评审", Muted(sag ? "请选择已保存的宏，或导入本地宏。" : "录制完成后，保存宏并按日期或名称查找。"));
                return;
            }
            _macroId = macro.Id;
            var content = Stack(Details([("当前宏", macro.Name), ("关联程序", macro.Program.Length > 0 ? macro.Program : "—"), ("窗体", macro.Form.Length > 0 ? macro.Form : "—")]),
                Below(Btn("读取宏代码", "ReadMacro", () => Modal(macro.Name, Code(macro.Code, "MacroText", 280), "完成", () => true, cancel: false, width: 690)), 6));
            if (macro.Review is not null)
            {
                var review = Table(macro.Review, "MacroReviewGrid", ("检查项", "Check", 1.2), ("判断", "Result", .65), ("建议", "Advice", 1.55));
                review.MaxHeight = 170;
                content.Children.Add(Below(Views.Text("流程评审"), 8));
                content.Children.Add(Below(review, 4));
            }
            var commands = Actions(Btn(sag ? "检查宏" : "AI 流程评审", "ReviewMacro", () =>
            {
                Data.Macros[Data.Macros.IndexOf(macro)] = macro with { Review = Evaluate(macro.Code) };
                Render();
                Save();
            }, "Button.Soft"));
            if (macro.Review is not null) commands.Children.Add(Primary("生成经验…", "GenerateExperience", () => ShareExperience(macro)));
            content.Children.Add(Below(commands, 8));
            selected.Content = Card(sag ? "宏检查" : "读取宏与流程评审", content);
        }
        void Reload()
        {
            var rows = Data.Macros.Where(macro =>
                $"{macro.Name} {macro.Form} {macro.Program}".Contains(_macroQuery.Trim(), StringComparison.OrdinalIgnoreCase) &&
                macro.DateText.Contains(_macroDate.Trim(), StringComparison.Ordinal)).OrderByDescending(macro => macro.Date).ToList();
            var chosen = rows.FirstOrDefault(macro => macro.Id == _macroId);
            grid.ItemsSource = rows;
            grid.SelectedItem = chosen ?? rows.FirstOrDefault();
            Show();
        }
        grid.SelectionChanged += (_, _) => Show();
        search.TextChanged += (_, _) => { _macroQuery = search.Text; Reload(); };
        date.TextChanged += (_, _) => { _macroDate = date.Text; Reload(); };
        Reload();
        return SideBySide(Card(sag ? "宏文件" : "已保存的宏", Stack(Form(Field("名称或关联程序", search),
            Field("归档日期", Prompt(date, "yyyy-MM-dd"))), grid, Below(Actions(edit, download), 8)),
            sag ? Primary("导入宏…", "ImportMacro", ImportMacro) : Jump("返回录制", "Record")), selected, 1.2, 720);
    }

    private static MacroReview[] Evaluate(string code)
    {
        var lines = code.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith("--")).ToArray();
        int Find(string expression) => Array.FindIndex(lines, line => Regex.IsMatch(line, expression, RegexOptions.IgnoreCase, ParseTimeout));
        var show = Find(@"^show\s+!!");
        var condition = Find(@"^if\b.+\bthen$");
        var change = Find(@"^!![\w.]+\.val\s*=");
        var end = Find(@"^endif\b");
        var hide = Find(@"^hide\s+!!");
        return
        [
            new("定位窗体", show >= 0, "明确打开目标窗体。"),
            new("核对前置条件", show >= 0 && condition > show && end > condition, "先检查状态并闭合条件分支。"),
            new("记录操作步骤", condition >= 0 && change > condition && end > change, "在条件分支内更新窗体字段。"),
            new("结束与清理", end >= 0 && hide > end, "退出分支后关闭窗体。")
        ];
    }

    private void ImportMacro()
    {
        var dialog = new OpenFileDialog { Title = "导入本地宏", Filter = "PML 宏|*.mac;*.pmlmac;*.pml;*.pmlcmd", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 512 * 1024) { Tell("请选择 512 KB 以内的宏文本。"); return; }
            var contents = File.ReadAllText(dialog.FileName);
            if (string.IsNullOrWhiteSpace(contents)) { Tell("所选文件没有可读取的内容。"); return; }
            var entry = new RecordedMacro(Guid.NewGuid().ToString("N"), Path.GetFileNameWithoutExtension(dialog.FileName), DateTime.Today, "", "", contents);
            Data.Macros.Insert(0, entry);
            _macroId = entry.Id;
            _macroQuery = _macroDate = "";
            Render();
            Save();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Tell("无法读取宏：" + error.Message); }
    }

    private static string SafeName(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(text.Take(105).Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray()).Trim(' ', '.');
        return cleaned.Length == 0 ? "PmlTwin" : cleaned;
    }

    private void ShareExperience(RecordedMacro macro)
    {
        if (macro.Review is null) return;
        string Escape(string text) => WebUtility.HtmlEncode(text);
        var title = macro.Name + " · 复用经验";
        var steps = string.Join("", macro.Review.Select(item => $"<li>{Escape(item.Check)}（{item.Result}）：{Escape(item.Advice)}</li>"));
        var html = $"<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>{Escape(title)}</title><body>" +
            $"<h1>{Escape(title)}</h1><p>日期：{macro.DateText}　窗体：{Escape(macro.Form)}　关联程序：{Escape(macro.Program)}</p>" +
            $"<h2>流程检查与复用步骤</h2><ol>{steps}</ol><h2>宏代码</h2><pre>{Escape(macro.Code)}</pre></body></html>";
        var filename = SafeName(title) + ".html";
        var notice = new Notice("ExperienceImportNotice");
        var accepted = Modal("核对 HTML 经验", Stack(Details([("标题", title), ("目标资料库", Data.DefaultLibrary)]),
            Below(Btn("下载 HTML", "DownloadExperience", () => ExportText("保存 HTML 经验", filename, "HTML|*.html", html)), 8), Below(notice, 5)),
            "加入 SAG 资料库", () =>
            {
                try
                {
                    var id = "macro-" + macro.Id;
                    var savename = _store.StoreExperience(id, html);
                    var entry = new DocumentEntry(id, filename, savename, "HTML", "宏录制经验", Encoding.UTF8.GetByteCount(html),
                        "检查通过", PlainHtml(html), Data.DefaultLibrary, DateTimeOffset.Now);
                    if (Data.Documents.Any(item => item.Id == id)) ReplaceDocument(entry);
                    else Data.Documents.Insert(0, entry);
                    _docId = id;
                    _docQuery = "";
                    _docFormat = "全部格式";
                    return true;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or RegexMatchTimeoutException)
                { notice.Show("无法保存 HTML：" + error.Message, "warning"); return false; }
            });
        if (!accepted) return;
        Go("Docs");
        Save("HTML 经验已部署到 SAG 资料库。");
    }

    private void ExportText(string title, string name, string filter, string text)
    {
        var dialog = new SaveFileDialog { Title = title, FileName = name, Filter = filter };
        if (dialog.ShowDialog() != true) return;
        try { File.WriteAllText(dialog.FileName, text); Tell("文件已保存。"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Tell("无法保存文件：" + error.Message); }
    }
}
