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
    public partial class LibraryWindow
    {
        void InstallMenus()
        {
            inventory.PreviewMouseRightButtonDown += (s, e) => { var row = Ui.Ancestor<DataGridRow>(e.OriginalSource as DependencyObject); if (row != null) row.IsSelected = true; };
            inventory.ContextMenu = new ContextMenu();
            inventory.ContextMenuOpening += (s, e) =>
            {
                var menu = inventory.ContextMenu;
                menu.Items.Clear();
                var row = inventory.SelectedItem as Entry;
                bool editable = row != null && (row.origin == "managed" || row.origin == "custom"), managed = row != null && row.origin == "managed";
                menu.Items.Add(Ui.Menu("编辑原 UIC", Edit, editable));
                menu.Items.Add(Ui.Menu("查看条目详情", () => { Ui.Message(row.name + "\n" + row.path + "\n\n" + row.reason, "加载条目详情"); return Task.FromResult(0); }, row != null));
                menu.Items.Add(new Separator());
                menu.Items.Add(Ui.Menu("接管加载管理…", Adopt, row != null && row.origin == "custom"));
                menu.Items.Add(Ui.Menu(row != null && row.enabled ? "移除加载…" : "恢复加载…", Toggle, managed));
                menu.Items.Add(new Separator());
                menu.Items.Add(Ui.Menu("复制 UIC 路径", () => { Clipboard.SetText(row.filePath ?? row.path); return Task.FromResult(0); }, row != null));
                menu.Items.Add(Ui.Menu("刷新条目", Scan, manifests.SelectedItem != null));
            };
            history.PreviewMouseRightButtonDown += (s, e) => { var row = Ui.Ancestor<DataGridRow>(e.OriginalSource as DependencyObject); if (row != null) row.IsSelected = true; };
            history.ContextMenu = new ContextMenu();
            history.ContextMenuOpening += (s, e) => { var menu = history.ContextMenu; menu.Items.Clear(); var version = history.SelectedItem as Version; bool selected = current != null && version != null; menu.Items.Add(Ui.Menu("查看此版本", () => Open(true), selected)); menu.Items.Add(Ui.Menu("恢复到编辑器…", Restore, selected)); menu.Items.Add(new Separator()); menu.Items.Add(Ui.Menu("导出此版本 UIC…", () => Download(false), selected)); menu.Items.Add(Ui.Menu("下载原始 UIC…", () => Download(true), selected && version.hasOriginal)); menu.Items.Add(Ui.Menu("复制版本标识", () => { Clipboard.SetText(version.id); return Task.FromResult(0); }, selected)); };
            documents.PreviewMouseRightButtonDown += (s, e) => { var row = Ui.Ancestor<ListBoxItem>(e.OriginalSource as DependencyObject); if (row != null) row.IsSelected = true; };
            var docMenu = new ContextMenu();
            docMenu.Items.Add(Ui.Menu("打开最新版本", () => Open(false)));
            docMenu.Items.Add(Ui.Menu("刷新文档", LoadDocs));
            docMenu.Opened += (s, e) => ((MenuItem)docMenu.Items[0]).IsEnabled = current != null;
            documents.ContextMenu = docMenu;
        }
    }

}
