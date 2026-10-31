using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        const string DragFormat = "RibbonStudio.Layout.Selection";
        sealed class LayoutDrag
        {
            public MainWindow Owner; public int Epoch; public Selection Source;
        }
        Selection clipboardSelection; Project clipboardProject; bool clipboardCut; int clipboardEpoch;
        RibbonTab activePreviewTab; DateTime suppressPreviewClick; bool draggingLayout;
        static readonly DependencyProperty DragSurface = DependencyProperty.RegisterAttached("DragSurface", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
        static FrameworkElement NearestDragSurface(DependencyObject source)
        {
            while (source != null)
            {
                var element = source as FrameworkElement;
                if (element != null && (bool)element.GetValue(DragSurface))
                    return element;
                source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
            }
            return null;
        }
        bool Current(Selection s)
        {
            return s != null && (s.Kind == "project" || s.Kind == "tab" && Project.tabs.Contains(s.Tab) || s.Kind == "group" && Project.tabs.Contains(s.Tab) && s.Tab.groups.Contains(s.Group) || s.Kind == "button" && Project.buttons.Contains(s.Button) && (s.Item == null || s.Group != null && Project.tabs.Contains(s.Tab) && s.Tab.groups.Contains(s.Group) && s.Group.items.Contains(s.Item)));
        }
        // Groups stay in their owning Tab; button references may move across any Group.
        bool CanPlace(Selection source, Selection target)
        {
            if (!Current(target) || source == null)
                return false;
            if (source.Kind == "tab")
                return target.Kind == "project" || target.Kind == "tab" && target.Tab != source.Tab;
            if (source.Kind == "group")
                return (target.Kind == "tab" && target.Tab == source.Tab) || (target.Kind == "group" && target.Group != source.Group && target.Tab == source.Tab);
            return source.Kind == "button" && (target.Kind == "group" || target.Kind == "button" && target.Item != null && target.Item != source.Item);
        }
        public bool PlaceSelection(Selection source, Selection target, bool after)
        {
            if (!Current(source) || !CanPlace(source, target))
                return false;
            Selected = source;
            bool changed = false;
            RunEdit("移动 " + Caption(source), () => { if (source.Kind == "tab") changed = PositionEdit(Project.tabs, source.Tab, target.Kind == "project" ? Project.tabs.Count : Project.tabs.IndexOf(target.Tab) + (after ? 1 : 0)); else if (source.Kind == "group") changed = PositionEdit(source.Tab.groups, source.Group, target.Kind == "tab" ? source.Tab.groups.Count : source.Tab.groups.IndexOf(target.Group) + (after ? 1 : 0)); else { var group = target.Group; int index = target.Kind == "group" ? group.items.Count : group.items.IndexOf(target.Item) + (after ? 1 : 0); var item = source.Item ?? new RibbonItem { buttonId = source.Button.id, size = "large" }; if (source.Group == group) changed = PositionEdit(group.items, item, index); else { if (source.Group != null) RemoveEdit(source.Group.items, item); InsertEdit(group.items, index, item); changed = true; } source.Tab = target.Tab; source.Group = group; source.Item = item; Selected = source; } });
            if (changed)
            {
                activePreviewTab = Selected.Tab;
                RenderAll();
            }
            return changed;
        }
        public Task CopyLayout(bool cut)
        {
            if (Project == null || Selected.Kind == "project")
                return Task.FromResult(0);
            clipboardCut = cut;
            clipboardEpoch = epoch;
            if (cut)
            {
                clipboardSelection = Selected;
                clipboardProject = Project;
            }
            else
            {
                var source = Selected;
                var ids = new HashSet<string>();
                if (source.Kind == "button")
                    ids.Add(source.Button.id);
                else if (source.Kind == "group")
                    foreach (var item in source.Group.items)
                        ids.Add(item.buttonId);
                else
                    foreach (var item in source.Tab.groups.SelectMany(g => g.items))
                        ids.Add(item.buttonId);
                clipboardProject = new Project { buttons = Project.buttons.Where(button => ids.Contains(button.id)).Select(Json.Copy).ToList() };
                clipboardSelection = new Selection { Kind = source.Kind, Tab = source.Kind == "tab" ? Json.Copy(source.Tab) : null, Group = source.Kind == "group" ? Json.Copy(source.Group) : null, Item = source.Item == null ? null : Json.Copy(source.Item), Button = source.Button == null ? null : clipboardProject.buttons.First(button => button.id == source.Button.id) };
            }
            status.Text = (cut ? "已剪切选择，粘贴后移动：" : "已复制：") + Caption(Selected) + " · 选择目标后 Ctrl+V";
            return Task.FromResult(0);
        }
        bool CanPaste(Selection target)
        {
            if (clipboardSelection == null || Project == null || !Current(target))
                return false;
            var s = clipboardSelection;
            if (clipboardCut)
                return clipboardEpoch == epoch && Current(s) && CanPlace(s, target);
            return s.Kind == "tab" ? (target.Kind == "tab" || target.Kind == "project") : s.Kind == "group" ? (target.Kind == "tab" || target.Kind == "group") : (target.Kind == "group" || target.Kind == "button" && target.Item != null);
        }
        RibbonGroup CloneClipboardGroup(RibbonGroup original, Dictionary<string, string> map)
        {
            var result = Json.Copy(original);
            result.id = Id("Group");
            result.sourceId = null;
            foreach (var item in result.items)
            {
                string id;
                if (!map.TryGetValue(item.buttonId, out id))
                {
                    id = CopyButton(clipboardProject.buttons.First(b => b.id == item.buttonId)).id;
                    map.Add(item.buttonId, id);
                }
                item.buttonId = id;
            }
            return result;
        }
        public Task PasteLayout()
        {
            if (!CanPaste(Selected))
            {
                status.Text = "请选择兼容目标：Tab → 项目/Tab，Group → Tab/Group，按钮 → Group/按钮";
                return Task.FromResult(0);
            }
            var target = Selected;
            var s = clipboardSelection;
            if (clipboardCut)
            {
                PlaceSelection(s, target, true);
                clipboardSelection = null;
                clipboardProject = null;
                return Task.FromResult(0);
            }
            RunEdit("粘贴 " + Caption(s), () =>
            {
                if (s.Kind == "button")
                {
                    var button = CopyButton(s.Button);
                    var item = new RibbonItem { buttonId = button.id, size = s.Item == null ? "large" : s.Item.size };
                    int index = target.Kind == "group" ? target.Group.items.Count : target.Group.items.IndexOf(target.Item) + 1;
                    InsertEdit(target.Group.items, index, item);
                    Selected = new Selection { Kind = "button", Tab = target.Tab, Group = target.Group, Item = item, Button = button };
                }
                else if (s.Kind == "group")
                {
                    var group = CloneClipboardGroup(s.Group, new Dictionary<string, string>());
                    group.caption += " 副本";
                    InsertEdit(target.Tab.groups, target.Kind == "tab" ? target.Tab.groups.Count : target.Tab.groups.IndexOf(target.Group) + 1, group);
                    Selected = new Selection { Kind = "group", Tab = target.Tab, Group = group };
                }
                else
                {
                    var map = new Dictionary<string, string>();
                    var tab = new RibbonTab { id = Id("Tab"), caption = s.Tab.caption + " 副本", groups = s.Tab.groups.Select(g => CloneClipboardGroup(g, map)).ToList() };
                    InsertEdit(Project.tabs, target.Kind == "project" ? Project.tabs.Count : Project.tabs.IndexOf(target.Tab) + 1, tab);
                    Selected = new Selection { Kind = "tab", Tab = tab };
                }
            });
            activePreviewTab = Selected.Tab;
            RenderAll();
            return Task.FromResult(0);
        }
        static string Caption(Selection s)
        {
            return s.Button != null ? s.Button.caption : s.Group != null ? s.Group.caption : s.Tab != null ? s.Tab.caption : "项目";
        }
        void WireDrag(FrameworkElement element, Selection scope, bool horizontal, bool treeNode = false)
        {
            Point start = new Point();
            bool pressed = false;
            Brush oldBackground = null;
            Thickness oldBorder = new Thickness();
            bool highlighted = false;
            element.AllowDrop = true;
            element.SetValue(DragSurface, true);
            element.PreviewMouseLeftButtonDown += (s, e) => { if (NearestDragSurface(e.OriginalSource as DependencyObject) != element) return; if (Ui.Ancestor<System.Windows.Controls.Primitives.ToggleButton>(e.OriginalSource as DependencyObject) != null) return; start = e.GetPosition(element); pressed = true; };
            element.PreviewMouseLeftButtonUp += (s, e) => pressed = false;
            element.PreviewMouseMove += (s, e) => { if (!pressed || e.LeftButton != MouseButtonState.Pressed || scope.Kind == "project") return; if (NearestDragSurface(e.OriginalSource as DependencyObject) != element) return; var at = e.GetPosition(element); if (Math.Abs(at.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(at.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return; pressed = false; e.Handled = true; Mouse.Capture(null); draggingLayout = true; try { DragDrop.DoDragDrop(element, new DataObject(DragFormat, new LayoutDrag { Owner = this, Epoch = epoch, Source = scope }), DragDropEffects.Move); } finally { draggingLayout = false; suppressPreviewClick = DateTime.UtcNow.AddMilliseconds(250); clearAfterDrag(element); } };
            Action clear = () => { if (!highlighted) return; var control = element as Control; if (control != null) { control.Background = oldBackground; control.BorderThickness = oldBorder; } var border = element as Border; if (border != null) { border.Background = oldBackground; border.BorderThickness = oldBorder; } highlighted = false; };
            DragEventHandler over = (s, e) => { if (treeNode && Ui.Ancestor<TreeViewItem>(e.OriginalSource as DependencyObject) != element) return; var drag = e.Data.GetData(DragFormat) as LayoutDrag; bool valid = drag != null && drag.Owner == this && drag.Epoch == epoch && Current(drag.Source) && CanPlace(drag.Source, scope); e.Effects = valid ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; clear(); if (valid) { var at = e.GetPosition(element); bool after = horizontal ? at.X > element.ActualWidth / 2 : at.Y > HeaderHeight(element, treeNode) / 2; bool inside = scope.Kind != drag.Source.Kind; var line = inside ? new Thickness(2) : horizontal ? new Thickness(after ? 0 : 3, 0, after ? 3 : 0, 0) : new Thickness(0, after ? 0 : 3, 0, after ? 3 : 0); var control = element as Control; var border = element as Border; if (control != null) { oldBackground = control.Background; oldBorder = control.BorderThickness; control.Background = new SolidColorBrush(Color.FromRgb(205, 222, 250)); control.BorderThickness = line; } else if (border != null) { oldBackground = border.Background; oldBorder = border.BorderThickness; border.Background = new SolidColorBrush(Color.FromRgb(205, 222, 250)); border.BorderThickness = line; } highlighted = true; status.Text = "移动“" + Caption(drag.Source) + "” → “" + Caption(scope) + "”" + (inside ? "内部末尾" : after ? "之后" : "之前"); } };
            element.DragOver += over;
            element.DragEnter += over;
            element.DragLeave += (s, e) => clear();
            element.Drop += (s, e) => { if (treeNode && Ui.Ancestor<TreeViewItem>(e.OriginalSource as DependencyObject) != element) return; clear(); var drag = e.Data.GetData(DragFormat) as LayoutDrag; e.Handled = true; if (drag == null || drag.Owner != this || drag.Epoch != epoch) return; var at = e.GetPosition(element); PlaceSelection(drag.Source, scope, horizontal ? at.X > element.ActualWidth / 2 : at.Y > HeaderHeight(element, treeNode) / 2); };
        }
        void clearAfterDrag(FrameworkElement element)
        {
            Dispatcher.BeginInvoke(new Action(() => { if (Project != null) RenderAll(); }));
        }
        static double HeaderHeight(FrameworkElement element, bool treeNode)
        {
            if (!treeNode)
                return element.ActualHeight;
            var node = (TreeViewItem)element;
            var header = node.Header as FrameworkElement;
            return header == null ? 30 : header.ActualHeight + 12;
        }
    }
}
