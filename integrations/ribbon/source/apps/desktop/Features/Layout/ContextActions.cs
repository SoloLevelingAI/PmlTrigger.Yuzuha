using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Threading;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        Func<Task> InContext(Selection selection, Func<Task> action)
        {
            return () => Guard(async () => { Selected = selection; await action(); });
        }
        public ContextMenu SelectionMenu(Selection selection)
        {
            var menu = new ContextMenu();
            bool button = selection.Kind == "button", group = selection.Kind == "group", tab = selection.Kind == "tab", placed = selection.Item != null;
            if (selection.Kind != "project")
            {
                menu.Items.Add(Ui.Menu("剪切", InContext(selection, () => CopyLayout(true)), true, "Ctrl+X"));
                menu.Items.Add(Ui.Menu("复制", InContext(selection, () => CopyLayout(false)), true, "Ctrl+C"));
            }
            var paste = Ui.Menu("粘贴", InContext(selection, PasteLayout), CanPaste(selection), "Ctrl+V");
            menu.Items.Add(paste);
            menu.Items.Add(new Separator());
            if (!button)
            {
                var add = new MenuItem { Header = "添加" };
                if (selection.Kind == "project")
                    add.Items.Add(Ui.Menu("菜单页", InContext(selection, () => Add("tab"))));
                if (tab)
                    add.Items.Add(Ui.Menu("Group", InContext(selection, () => Add("group"))));
                if (group)
                    add.Items.Add(Ui.Menu("按钮", InContext(selection, () => Add("button"))));
                menu.Items.Add(add);
            }
            if (selection.Kind != "project")
            {
                var edit = new MenuItem { Header = "编辑" };
                edit.Items.Add(Ui.Menu("重命名…", InContext(selection, RenameSelection), true, "F2"));
                if (button)
                {
                    edit.Items.Add(Ui.Menu("编辑功能定义 / Macro…", () => { EditCommandDefinition(selection.Button, "macro"); return Task.FromResult(0); }));
                    edit.Items.Add(new Separator());
                    edit.Items.Add(Ui.Menu("管理功能图标…", () => { EditCommandDefinition(selection.Button, null); return Task.FromResult(0); }));
                }
                edit.Items.Add(new Separator());
                AddHistoryMenu(edit);
                edit.Items.Add(new Separator());
                edit.Items.Add(Ui.Menu("复制唯一标识", () => { Clipboard.SetText(button ? selection.Button.id : group ? selection.Group.id : selection.Tab.id); return Task.FromResult(0); }));
                if (button)
                    edit.Items.Add(Ui.Menu("复制 Macro", () => { Clipboard.SetText(selection.Button.macro ?? ""); return Task.FromResult(0); }));
                menu.Items.Add(edit);
                var layout = new MenuItem { Header = "布局" };
                if (button)
                {
                    foreach (var size in new[] { "large", "small" })
                    {
                        string chosen = size;
                        var sizeItem = Ui.Menu(size == "large" ? "Large · 32 × 32" : "Small · 16 × 16", InContext(selection, () => SetItemSize(chosen)), placed);
                        sizeItem.IsCheckable = true;
                        sizeItem.IsChecked = placed && selection.Item.size == size;
                        layout.Items.Add(sizeItem);
                    }
                    var move = new MenuItem { Header = "移动到 Group" };
                    foreach (var t in Project.tabs)
                        foreach (var g in t.groups)
                        {
                            var dt = t;
                            var dg = g;
                            move.Items.Add(Ui.Menu(t.caption + " / " + g.caption, InContext(selection, () => MoveButtonTo(dt, dg)), g != selection.Group));
                        }
                    layout.Items.Add(move);
                    layout.Items.Add(new Separator());
                }
                int index = placed ? selection.Group.items.IndexOf(selection.Item) : group ? selection.Tab.groups.IndexOf(selection.Group) : tab ? Project.tabs.IndexOf(selection.Tab) : -1;
                int count = placed ? selection.Group.items.Count : group ? selection.Tab.groups.Count : tab ? Project.tabs.Count : 0;
                layout.Items.Add(Ui.Menu("上移", InContext(selection, () => Move(-1)), index > 0));
                layout.Items.Add(Ui.Menu("下移", InContext(selection, () => Move(1)), index >= 0 && index < count - 1));
                menu.Items.Add(layout);
                menu.Items.Add(new Separator());
                menu.Items.Add(Ui.Menu(button ? "移除引用…" : "移除所选布局…", InContext(selection, Delete), !button || placed, "Delete"));
            }
            else
            {
                var editHistory = new MenuItem { Header = "撤销 / 重做" };
                AddHistoryMenu(editHistory);
                menu.Items.Add(editHistory);
                menu.Items.Add(Ui.Menu("检查项目", () => Guard(Validate)));
                menu.Items.Add(Ui.Menu("保存快照", () => Guard(SaveProject), true, "Ctrl+S"));
            }
            menu.Opened += (s, e) => { paste.IsEnabled = CanPaste(selection); ChooseContext(selection); };
            return menu;
        }
        void AddHistoryMenu(MenuItem parent)
        {
            parent.Items.Add(Ui.Menu("撤销", () => Guard(UndoEdit), CanUndo && HistoryAllowed, "Ctrl+Z"));
            parent.Items.Add(Ui.Menu("重做", () => Guard(RedoEdit), CanRedo && HistoryAllowed, "Ctrl+Y"));
        }
        void ChooseContext(Selection selection)
        {
            Selected = selection;
            bool before = loading;
            loading = true;
            foreach (var node in TreeNodes(tree.Items))
            {
                var scope = node.Tag as Selection;
                if (scope != null && scope.Kind == selection.Kind && scope.Button == selection.Button && scope.Item == selection.Item && scope.Group == selection.Group && scope.Tab == selection.Tab)
                {
                    node.IsSelected = true;
                    break;
                }
            }
            loading = before;
            RenderInspector();
            foreach (var button in PreviewButtons(ribbonPreview))
            {
                var scope = button.DataContext as Selection;
                if (scope != null)
                    button.Tag = scope.Kind == "tab" ? (scope.Tab == activePreviewTab ? "Selected" : null) : (scope.Item == selection.Item && scope.Button == selection.Button ? "Selected" : null);
            }
        }
        static IEnumerable<TreeViewItem> TreeNodes(ItemCollection items)
        {
            foreach (var item in items)
            {
                var node = item as TreeViewItem;
                if (node == null)
                    continue;
                yield return node;
                foreach (var child in TreeNodes(node.Items))
                    yield return child;
            }
        }
        static IEnumerable<Button> PreviewButtons(DependencyObject root)
        {
            var button = root as Button;
            if (button != null)
                yield return button;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in PreviewButtons(VisualTreeHelper.GetChild(root, i)))
                    yield return child;
        }
        IEnumerable<TextBox> InspectorFields()
        {
            foreach (var child in inspector.Children)
            {
                var field = child as TextBox;
                if (field != null)
                    yield return field;
                var expander = child as Expander;
                if (expander != null && expander.Content is StackPanel)
                foreach (var box in ((StackPanel)expander.Content).Children.OfType<TextBox>())
                    yield return box;
            }
        }
        void FocusField(string name)
        {
            var box = InspectorFields().FirstOrDefault(b => Convert.ToString(b.Tag) == name);
            if (box != null)
            {
                box.Focus();
                box.SelectAll();
            }
        }
        async Task RenameSelection()
        {
            if (Selected.Button != null) { EditCommandDefinition(Selected.Button, "caption"); return; }
            string value = Caption(Selected);
            var text = Ui.Prompt("重命名", "显示名称", value, this);
            if (text == null)
                return;
            var selection = Selected;
            RunEdit("重命名", () => SetEdit(value, text, v => { if (selection.Button != null) selection.Button.caption = v; else if (selection.Kind == "group") selection.Group.caption = v; else selection.Tab.caption = v; }));
            RenderAll();
            await Task.FromResult(0);
        }
        public Task SetItemSize(string size)
        {
            var item = Selected.Item;
            if (item == null)
                return Task.FromResult(0);
            RunEdit("图标大小", () => SetEdit(item.size, size, value => item.size = value));
            RenderAll();
            return Task.FromResult(0);
        }
        Task MoveButtonTo(RibbonTab tab, RibbonGroup group)
        {
            if (Selected.Button != null && Selected.Group != group)
                PlaceSelection(Selected, new Selection { Kind = "group", Tab = tab, Group = group }, true);
            return Task.FromResult(0);
        }
        RibbonButton CopyButton(RibbonButton original)
        {
            var copy = Json.Copy(original);
            copy.id = Id("Button");
            copy.sourceId = null;
            copy.commandKey = "";
            copy.caption += " 副本";
            InsertEdit(Project.buttons, Project.buttons.Count, copy);
            return copy;
        }
        RibbonGroup CopyGroup(RibbonGroup original, Dictionary<string, string> buttons)
        {
            var copy = Json.Copy(original);
            copy.id = Id("Group");
            copy.sourceId = null;
            foreach (var item in copy.items)
            {
                string id;
                if (!buttons.TryGetValue(item.buttonId, out id))
                {
                    var old = Project.buttons.FirstOrDefault(b => b.id == item.buttonId);
                    if (old == null)
                        throw new Exception("分组包含失效引用，请先检查项目");
                    id = CopyButton(old).id;
                    buttons.Add(item.buttonId, id);
                }
                item.buttonId = id;
            }
            return copy;
        }
        public async Task DuplicateSelection()
        {
            await CopyLayout(false);
            await PasteLayout();
        }

    }

}
