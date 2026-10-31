using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        static object CanonicalEditor(object value)
        {
            var map = value as System.Collections.Generic.Dictionary<string, object>;
            if (map != null)
            {
                var result = new System.Collections.Generic.SortedDictionary<string, object>(StringComparer.Ordinal);
                foreach (var pair in map)
                    result[pair.Key] = CanonicalEditor(pair.Value);
                return result;
            }
            var array = value as object[];
            return array == null ? value : array.Select(CanonicalEditor).ToArray();
        }
        string EditorData()
        {
            return Json.Serializer().Serialize(CanonicalEditor(Json.Read<object>(Json.Write(new Project { schemaVersion = Project.schemaVersion, @namespace = Project.@namespace, buttons = Project.buttons, tabs = Project.tabs }))));
        }
        async Task CheckReplay(string before, string after, string action)
        {
            await UndoEdit();

            LayoutCheck(EditorData() == before, "undo " + action);
            await RedoEdit();
            LayoutCheck(EditorData() == after, "redo " + action);
        }
        void VerifyIncrementalPerformance()
        {
            var original = Project;
            var selected = Selected;
            var large = Json.Copy(original);
            for (int i = 0; i < 10000; i++)
                large.buttons.Add(new RibbonButton { id = "Perf." + i, caption = "性能按钮", macro = "show !!Perf", commandKey = "Macro:" + i, tooltipText = "", tooltipTitle = "" });
            Project = large;
            Selected = new Selection { Kind = "project" };
            ResetEditHistory();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 200; i++)
            {
                var next = "PerfNamespace" + i;
                RunEdit("性能验证", () => SetEdit(Project.@namespace, next, v => Project.@namespace = v));
            }
            watch.Stop();
            LayoutCheck(undoEdits.Count == 50 && undoEdits.All(e => e.Operations.Count == 1), "history stores only incremental operations");
            LayoutCheck(watch.ElapsedMilliseconds < 2000, "200 edits on 10000 buttons must avoid full serialization");
            lastEditBenchmarkMs = watch.Elapsed.TotalMilliseconds;
            Project = original;
            Selected = selected;
            autosave.Stop();
            ResetEditHistory();
        }
        public double lastEditBenchmarkMs;
        public async Task VerifyEditHistory()
        {
            var backup = Json.Copy(Project);
            try
            {
                Load(Json.Copy(backup));
                ShowCurrentDocument();
                LayoutCheck(!CanUndo && !CanRedo && !undoButton.IsEnabled && !redoButton.IsEnabled, "empty history controls");
                var origin = Json.Write(Project.origin);
                var document = Project.documentId;
                var binding = Json.Write(Project.preferredBinding);
                var initialButton = Selected.Button;
                var captionBefore = initialButton.caption;
                ApplyLibraryFields(new[] { initialButton }, new System.Collections.Generic.Dictionary<string,string> { { "caption", "局部刷新检查" } });
                LayoutCheck(initialButton.caption == "局部刷新检查", "definition edit changes the shared command");
                LayoutCheck(PreviewButtons(ribbonPreview).Any(b => b.DataContext is Selection && ((Selection)b.DataContext).Button == initialButton && ((StackPanel)b.Content).Children.OfType<TextBlock>().Any(text => text.Text == "局部刷新检查")), "preview caption refreshed");
                await UndoEdit();
                LayoutCheck(Selected.Button.caption == captionBefore, "property delta undo");
                Load(Json.Copy(backup));
                string initial = EditorData();
                Selected = new Selection { Kind = "tab", Tab = Project.tabs.First() };
                await Add("tab");
                string added = EditorData();
                LayoutCheck(CanUndo && undoButton.IsEnabled && !CanRedo, "undo control after edit");
                await CheckReplay(initial, added, "add Tab");
                var source = new Selection { Kind = "tab", Tab = Project.tabs.Last() };
                var target = new Selection { Kind = "tab", Tab = Project.tabs.First() };
                string before = EditorData();
                LayoutCheck(PlaceSelection(source, target, false), "Tab drag");
                string after = EditorData();
                await CheckReplay(before, after, "Tab drag");
                var tab = Project.tabs.First(t => t.groups.Count > 1);
                source = new Selection { Kind = "group", Tab = tab, Group = tab.groups.Last() };
                target = new Selection { Kind = "group", Tab = tab, Group = tab.groups.First() };
                before = EditorData();
                LayoutCheck(PlaceSelection(source, target, false), "Group drag");
                after = EditorData();
                await CheckReplay(before, after, "Group drag");
                tab = Project.tabs.First(t => t.groups.Any(g => g.items.Count > 1));
                var group = tab.groups.First(g => g.items.Count > 1);
                var item = group.items.Last();
                source = new Selection { Kind = "button", Tab = tab, Group = group, Item = item, Button = Project.buttons.First(b => b.id == item.buttonId) };
                target = new Selection { Kind = "button", Tab = tab, Group = group, Item = group.items.First(), Button = Project.buttons.First(b => b.id == group.items.First().buttonId) };
                before = EditorData();
                LayoutCheck(PlaceSelection(source, target, false), "button drag");
                after = EditorData();
                await CheckReplay(before, after, "button drag");
                source = Selected;
                var destination = source.Tab.groups.First(g => g != source.Group);
                before = EditorData();
                string movedId = source.Button.id;
                LayoutCheck(PlaceSelection(source, new Selection { Kind = "group", Tab = source.Tab, Group = destination }, true), "cross Group drag");
                after = EditorData();
                await CheckReplay(before, after, "cross Group drag");
                LayoutCheck(Selected.Button.id == movedId, "replay restores selection identity");
                before = EditorData();
                Selected = new Selection { Kind = "group", Tab = Selected.Tab, Group = Selected.Tab.groups.Last() };
                await CopyLayout(true);
                LayoutCheck(EditorData() == before, "cut has no history mutation");
                var cutGroup = Selected.Group;
                var cutTab = Selected.Tab;
                Selected = new Selection { Kind = "tab", Tab = cutTab };
                await PasteLayout();
                LayoutCheck(EditorData() == before, "adjacent Group cut no-op");
                var moving = Project.tabs.SelectMany(t => t.groups.Select(g => new { Tab = t, Group = g })).First(p => p.Group.items.Count > 0);
                item = moving.Group.items.First();
                Selected = new Selection { Kind = "button", Tab = moving.Tab, Group = moving.Group, Item = item, Button = Project.buttons.First(b => b.id == item.buttonId) };
                await CopyLayout(true);
                Selected = new Selection { Kind = "group", Tab = moving.Tab, Group = moving.Tab.groups.First(g => g != moving.Group) };
                before = EditorData();
                await PasteLayout();
                after = EditorData();
                LayoutCheck(before != after, "cut paste changes location");
                await CheckReplay(before, after, "cut paste");
                var owningTab = Selected.Tab;
                Selected = new Selection { Kind = "group", Tab = owningTab, Group = owningTab.groups.First() };
                await CopyLayout(true);
                Selected = new Selection { Kind = "tab", Tab = owningTab };
                before = EditorData();
                await PasteLayout();
                after = EditorData();
                LayoutCheck(before != after, "Group cut paste moves order");
                await CheckReplay(before, after, "Group cut paste");
                var placedGroup = Selected.Tab.groups.First(g => g.items.Count > 0);
                var placed = placedGroup.items.First();
                Selected = new Selection { Kind = "button", Tab = Selected.Tab, Group = placedGroup, Item = placed, Button = Project.buttons.First(b => b.id == placed.buttonId) };
                await CopyLayout(false);
                before = EditorData();
                await PasteLayout();
                after = EditorData();
                string clone = Selected.Button.id;
                await CheckReplay(before, after, "copy paste");
                LayoutCheck(Selected.Button.id == clone && Selected.Button.sourceId == null, "redo reuses original clone identity");
                before = EditorData();
                await PasteLayout();
                after = EditorData();
                await UndoEdit();
                LayoutCheck(EditorData() == before && CanRedo, "second paste undo");
                await SetItemSize(Selected.Item.size == "large" ? "small" : "large");
                LayoutCheck(!CanRedo, "new edit truncates redo");
                before = EditorData();
                int steps = undoEdits.Count;
                PlaceSelection(Selected, Selected, false);
                LayoutCheck(undoEdits.Count == steps, "invalid drop adds no history");
                var treeBeforeSave = tree.Items[0];
                await SaveDraft("edit");
                LayoutCheck(Object.ReferenceEquals(treeBeforeSave, tree.Items[0]), "autosave does not rebuild visuals");
                LayoutCheck(undoEdits.Count == steps, "autosave adds no history");
                await SaveProject();
                LayoutCheck(undoEdits.Count == steps && Current(Selected), "manual save keeps history and valid selection");
                after = EditorData();
                await UndoEdit();
                LayoutCheck(CanRedo, "undo after save");
                await RedoEdit();
                LayoutCheck(EditorData() == after, "redo after normalized save preserves keys");
                LayoutCheck(Project.documentId == document && Json.Write(Project.origin) == origin && Json.Write(Project.preferredBinding) == binding, "editor replay preserves document, source XML and bindings");
                LayoutCheck(!HistoryShortcut(Ui.Input("Macro"), Key.Z, ModifierKeys.Control) && !HistoryShortcut(Ui.Combo(), Key.Y, ModifierKeys.Control) && HistoryShortcut(tree, Key.Z, ModifierKeys.Control) && HistoryShortcut(tree, Key.Y, ModifierKeys.Control), "shortcut respects text editing focus");
                Load(Json.Copy(backup));
                Selected = new Selection { Kind = "tab", Tab = Project.tabs.First() };
                await Add("tab");
                await Add("tab");
                after = EditorData();
                await UndoEdit();
                await UndoEdit();
                LayoutCheck(EditorData() == initial, "multi-step undo");
                await RedoEdit();
                await RedoEdit();
                LayoutCheck(EditorData() == after, "multi-step redo");
                // Test the same bounded stack used by normal mutations without changing external files.
                for (int i = 0; i < 60; i++)
                {
                    var next = "UndoLimit" + i;
                    RunEdit("容量检查", () => SetEdit(Project.@namespace, next, v => Project.@namespace = v));
                }
                LayoutCheck(undoEdits.Count == 50, "bounded session history");
                Load(Json.Copy(backup));
                LayoutCheck(!CanUndo && !CanRedo, "loading a document resets history");
                VerifyIncrementalPerformance();
            }
            finally { Load(backup); ShowCurrentDocument(); }
        }
    }
}
