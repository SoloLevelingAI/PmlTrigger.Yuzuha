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
    public partial class DeploymentWindow
    {
        void InstallMenus()
        {
            foreach (var grid in new[] { left, right })
            {
                var currentGrid = grid;
                currentGrid.PreviewMouseRightButtonDown += (s, e) => { var row = Ui.Ancestor<DataGridRow>(e.OriginalSource as DependencyObject); if (row != null) row.IsSelected = true; };
                currentGrid.ContextMenu = new ContextMenu();
                currentGrid.ContextMenuOpening += (s, e) => { var menu = currentGrid.ContextMenu; menu.Items.Clear(); var item = currentGrid.SelectedItem as ModuleRow; bool toRight = currentGrid == left; menu.Items.Add(Ui.Menu(toRight ? "加入本次选择 →" : "← 取消本次选择", () => { foreach (var row in rows) row.Marked = false; item.Marked = true; Move(toRight); return Task.FromResult(0); }, item != null && item.Editable)); menu.Items.Add(Ui.Menu("勾选当前筛选结果", () => { Mark(currentGrid); return Task.FromResult(0); })); menu.Items.Add(new Separator()); menu.Items.Add(Ui.Menu("预览全部待加入项", () => Run(PreviewModules), rows.Any(r => r.Selected && !r.Existing))); menu.Items.Add(Ui.Menu("复制模块名称", () => { Clipboard.SetText(item.manifestName); return Task.FromResult(0); }, item != null)); };
            }
        }
    }
}
