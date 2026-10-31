using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RibbonStudio
{
    public partial class MainWindow
    {
        // The shell owns view navigation; placement owns only definition references.
        public Action CommandPlacementNavigate;

        public List<Selection> CommandReferenceLocations(string buttonId)
        {
            var result = new List<Selection>();
            if (Project == null || String.IsNullOrEmpty(buttonId)) return result;
            var button = Project.buttons.FirstOrDefault(b => b.id == buttonId);
            if (button == null) return result;
            foreach (var tab in Project.tabs)
                foreach (var group in tab.groups)
                    foreach (var item in group.items.Where(i => i.buttonId == buttonId))
                        result.Add(new Selection { Kind = "button", Tab = tab, Group = group, Item = item, Button = button });
            return result;
        }

        // Insert independent layout items referencing the same definitions. No copies of
        // Macro, icon or Tooltip are made, and redo reuses these exact item instances.
        public int PlaceCommandReferences(IEnumerable<string> buttonIds, RibbonTab tab, RibbonGroup group, string size = "large")
        {
            if (Project == null || !IsDocumentView || AnyToolWorking) return 0;
            if (tab == null || group == null || !Project.tabs.Contains(tab) || !tab.groups.Contains(group))
                throw new InvalidOperationException("请先选择当前文件中的菜单页与分组。");
            if (size != "large" && size != "small") throw new ArgumentException("显示大小必须为 large 或 small。", "size");
            var requested = (buttonIds ?? Enumerable.Empty<string>()).Distinct().ToList();
            if (requested.Count == 0) return 0;
            var definitions = requested.Select(id => Project.buttons.FirstOrDefault(b => b.id == id)).ToList();
            if (definitions.Any(b => b == null)) throw new InvalidOperationException("选择中包含已删除或来自其他文件的功能，请重新选择。");
            RunEdit("放入 " + definitions.Count + " 个功能引用", () =>
            {
                foreach (var button in definitions)
                {
                    var item = new RibbonItem { buttonId = button.id, size = size };
                    InsertEdit(group.items, group.items.Count, item);
                    Selected = new Selection { Kind = "button", Tab = tab, Group = group, Item = item, Button = button };
                }
            });
            activePreviewTab = tab;
            if (CommandPlacementNavigate != null) CommandPlacementNavigate();
            RenderAll();
            status.Text = "已将 " + definitions.Count + " 个功能引用放入 " + tab.caption + " / " + group.caption + "；定义保持共享，可一步撤销";
            return definitions.Count;
        }

        public bool LocateCommandReference(Selection location)
        {
            if (Project == null || !IsDocumentView || location == null || location.Item == null || !Current(location)) return false;
            // Resolve the definition from its current stable id rather than trusting a
            // stale caller's Button object following document refresh or undo.
            var button = Project.buttons.FirstOrDefault(b => b.id == location.Item.buttonId);
            if (button == null) return false;
            Selected = new Selection { Kind = "button", Tab = location.Tab, Group = location.Group, Item = location.Item, Button = button };
            activePreviewTab = location.Tab;
            if (CommandPlacementNavigate != null) CommandPlacementNavigate();
            RenderAll();
            status.Text = "已定位：" + location.Tab.caption + " / " + location.Group.caption + " / " + button.caption;
            return true;
        }

        public bool RemoveCommandReference(Selection location)
        {
            if (Project == null || !IsDocumentView || AnyToolWorking || location == null || location.Item == null || !Current(location)) return false;
            Selected = location;
            RunEdit("移除引用 " + location.Button.caption, () =>
            {
                RemoveEdit(location.Group.items, location.Item);
                Selected = new Selection { Kind = "button", Button = location.Button };
            });
            RenderAll();
            status.Text = "已移除当前位置的引用；功能定义与其他引用保留，可撤销";
            return true;
        }

        public Task ChooseCommandPlacement(IEnumerable<string> buttonIds)
        {
            if (Project == null || !IsDocumentView) return Task.FromResult(0);
            var ids = (buttonIds ?? Enumerable.Empty<string>()).Distinct().ToList();
            if (ids.Count == 0) { status.Text = "请先在功能库选择功能"; return Task.FromResult(0); }
            var destinations = Project.tabs.SelectMany(t => t.groups.Select(g => Tuple.Create(t, g))).ToList();
            if (destinations.Count == 0)
            {
                if (CommandPlacementNavigate != null) CommandPlacementNavigate();
                status.Text = "请先在 Ribbon 编排中添加菜单页和分组，再放入所选功能";
                return Task.FromResult(0);
            }
            var window = Ui.Dialog("将功能放入 Ribbon", this, 650, 420);
            window.MinHeight = 350; window.SizeToContent = SizeToContent.Height;
            var body = new StackPanel { Margin = new Thickness(24) };
            body.Children.Add(Ui.Text("放入 " + ids.Count + " 个功能引用；名称、Macro、图标和 Tooltip 继续共享。", 13));
            body.Children.Add(Ui.Text("目标菜单页 / 分组", 12, Ui.Muted));
            var choices = Ui.Combo();
            foreach (var destination in destinations) choices.Items.Add(destination.Item1.caption + " / " + destination.Item2.caption);
            int preferred = destinations.FindIndex(d => d.Item2 == Selected.Group);
            choices.SelectedIndex = preferred < 0 ? 0 : preferred;
            body.Children.Add(choices);
            body.Children.Add(Ui.Text("显示大小（仅影响本次放置）", 12, Ui.Muted));
            var sizes = Ui.Combo(); sizes.Items.Add("大按钮 · 32 × 32"); sizes.Items.Add("小按钮 · 16 × 16"); sizes.SelectedIndex = 0;
            body.Children.Add(sizes);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = Ui.Sync("取消", () => window.DialogResult = false); cancel.IsCancel = true; actions.Children.Add(cancel);
            var place = Ui.Button("放入分组", () =>
            {
                if (choices.SelectedIndex < 0) return Task.FromResult(0);
                var destination = destinations[choices.SelectedIndex];
                PlaceCommandReferences(ids, destination.Item1, destination.Item2, sizes.SelectedIndex == 1 ? "small" : "large");
                window.DialogResult = true;
                return Task.FromResult(0);
            }, true);
            place.IsDefault = true; actions.Children.Add(place); body.Children.Add(actions);
            window.Content = body; Chrome.Install(window); window.ShowDialog();
            return Task.FromResult(0);
        }

        public Task ShowCommandLocations(string buttonId)
        {
            var locations = CommandReferenceLocations(buttonId);
            if (locations.Count == 0) { status.Text = "该功能尚未放入 Ribbon；可从功能库选择后放入分组"; return Task.FromResult(0); }
            if (locations.Count == 1) { LocateCommandReference(locations[0]); return Task.FromResult(0); }
            var window = Ui.Dialog("功能使用位置", this, 700, 480);
            var root = new DockPanel { Margin = new Thickness(24) };
            var heading = Ui.Text("同一功能共 " + locations.Count + " 处引用，选择要定位的位置。", 14);
            DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
            var list = new ListBox { Margin = new Thickness(0, 12, 0, 12), BorderBrush = Ui.Line, BorderThickness = new Thickness(1) };
            foreach (var location in locations)
                list.Items.Add(new ListBoxItem { Tag = location, Content = location.Tab.caption + " / " + location.Group.caption + " / 第 " + (location.Group.items.IndexOf(location.Item) + 1) + " 项 · " + (location.Item.size == "small" ? "小按钮" : "大按钮"), Padding = new Thickness(10) });
            list.SelectedIndex = 0; root.Children.Add(list);
            var cancel = Ui.Sync("取消", () => window.DialogResult = false); cancel.IsCancel = true; actions.Children.Add(cancel);
            var locate = Ui.Button("定位引用", () =>
            {
                var item = list.SelectedItem as ListBoxItem;
                if (item != null && LocateCommandReference(item.Tag as Selection)) window.DialogResult = true;
                return Task.FromResult(0);
            }, true);
            locate.IsDefault = true; actions.Children.Add(locate);
            window.Content = root; Chrome.Install(window); window.ShowDialog();
            return Task.FromResult(0);
        }

        void RenderCommandPlacementInspector()
        {
            var scope = Selected;
            if (Project == null || scope == null || scope.Kind != "button" || scope.Button == null) return;
            var button = scope.Button;
            bool placed = scope.Item != null && Current(scope);
            inspector.Children.Add(Ui.Text("放置属性", 17));
            inspector.Children.Add(Ui.Text(button.caption, 14));
            inspector.Children.Add(Ui.Text("这里只修改当前引用的位置、顺序与大小。功能定义由所有使用位置共享。", 12, Ui.Muted));
            inspector.Children.Add(Ui.Text("使用位置：" + CommandReferenceLocations(button.id).Count + " 处", 12, Ui.Muted));
            if (placed)
            {
                inspector.Children.Add(Ui.Text("当前位置", 12, Ui.Muted));
                inspector.Children.Add(Ui.Text(scope.Tab.caption + " / " + scope.Group.caption, 13));
                var destinations = Project.tabs.SelectMany(t => t.groups.Select(g => Tuple.Create(t, g))).ToList();
                inspector.Children.Add(Ui.Text("移动当前引用到", 12, Ui.Muted));
                var groups = Ui.Combo(); groups.MinWidth = 0; groups.HorizontalAlignment = HorizontalAlignment.Stretch;
                foreach (var destination in destinations) groups.Items.Add(destination.Item1.caption + " / " + destination.Item2.caption);
                groups.SelectedIndex = destinations.FindIndex(d => d.Item2 == scope.Group);
                groups.SelectionChanged += async (s, e) =>
                {
                    if (loading || groups.SelectedIndex < 0 || !Current(scope)) return;
                    var destination = destinations[groups.SelectedIndex];
                    if (destination.Item2 == scope.Group) return;
                    await Guard(() => { Selected = scope; return MoveButtonTo(destination.Item1, destination.Item2); });
                };
                inspector.Children.Add(groups);
                inspector.Children.Add(Ui.Text("显示大小（仅当前引用）", 12, Ui.Muted));
                var sizes = Ui.Combo(); sizes.MinWidth = 0; sizes.HorizontalAlignment = HorizontalAlignment.Stretch;
                sizes.Items.Add("大按钮 · 32 × 32"); sizes.Items.Add("小按钮 · 16 × 16"); sizes.SelectedIndex = scope.Item.size == "small" ? 1 : 0;
                sizes.SelectionChanged += async (s, e) =>
                {
                    if (loading || sizes.SelectedIndex < 0 || !Current(scope)) return;
                    await Guard(() => { Selected = scope; return SetItemSize(sizes.SelectedIndex == 1 ? "small" : "large"); });
                };
                inspector.Children.Add(sizes);
                int position = scope.Group.items.IndexOf(scope.Item);
                inspector.Children.Add(Ui.Text("顺序：第 " + (position + 1) + " 项 / 共 " + scope.Group.items.Count + " 项", 12, Ui.Muted));
                var order = new WrapPanel();
                var up = Ui.Button("上移", () => Guard(() => { if (Current(scope)) { Selected = scope; return Move(-1); } return Task.FromResult(0); }));
                var down = Ui.Button("下移", () => Guard(() => { if (Current(scope)) { Selected = scope; return Move(1); } return Task.FromResult(0); }));
                up.IsEnabled = position > 0; down.IsEnabled = position >= 0 && position < scope.Group.items.Count - 1;
                order.Children.Add(up); order.Children.Add(down); inspector.Children.Add(order);
                inspector.Children.Add(Ui.Button("移除当前引用", () => Guard(() => { RemoveCommandReference(scope); return Task.FromResult(0); })));
                inspector.Children.Add(Ui.Text("移除后功能仍留在功能库，其他位置不受影响。", 12, Ui.Muted));
            }
            else
                inspector.Children.Add(Ui.Text("当前选中功能尚未关联一个具体放置引用。", 12, Ui.Muted));
            inspector.Children.Add(Ui.Button(placed ? "另放入一个分组…" : "放入 Ribbon…", () => Guard(() => ChooseCommandPlacement(new[] { button.id }))));
            inspector.Children.Add(Ui.Button("查看所有使用位置…", () => Guard(() => ShowCommandLocations(button.id))));
            inspector.Children.Add(Ui.Button("编辑功能定义", () =>
            {
                Selected = new Selection { Kind = "button", Button = button };
                EditCommandDefinition(button, null);
                return Task.FromResult(0);
            }));
        }
    }
}
