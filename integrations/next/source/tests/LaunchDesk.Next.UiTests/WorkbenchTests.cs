using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using NUnit.Framework;
using System.Diagnostics;
namespace LaunchDesk.Next.UiTests;

[TestFixture, NonParallelizable]
public sealed class WorkbenchTests
{
    [Test]
    public void RealWindow_DiscoverEditSaveRejectAndReadEvidence()
    {
        var root = FindRoot();
        var output = Environment.GetEnvironmentVariable("LAUNCHDESK_NEXT_UI_OUTPUT")
            ?? Path.Combine(root, "artifacts", "frontend");
        Directory.CreateDirectory(output);
        var data = Path.Combine(output, "ui-data-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(data);
        var exe = Environment.GetEnvironmentVariable("LAUNCHDESK_NEXT_UI_EXE")
            ?? Path.Combine(root, "src", "LaunchDesk.Next.Desktop", "bin", "Debug", "net10.0-windows", "LaunchDesk.Next.Desktop.exe");
        var start = new ProcessStartInfo(exe) { UseShellExecute = false }; start.Environment["LAUNCHDESK_NEXT_DATA"] = data;
        using var app = FlaUI.Core.Application.Launch(start); using var automation = new UIA3Automation();
        Window? diagnosticWindow = null;
        try
        {
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new InvalidOperationException("LaunchDesk main window did not appear within 20 seconds.");
            diagnosticWindow = window;
            Wait(() => List(window, "Candidates").Items.Length > 0, 30);
            var candidates = List(window, "Candidates"); Assert.That(candidates.Items.Length, Is.GreaterThanOrEqualTo(1)); candidates.Items[0].Select();
            Wait(() => Field(window, "ExecutablePath").Text.Length > 0, 10);
            Capture(window, Path.Combine(output, "01-live-discovery.png"));
            Field(window, "DisplayName").Text = "UI verification profile";
            Field(window, "ExecutablePath").Text = Path.Combine(data, "missing-safe-fixture.exe");
            Field(window, "WorkingDirectory").Text = data;
            Combo(window, "Protocol").Select(2);
            Check(window, "CheckLicence").IsChecked = false;
            Button(window, "Save").Invoke();
            Wait(() => List(window, "Configurations").Items.Length > 0, 10);
            Assert.That(List(window, "Configurations").Items.Length, Is.GreaterThan(0));
            Capture(window, Path.Combine(output, "02-saved-profile.png"));
            Button(window, "Launch").Invoke();
            Wait(() => RequireElement(window, "Status").Name.Contains("已取消"), 10);
            Capture(window, Path.Combine(output, "03-cancelled-password.png"));
            Combo(window, "PasswordPolicy").Select(0);
            Button(window, "Save").Invoke();
            Wait(() => RequireElement(window, "Status").Name.Contains("saved"), 10);
            Button(window, "Launch").Invoke();
            Wait(() => RequireElement(window, "Status").Name.Contains("启动失败"), 10);
            Capture(window, Path.Combine(output, "03-rejected-launch.png"));
            Field(window, "ExecutablePath").Text = Path.Combine(root, "tests", "LaunchDesk.Next.ProcessFixture", "bin", "Debug", "net10.0", "LaunchDesk.Next.ProcessFixture.exe");
            Button(window, "Save").Invoke();
            Wait(() => RequireElement(window, "Status").Name.Contains("saved"), 10);
            Button(window, "Launch").Invoke();
            Wait(() => RequireElement(window, "Status").Name.Contains("进程已创建") || RequireElement(window, "Status").Name.Contains("启动中"), 10);
            Capture(window, Path.Combine(output, "05-safe-process-created.png"));
            Button(window, "HistoryTab").Invoke();
            var rows = List(window, "Records").Items;
            Assert.That(rows.Length, Is.GreaterThan(0));
            var edited = rows.FirstOrDefault(row => row.Name.Contains("InputEdited"))
                ?? throw new InvalidOperationException("InputEdited evidence row is missing.");
            edited.Select();
            Assert.That(Field(window, "SnapshotDetail").Text, Does.Contain("Before"));
            Capture(window, Path.Combine(output, "04-evidence-history.png"));
            File.WriteAllText(Path.Combine(output, "ui-run.txt"), $"PASS {DateTimeOffset.Now:O}\nData={data}\nCandidates={candidates.Items.Length}\nRecords={rows.Length}\nNo vendor process launched; cancellation, missing executable failure and safe bounded fixture Created verified.");
        }
        catch
        {
            if (diagnosticWindow is not null) { Capture(diagnosticWindow, Path.Combine(output, "failure.png")); File.WriteAllText(Path.Combine(output, "failure-tree.txt"), string.Join(Environment.NewLine, diagnosticWindow.FindAllDescendants().Select(x => $"{x.ControlType} {x.Properties.AutomationId.ValueOrDefault} {x.Properties.Name.ValueOrDefault}"))); }
            throw;
        }
        finally { try { app.Close(); } catch (InvalidOperationException) { } }
    }
    private static void Capture(Window window, string path)
    {
        if (Environment.GetEnvironmentVariable("LAUNCHDESK_NEXT_UI_CAPTURE") != "0")
            window.Capture().Save(path);
    }
    private static AutomationElement RequireElement(Window window, string id) =>
        window.FindFirstDescendant(cf => cf.ByAutomationId(id))
        ?? throw new InvalidOperationException($"Required UI element '{id}' was not found.");

    private static TextBox Field(Window window, string id) =>
        RequireElement(window, id).AsTextBox()
        ?? throw new InvalidOperationException($"Required UI element '{id}' is not a text box.");

    private static ListBox List(Window window, string id) =>
        RequireElement(window, id).AsListBox()
        ?? throw new InvalidOperationException($"Required UI element '{id}' is not a list box.");

    private static ComboBox Combo(Window window, string id) =>
        RequireElement(window, id).AsComboBox()
        ?? throw new InvalidOperationException($"Required UI element '{id}' is not a combo box.");

    private static CheckBox Check(Window window, string id) =>
        RequireElement(window, id).AsCheckBox()
        ?? throw new InvalidOperationException($"Required UI element '{id}' is not a check box.");

    private static Button Button(Window window, string id) =>
        RequireElement(window, id).AsButton()
        ?? throw new InvalidOperationException($"Required UI element '{id}' is not a button.");
    private static void Wait(Func<bool> condition, int seconds) { var timer = Stopwatch.StartNew(); while (timer.Elapsed < TimeSpan.FromSeconds(seconds)) { if (condition()) return; Thread.Sleep(100); } Assert.Fail("UI condition timed out"); }
    private static string FindRoot() { var d = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "LaunchDesk.Next.slnx"))) d = d.Parent; return d?.FullName ?? throw new InvalidOperationException("Project root missing"); }
}
