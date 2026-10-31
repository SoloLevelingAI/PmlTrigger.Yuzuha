using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace RibbonStudio
{
    public partial class LibraryWindow : Window
    {
        readonly MainWindow main; ApiClient Api
        {
            get
            {
                return main.Api;
            }
        }
        readonly ListBox documents = new ListBox { DisplayMemberPath = "label", MinWidth = 210 }, versions = new ListBox { DisplayMemberPath = "sequence" };
        readonly DataGrid history = Ui.Grid("序号|sequence", "类型|kind", "保留策略|retention", "时间|createdAt", "说明|note");
        readonly DataGrid inventory = Ui.Grid("名称|name", "权限|permission", "UIC 路径|path", "已加载|enabled", "文件存在|exists");
        readonly ComboBox installations = Ui.Combo(), manifests = Ui.Combo(), origin = Ui.Combo(), permission = Ui.Combo(), state = Ui.Combo();
        readonly TextBox mon = Ui.Input(), search = Ui.Input(), docSearch = Ui.Input(); readonly TextBlock feedback = Ui.Text("", 11, Ui.Muted);
        List<Document> docs = new List<Document>(); List<Entry> entries = new List<Entry>(); Document current; Task initializeTask;
        public Task Ready()
        {
            return initializeTask ?? (initializeTask = Initialize());
        }
        async Task Initialize()
        {
            await LoadDocs();
            installations.ItemsSource = (await Api.Get<Targets>("api/v1/deployment/targets")).targets;
        }
        public LibraryWindow(MainWindow owner, bool embedded = false)
        {
            main = owner;
            Owner = owner;
            Title = "UIC 管理与持久化历史";
            Width = 1150;
            Height = 760;
            MinWidth = 850;
            MinHeight = 550;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = owner.FontFamily;
            Background = owner.Background;
            var root = new DockPanel { Margin = new Thickness(20) };
            Content = root;
            var heading = Ui.Text("UIC 管理与历史", 21);
            DockPanel.SetDock(heading, Dock.Top);
            root.Children.Add(heading);
            DockPanel.SetDock(feedback, Dock.Bottom);
            root.Children.Add(feedback);
            var tabs = new TabControl();
            root.Children.Add(tabs);
            tabs.Items.Add(new TabItem { Header = "加载条目管理", Content = Manager() });
            tabs.Items.Add(new TabItem { Header = "文档与快照", Content = History() });
            Loaded += async (s, e) => { try { await Ready(); } catch (Exception error) { feedback.Text = error.Message; } };
            if(!embedded) Chrome.Install(this);
            InstallMenus();
        }
        UIElement History()
        {
            var root = new DockPanel { Margin = new Thickness(12) };
            var note = Ui.Text("自动草稿保留最近 5 次；手动保存、导入、部署及恢复快照长期保留。恢复到编辑器不会直接覆盖实际 UIC。", 11, Ui.Muted);
            DockPanel.SetDock(note, Dock.Top);
            root.Children.Add(note);
            var actions = new WrapPanel();
            actions.Children.Add(Ui.Button("打开最新", () => Open(false)));
            actions.Children.Add(Ui.Button("查看选中版本", () => Open(true)));
            actions.Children.Add(Ui.Button("恢复为当前版本", Restore));
            actions.Children.Add(Ui.Button("导出该版本 UIC", () => Download(false)));
            actions.Children.Add(Ui.Button("下载原始文件", () => Download(true)));
            DockPanel.SetDock(actions, Dock.Bottom);
            root.Children.Add(actions);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(245) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(grid);
            var left = new DockPanel { Margin = new Thickness(0, 5, 12, 5) };
            left.Children.Add(documents);
            DockPanel.SetDock(docSearch, Dock.Top);
            left.Children.Insert(0, docSearch);
            grid.Children.Add(left);
            Grid.SetColumn(history, 1);
            grid.Children.Add(history);
            docSearch.ToolTip = "搜索文档名称或源路径";
            docSearch.TextChanged += (s, e) => FilterDocs();
            documents.SelectionChanged += async (s, e) => { current = documents.SelectedItem as Document; if (current != null) try { var id = current.id; var list = await Api.Post<Versions>("workspace/versions", new { documentId = id }); if (current != null && current.id == id) history.ItemsSource = list.versions; feedback.Text = current.sourcePath + " · " + current.count + " 条保留记录"; } catch (Exception error) { feedback.Text = error.Message; } };
            return root;
        }
        async Task LoadDocs()
        {
            docs = (await Api.Get<Documents>("api/v1/workspace/documents")).documents;
            FilterDocs();
        }
        void FilterDocs()
        {
            var q = docSearch.Text.ToLower();
            documents.ItemsSource = docs.Where(d => (d.label + " " + d.sourcePath).ToLower().Contains(q)).ToList();
        }
        async Task Open(bool selected)
        {
            if (current == null)
                throw new Exception("请选择文档");
            var version = history.SelectedItem as Version;
            if (selected && version == null)
                throw new Exception("请选择版本");
            var project = await Api.Post<Project>("workspace/open", new
            {
                documentId = current.id,
                versionId = selected ? version.id : null
            });
            await main.OpenDocument(project, selected);
            Close();
        }
        async Task Restore()
        {
            var version = history.SelectedItem as Version;
            if (current == null || version == null)
                throw new Exception("请选择版本");
            if (!Ui.Confirm("恢复该版本到编辑器并追加恢复快照？实际 UIC 需另行确认部署。"))
                return;
            await main.FlushDocumentById(current.id);
            var result = await Api.Post<SaveResult>("workspace/restore", new
            {
                documentId = current.id,
                versionId = version.id
            });
            await main.OpenDocument(result.project, true);
            Close();
        }
        async Task Download(bool original)
        {
            var version = history.SelectedItem as Version;
            if (current == null || version == null)
                throw new Exception("请选择版本");
            byte[] bytes;
            if (original)
                bytes = await Api.Bytes("workspace/original", new
                {
                    documentId = current.id,
                    versionId = version.id
                });
            else
            {
                var project = await Api.Post<Project>("workspace/open", new
                {
                    documentId = current.id,
                    versionId = version.id
                });
                bytes = await Api.Bytes("uic/export", project);
            }
            var path = Ui.Save("UIC 文件|*.uic", "version-" + version.sequence + ".uic");
            if (path != null)
                File.WriteAllBytes(path, bytes);
        }
        UIElement Manager()
        {
            var root = new DockPanel { Margin = new Thickness(12) };
            var top = new StackPanel();
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);
            top.Children.Add(Ui.Text("系统 / 未识别条目只读。自定义 UIC 可编辑；托管条目可移除或恢复加载，UIC 文件和长期快照保留。", 11, Ui.Muted));
            installations.ToolTip = "选择安装";
            top.Children.Add(installations);
            var paths = new DockPanel();
            paths.Children.Add(Ui.Button("检测目录", Inspect));
            mon.ToolTip = "mon.exe 完整路径";
            paths.Children.Add(mon);
            top.Children.Add(paths);
            top.Children.Add(manifests);
            top.Children.Add(Ui.Button("扫描条目", Scan));
            var filters = new WrapPanel();
            search.Width = 260;
            search.ToolTip = "搜索名称、路径";
            foreach (var value in new[] { "全部来源", "managed", "custom", "protected", "unknown" })
                origin.Items.Add(value);
            foreach (var value in new[] { "全部权限", "可编辑", "可管理加载", "只读" })
                permission.Items.Add(value);
            foreach (var value in new[] { "全部加载状态", "已加载", "已移除" })
                state.Items.Add(value);
            origin.SelectedIndex = permission.SelectedIndex = state.SelectedIndex = 0;
            filters.Children.Add(search);
            filters.Children.Add(origin);
            filters.Children.Add(permission);
            filters.Children.Add(state);
            top.Children.Add(filters);
            search.TextChanged += (s, e) => Filter();
            origin.SelectionChanged += (s, e) => Filter();
            permission.SelectionChanged += (s, e) => Filter();
            state.SelectionChanged += (s, e) => Filter();
            installations.SelectionChanged += async (s, e) => { var target = installations.SelectedItem as Target; if (target != null) { mon.Text = target.monPath; try { await Inspect(); } catch (Exception error) { feedback.Text = error.Message; } } };
            var actions = new WrapPanel();
            actions.Children.Add(Ui.Button("编辑原文件", Edit));
            actions.Children.Add(Ui.Button("接管加载管理", Adopt));
            actions.Children.Add(Ui.Button("移除 / 恢复加载", Toggle));
            DockPanel.SetDock(actions, Dock.Bottom);
            root.Children.Add(actions);
            root.Children.Add(inventory);
            inventory.SelectionChanged += (s, e) => { var row = inventory.SelectedItem as Entry; if (row != null) feedback.Text = row.reason + " · " + row.filePath; };
            return root;
        }
        async Task Inspect()
        {
            var target = await Api.Post<Target>("deployment/inspect", new
            {
                monPath = mon.Text.Trim()
            });
            mon.Text = target.monPath;
            manifests.ItemsSource = target.manifests;
            manifests.SelectedIndex = 0;
            entries.Clear();
            Filter();
        }
        async Task Scan()
        {
            if (manifests.SelectedItem == null)
                throw new Exception("请选择模块清单");
            var result = await Api.Post<Inventory>("management/list", new
            {
                monPath = mon.Text.Trim(),
                manifestName = manifests.SelectedItem.ToString()
            });
            entries = result.rows;
            Filter();
            feedback.Text = "已扫描 " + entries.Count + " 个加载条目";
        }
        void Filter()
        {
            if (origin.SelectedIndex < 0 || permission.SelectedIndex < 0 || state.SelectedIndex < 0)
                return;
            var q = search.Text.ToLower();
            inventory.ItemsSource = entries.Where(row => (row.name + " " + row.path).ToLower().Contains(q) && (origin.SelectedIndex == 0 || row.origin == origin.SelectedItem.ToString()) && (permission.SelectedIndex == 0 || permission.SelectedIndex == 1 && (row.origin == "custom" || row.origin == "managed") || permission.SelectedIndex == 2 && row.origin == "managed" || permission.SelectedIndex == 3 && (row.origin == "protected" || row.origin == "unknown")) && (state.SelectedIndex == 0 || state.SelectedIndex == 1 && row.enabled || state.SelectedIndex == 2 && !row.enabled)).ToList();
        }
        Entry Row()
        {
            var row = inventory.SelectedItem as Entry;
            if (row == null)
                throw new Exception("请选择加载条目");
            return row;
        }
        object TargetInput(Entry row)
        {
            return new
            {
                monPath = mon.Text.Trim(),
                manifestName = Convert.ToString(manifests.SelectedItem),
                key = row.key,
                expectedManifestHash = row.manifestHash,
                documentId = main.Project == null ? null : main.Project.documentId
            };
        }
        async Task Edit()
        {
            var row = Row();
            if (row.origin != "custom" && row.origin != "managed")
                throw new Exception("该条目只读：" + row.reason);
            var project = await Api.Post<Project>("management/open", TargetInput(row));
            await main.OpenDocument(project);
            Close();
        }
        async Task Adopt()
        {
            var row = Row();
            if (row.origin != "custom" && row.origin != "managed")
                throw new Exception("系统或未知条目不能接管");
            if (!Ui.Confirm("接管加载项 " + row.name + "？记录原路径和源文件快照，本步不修改 UIC 或 XML。"))
                return;
            await Api.Post<SaveResult>("management/adopt", TargetInput(row));
            await Scan();
            await LoadDocs();
        }
        async Task Toggle()
        {
            var row = Row();
            if (row.origin != "managed")
                throw new Exception("只有托管条目可修改加载状态");
            if (!Ui.Confirm((row.enabled ? "移除" : "恢复") + "加载登记：" + row.name + "\n" + row.path + "\nUIC 文件保留，XML 修改前会备份。"))
                return;
            await Api.Post<object>("management/toggle", new
            {
                monPath = mon.Text.Trim(),
                manifestName = Convert.ToString(manifests.SelectedItem),
                key = row.key,
                expectedManifestHash = row.manifestHash,
                enabled = !row.enabled
            });
            await Scan();
            await LoadDocs();
        }
        public async Task VerifyReadOnly(string monPath, string snapshot)
        {
            await Ready();
            if (docs.Count == 0)
                throw new Exception("No persisted document in library");
            current = docs[0];
            var records = await Api.Post<Versions>("workspace/versions", new
            {
                documentId = current.id
            });
            if (records.versions.Count == 0)
                throw new Exception("No history in library");
            history.ItemsSource = records.versions;
            mon.Text = monPath;
            await Inspect();
            await Scan();
            if (entries.Count == 0)
                throw new Exception("No module inventory in library");
            Ui.Capture(this, snapshot);
        }
    }
}
