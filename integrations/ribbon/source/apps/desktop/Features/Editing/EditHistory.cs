using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        sealed class HistoryCursor
        {
            public string Kind, TabId, GroupId, ButtonId; public int TabIndex, GroupIndex, ItemIndex;
        }
        sealed class EditOperation
        {
            public Action Undo, Redo;
        }
        sealed class HistoryEntry
        {
            public string Action; public HistoryCursor Before, After; public List<EditOperation> Operations = new List<EditOperation>();
        }
        List<HistoryEntry> undoEdits = new List<HistoryEntry>(), redoEdits = new List<HistoryEntry>();
        HistoryEntry pendingEdit; Button undoButton, redoButton;
        public bool CanUndo
        {
            get
            {
                return undoEdits.Count > 0;
            }
        }
        public bool CanRedo
        {
            get
            {
                return redoEdits.Count > 0;
            }
        }
        bool HistoryAllowed
        {
            get
            {
                return Project != null && !draggingLayout && IsDocumentView && !AnyToolWorking;
            }
        }
        static bool HistoryShortcut(DependencyObject source, Key key, ModifierKeys modifiers)
        {
            return modifiers == ModifierKeys.Control && (key == Key.Z || key == Key.Y) && Ui.Ancestor<TextBox>(source) == null && Ui.Ancestor<ComboBox>(source) == null;
        }
        HistoryCursor CursorFor(Selection s)
        {
            return new HistoryCursor { Kind = s.Kind, TabId = s.Tab == null ? null : s.Tab.id, GroupId = s.Group == null ? null : s.Group.id, ButtonId = s.Button == null ? null : s.Button.id, TabIndex = s.Tab == null ? -1 : Project.tabs.IndexOf(s.Tab), GroupIndex = s.Group == null || s.Tab == null ? -1 : s.Tab.groups.IndexOf(s.Group), ItemIndex = s.Item == null || s.Group == null ? -1 : s.Group.items.IndexOf(s.Item) };
        }
        Selection ResolveCursor(HistoryCursor cursor)
        {
            if (cursor == null)
                return new Selection { Kind = "project" };
            var tab = Project.tabs.FirstOrDefault(t => t.id == cursor.TabId);
            if (tab == null && cursor.TabIndex >= 0 && cursor.TabIndex < Project.tabs.Count)
                tab = Project.tabs[cursor.TabIndex];
            var group = tab == null ? null : tab.groups.FirstOrDefault(g => g.id == cursor.GroupId);
            if (group == null && tab != null && cursor.GroupIndex >= 0 && cursor.GroupIndex < tab.groups.Count)
                group = tab.groups[cursor.GroupIndex];
            var item = group != null && cursor.ItemIndex >= 0 && cursor.ItemIndex < group.items.Count ? group.items[cursor.ItemIndex] : null;
            var button = Project.buttons.FirstOrDefault(b => b.id == cursor.ButtonId);
            if (item != null)
                button = Project.buttons.FirstOrDefault(b => b.id == item.buttonId);
            if (cursor.Kind == "button" && button != null)
                return new Selection { Kind = "button", Tab = item == null ? null : tab, Group = item == null ? null : group, Item = item, Button = button };
            if (cursor.Kind == "group" && group != null)
                return new Selection { Kind = "group", Tab = tab, Group = group };
            if (cursor.Kind == "tab" && tab != null)
                return new Selection { Kind = "tab", Tab = tab };
            return new Selection { Kind = "project" };
        }

        void ResetEditHistory()
        {
            undoEdits.Clear();
            redoEdits.Clear();
            pendingEdit = null;
            UpdateHistoryButtons();
        }

        void RunEdit(string label, Action action)
        {
            if (!IsDocumentView)
                return;
            if (pendingEdit != null)
            {
                action();
                return;
            }
            var entry = new HistoryEntry { Action = label, Before = CursorFor(Selected) };
            pendingEdit = entry;
            try
            {
                action();
                entry.After = CursorFor(Selected);
            }
            catch { for (int i = entry.Operations.Count - 1; i >= 0; i--) entry.Operations[i].Undo(); Selected = ResolveCursor(entry.Before); throw; }
            finally { pendingEdit = null; }
            if (entry.Operations.Count == 0)
                return;
            undoEdits.Add(entry);
            redoEdits.Clear();
            while (undoEdits.Count > 50)
                undoEdits.RemoveAt(0);
            Changed(label, false);
            UpdateHistoryButtons();
        }
        void ApplyEdit(Action redo, Action undo)
        {
            if (pendingEdit == null)
                throw new InvalidOperationException("Mutation requires a feature edit transaction");
            redo();
            pendingEdit.Operations.Add(new EditOperation { Undo = undo, Redo = redo });
        }
        void SetEdit<T>(T oldValue, T value, Action<T> setter)
        {
            if (EqualityComparer<T>.Default.Equals(oldValue, value))
                return;
            ApplyEdit(() => setter(value), () => setter(oldValue));
        }
        void InsertEdit<T>(List<T> list, int index, T item)
        {
            ApplyEdit(() => list.Insert(index, item), () => list.Remove(item));
        }
        void RemoveEdit<T>(List<T> list, T item)
        {
            int index = list.IndexOf(item);
            if (index < 0)
                return;
            ApplyEdit(() => list.Remove(item), () => list.Insert(index, item));
        }
        bool PositionEdit<T>(List<T> list, T item, int position)
        {
            int previous = list.IndexOf(item);
            if (previous >= 0 && previous < position)
                position--;
            position = Math.Max(0, Math.Min(position, list.Count - (previous < 0 ? 0 : 1)));
            if (previous == position)
                return false;
            int next = position;
            ApplyEdit(() => { list.Remove(item); list.Insert(next, item); }, () => { list.Remove(item); if (previous >= 0) list.Insert(previous, item); });
            return true;
        }
        void UpdateHistoryButtons()
        {
            if (undoButton != null)
            {
                undoButton.IsEnabled = HistoryAllowed && CanUndo;
                undoButton.ToolTip = CanUndo ? "撤销：" + undoEdits.Last().Action + " (Ctrl+Z)" : "没有可撤销的编辑 (Ctrl+Z)";
            }
            if (redoButton != null)
            {
                redoButton.IsEnabled = HistoryAllowed && CanRedo;
                redoButton.ToolTip = CanRedo ? "重做：" + redoEdits.Last().Action + " (Ctrl+Y)" : "没有可重做的编辑 (Ctrl+Y)";
            }
        }
        public Task UndoEdit()
        {
            return ReplayEdit(false);
        }
        public Task RedoEdit()
        {
            return ReplayEdit(true);
        }
        Task ReplayEdit(bool redo)
        {
            var source = redo ? redoEdits : undoEdits;
            var destination = redo ? undoEdits : redoEdits;
            if (!HistoryAllowed || source.Count == 0)
                return Task.FromResult(0);
            autosave.Stop();
            var entry = source.Last();
            if (redo)
            {
                foreach (var operation in entry.Operations)
                    operation.Redo();
            }
            else
                for (int i = entry.Operations.Count - 1; i >= 0; i--)
                    entry.Operations[i].Undo();
            source.RemoveAt(source.Count - 1);
            destination.Add(entry);
            if (clipboardCut)
            {
                clipboardSelection = null;
                clipboardProject = null;
                clipboardCut = false;
            }
            Selected = ResolveCursor(redo ? entry.After : entry.Before);
            activePreviewTab = Selected.Tab ?? activePreviewTab;
            Changed("编辑", false);
            RenderAll();
            UpdateHistoryButtons();
            status.Text = (redo ? "已重做：" : "已撤销：") + entry.Action + " · 增量记录";
            return Task.FromResult(0);
        }
    }
}
