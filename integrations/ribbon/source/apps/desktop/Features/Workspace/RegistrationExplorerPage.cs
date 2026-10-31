using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RibbonStudio
{
    // A single read-only relationship explorer for every UIC in the workspace.
    // Registration changes remain in the existing separate management tool.
    public sealed class RegistrationExplorerPage : UserControl
    {
        sealed class PathRow
        {
            public string Key { get; set; }
            public string Name { get; set; }
            public string FullPath { get; set; }
            public int Count { get; set; }
            public string Status { get; set; }
        }
        sealed class RelationRow
        {
            public XmlRegistration Source;
            public string Uic { get { return Source.uicPath; } }
            public string Xml { get { return Source.xmlPath; } }
            public string Product { get { return Source.product; } }
            public string Version { get { return Source.version; } }
            public string Origin { get { return Source.origin; } }
        }
        readonly MainWindow owner;
        readonly DockPanel root = new DockPanel { Margin = new Thickness(16, 10, 16, 12) };
        readonly TextBlock summary = Ui.Text("尚未扫描", 13);
        readonly TextBlock feedback = Ui.Text("只读查询；登记变更由独立管理工具完成。", 12, Ui.Muted);
        readonly TextBlock selectedSummary = Ui.Text("请选择左侧路径", 13);
        readonly TextBox search = Ui.Input();
        readonly TextBox scopeInput = Ui.Input();
        readonly ComboBox direction = Ui.Combo(), products = Ui.Combo(), origins = Ui.Combo();
        readonly DataGrid paths = Ui.Grid("UIC / XML|Name", "完整位置|FullPath", "关联数|Count", "扫描状态|Status");
        readonly DataGrid relations = Ui.Grid("UIC 完整路径|Uic", "XML 完整路径|Xml", "产品|Product", "版本|Version", "来源|Origin");
        readonly StackPanel details = new StackPanel(), diagnostics = new StackPanel();
        readonly HashSet<string> extraMons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Button manage;
        RegistrationScan scan;
        List<XmlRegistration> allRelations = new List<XmlRegistration>();
        List<Project> openProjects = new List<Project>();
        string selectedKey, selectedUic, requestedUic, xmlScope;
        bool rendering;
        public bool IsWorking { get; private set; }
        ApiClient Api { get { return owner.Api; } }

        public RegistrationExplorerPage(MainWindow main, bool compact = false)
        {
            if (main == null) throw new ArgumentNullException("main");
            owner = main; Content = root;
            var heading = new StackPanel();
            heading.Children.Add(Ui.Text("UIC ↔ XML 关系查询", 20));
            heading.Children.Add(Ui.Text("聚合已打开 UIC 与扫描发现的登记，按两个方向查看；此页不写入 UIC 或 XML。", 12, Ui.Muted));
            var actions = new WrapPanel();
            actions.Children.Add(Ui.Button("重新扫描", Reload));
            actions.Children.Add(Ui.Button("添加 UIC…", AddUic));
            actions.Children.Add(Ui.Button("选择 mon.exe…", () => Run(ChooseMon)));
            actions.Children.Add(Ui.Button("选择 XML…", () => Run(ChooseXml)));
            actions.Children.Add(Ui.Sync("全部扫描结果", () => {
                xmlScope = null; requestedUic = null; selectedKey = null;
                rendering = true; scopeInput.Text = ""; search.Text = ""; products.SelectedIndex = 0; origins.SelectedIndex = 0; rendering = false;
                Render();
            }));
            heading.Children.Add(actions);
            var scope = new DockPanel();
            var query = Ui.Button("查询路径", () => Run(QueryPath)); DockPanel.SetDock(query, Dock.Right); scope.Children.Add(query);
            scopeInput.ToolTip = "输入 mon.exe、UIC 或 XML 完整路径；XML 仅查询本次已知扫描结果"; scope.Children.Add(scopeInput); heading.Children.Add(scope);
            var filters = new WrapPanel();
            direction.ItemsSource = new[] { "UIC → XML", "XML → UIC" }; direction.SelectedIndex = 0;
            products.ItemsSource = new[] { "全部产品", "E3D", "AM", "PDMS", "未知" }; products.SelectedIndex = 0;
            origins.ItemsSource = new[] { "全部来源", "部署器", "原生 / 只读", "既有自定义 / 未接管" }; origins.SelectedIndex = 0;
            foreach (var combo in new[] { direction, products, origins }) { combo.Margin = new Thickness(0, 4, 12, 8); filters.Children.Add(combo); combo.SelectionChanged += (s,e) => { if (!rendering) { selectedKey = null; Render(); } }; }
            search.Width = 290; search.ToolTip = "筛选文件名、完整路径、登记名称、产品版本或来源"; search.Margin = new Thickness(0, 4, 0, 8); search.TextChanged += (s,e) => { if (!rendering) Render(); }; filters.Children.Add(search);
            heading.Children.Add(filters); heading.Children.Add(summary);
            DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
            var footer = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            manage = Ui.Button("管理登记…", () => owner.OpenTool(true, selectedUic));
            manage.IsEnabled = false; manage.ToolTip = "打开独立登记管理工具；此查询页本身不执行写入"; DockPanel.SetDock(manage, Dock.Right); footer.Children.Add(manage);
            footer.Children.Add(feedback); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
            var errors = new Expander { Header = "扫描范围、未解析路径与读取错误", Content = Ui.Scroll(diagnostics), MaxHeight = 190, Margin = new Thickness(0, 6, 0, 0) };
            DockPanel.SetDock(errors, Dock.Bottom); root.Children.Add(errors);
            var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) }); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
            paths.Margin = new Thickness(0, 0, 12, 0); paths.MinColumnWidth = 50; paths.Columns[0].Width = 125; paths.Columns[2].Width = 58; paths.Columns[3].Width = 125;
            paths.SelectionChanged += (s,e) => { if (rendering) return; var row = paths.SelectedItem as PathRow; selectedKey = row == null ? null : row.Key; RenderRelations(); };
            body.Children.Add(Ui.TableCard(paths));
            var right = new DockPanel(); Grid.SetColumn(right, 1); body.Children.Add(right);
            DockPanel.SetDock(selectedSummary, Dock.Top); right.Children.Add(selectedSummary);
            var detail = new Expander { Header = "所选关系的完整信息", Content = Ui.Scroll(details), MaxHeight = 235, IsExpanded = false }; DockPanel.SetDock(detail, Dock.Bottom); right.Children.Add(detail);
            relations.MinColumnWidth = 45; relations.Columns[2].Width = 55; relations.Columns[3].Width = 85; relations.Columns[4].Width = 115;
            relations.SelectionChanged += (s,e) => RenderRelationDetails(); right.Children.Add(Ui.TableCard(relations));
            root.Children.Add(body);
            if (compact) {
                root.Margin = new Thickness(8, 2, 8, 4); root.Children.Remove(heading);
                filters.Children.Remove(search); filters.Children.Remove(direction); heading.Children.Remove(summary); search.Width = 170; direction.Width = 100;
                var compactHeading = new DockPanel(); var options = new Expander { Header = "范围 / 筛选", Content = heading, MaxHeight = 240 };
                heading.Children[0].Visibility = Visibility.Collapsed; heading.Children[1].Visibility = Visibility.Collapsed;
                root.Children.Remove(errors); heading.Children.Add(errors);
                var optionsMenu = new ContextMenu(); var menuBody = Ui.Scroll(heading); menuBody.MaxHeight = 480; menuBody.Width = 530; optionsMenu.Items.Add(new MenuItem { Header = menuBody, StaysOpenOnClick = true });
                var optionsButton = Ui.Sync("⋯", () => optionsMenu.IsOpen = true); optionsButton.ToolTip = "查询范围、筛选与扫描诊断"; optionsMenu.PlacementTarget = optionsButton;
                var toolbar = new WrapPanel(); toolbar.Children.Add(direction); toolbar.Children.Add(search); toolbar.Children.Add(Ui.Button("重新扫描", Reload)); toolbar.Children.Add(optionsButton); toolbar.Children.Add(summary); compactHeading.Children.Add(toolbar);
                DockPanel.SetDock(compactHeading, Dock.Top); root.Children.Insert(0, compactHeading);
                summary.FontSize = 10; summary.MaxWidth = 145; summary.TextWrapping = TextWrapping.NoWrap; summary.TextTrimming = TextTrimming.CharacterEllipsis; summary.SetBinding(FrameworkElement.ToolTipProperty, new System.Windows.Data.Binding("Text") { Source = summary });
                feedback.TextWrapping = TextWrapping.NoWrap; feedback.TextTrimming = TextTrimming.CharacterEllipsis; feedback.FontSize = 10; feedback.Text = "只读查询 · 变更需确认"; footer.Margin = new Thickness(0); errors.Header = "扫描诊断";
                selectedSummary.MaxHeight = 28; selectedSummary.FontSize = 10; selectedSummary.TextWrapping = TextWrapping.NoWrap; selectedSummary.TextTrimming = TextTrimming.CharacterEllipsis;
                paths.RowHeight = 30; relations.RowHeight = 30; paths.FontSize = 10; relations.FontSize = 10; paths.Columns[0].Width = 100; paths.Columns[3].Visibility = Visibility.Collapsed;
                paths.Columns[1].Visibility = Visibility.Collapsed; relations.Columns[0].Visibility = Visibility.Collapsed;
                var productStyle = new Style(typeof(TextBlock));
                foreach (var pair in new[] { Tuple.Create("E3D", "#3159E8"), Tuple.Create("AM", "#9756A1"), Tuple.Create("PDMS", "#16826A") }) { var trigger = new DataTrigger { Binding = new System.Windows.Data.Binding("Product"), Value = pair.Item1 }; trigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, (Brush)new BrushConverter().ConvertFromString(pair.Item2))); productStyle.Triggers.Add(trigger); }
                ((DataGridTextColumn)relations.Columns[2]).ElementStyle = productStyle;
                relations.Columns[2].Width = 45; relations.Columns[3].Width = 65; relations.Columns[4].Width = 80;
                body.ColumnDefinitions[0].Width = new GridLength(0.65, GridUnitType.Star); paths.Margin = new Thickness(0,0,8,0);
            }
        }

        async Task Run(Func<Task> action)
        {
            if (IsWorking) return;
            IsWorking = true; root.IsEnabled = false;
            try { await action(); }
            catch (Exception error) { feedback.Text = "查询未完成：" + error.Message + "；当前显示可能是上次结果。"; }
            finally { IsWorking = false; root.IsEnabled = true; UpdateManage(); }
        }

        public Task Reload() { return Run(ReloadCore); }

        async Task ReloadCore()
        {
            openProjects = owner.OpenProjects().Where(p => p != null).ToList();
            var mons = new HashSet<string>(extraMons, StringComparer.OrdinalIgnoreCase);
            string profileError = null;
            try
            {
                var profiles = await Api.Post<DeploymentProfiles>("registration/profiles", new { });
                foreach (var profile in profiles.profiles ?? new List<DeploymentProfile>())
                    if (!String.IsNullOrWhiteSpace(profile.monPath)) mons.Add(profile.monPath);
            }
            catch (Exception error) { profileError = "部署配置读取失败：" + error.Message; }
            foreach (var project in openProjects)
                if (project.preferredBinding != null && !String.IsNullOrWhiteSpace(project.preferredBinding.monPath)) mons.Add(project.preferredBinding.monPath);
            var next = await Api.Post<RegistrationScan>("registration/scan", new { monPaths = mons.ToArray() });
            next.rows = next.rows ?? new List<XmlRegistration>(); next.targets = next.targets ?? new List<Target>(); next.errors = next.errors ?? new List<string>();
            if (profileError != null) next.errors.Add(profileError);
            scan = next;
            allRelations = scan.rows.Where(r => r != null && !String.IsNullOrWhiteSpace(r.uicPath) && !String.IsNullOrWhiteSpace(r.xmlPath))
                .GroupBy(r => Canonical(r.uicPath) + "\0" + Canonical(r.xmlPath), StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
            Render();
        }

        static string Canonical(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return "";
            try { return Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch { return path.Trim(); }
        }
        static bool Same(string a, string b) { return !String.IsNullOrWhiteSpace(a) && !String.IsNullOrWhiteSpace(b) && String.Equals(Canonical(a), Canonical(b), StringComparison.OrdinalIgnoreCase); }
        static string FileLabel(string path) { try { return Path.GetFileName(path); } catch { return path; } }
        static string ProjectPath(Project project)
        {
            if (project.origin != null && !String.IsNullOrWhiteSpace(project.origin.sourcePath)) return project.origin.sourcePath;
            return project.preferredBinding == null ? null : project.preferredBinding.filePath;
        }
        IEnumerable<string> KnownXmlPaths()
        {
            var result = allRelations.Select(r => r.xmlPath).ToList();
            if (scan != null)
                foreach (var target in scan.targets)
                    foreach (var manifest in target.manifests ?? new List<string>())
                        try { result.Add(Path.GetFullPath(Path.Combine(target.directory, manifest))); } catch { }
            return result.Where(p => !String.IsNullOrWhiteSpace(p)).Select(Canonical).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        IEnumerable<XmlRegistration> FilteredRelations()
        {
            string product = Convert.ToString(products.SelectedItem), origin = Convert.ToString(origins.SelectedItem), query = search.Text ?? "";
            return allRelations.Where(r => (xmlScope == null || Same(r.xmlPath, xmlScope))
                && (products.SelectedIndex <= 0 || String.Equals(String.IsNullOrEmpty(r.product) ? "未知" : r.product, product, StringComparison.OrdinalIgnoreCase))
                && (origins.SelectedIndex <= 0 || String.Equals(r.origin, origin, StringComparison.OrdinalIgnoreCase))
                && (query.Length == 0 || ((r.uicPath ?? "") + " " + r.xmlPath + " " + r.entryName + " " + r.product + " " + r.version + " " + r.origin).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        void Render()
        {
            if (rendering) return; rendering = true;
            try
            {
                var matches = FilteredRelations().ToList(); bool byXml = direction.SelectedIndex == 1;
                var rows = new List<PathRow>();
                var groups = matches.GroupBy(r => Canonical(byXml ? r.xmlPath : r.uicPath), StringComparer.OrdinalIgnoreCase);
                foreach (var group in groups) rows.Add(new PathRow { Key = group.Key, FullPath = group.Key, Name = FileLabel(group.Key), Count = group.Select(r => Canonical(byXml ? r.uicPath : r.xmlPath)).Distinct(StringComparer.OrdinalIgnoreCase).Count(), Status = "有已解析关系" });
                bool unrestricted = products.SelectedIndex <= 0 && origins.SelectedIndex <= 0;
                string query = search.Text ?? "";
                if (byXml && unrestricted)
                {
                    foreach (var xml in KnownXmlPaths().Where(x => xmlScope == null || Same(x, xmlScope)))
                        if (!rows.Any(r => Same(r.Key, xml)) && (query.Length == 0 || xml.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                            rows.Add(new PathRow { Key = xml, FullPath = xml, Name = FileLabel(xml), Status = "无已解析关系 / 见错误" });
                }
                if (!byXml && xmlScope == null && unrestricted)
                {
                    foreach (var project in openProjects)
                    {
                        string path = ProjectPath(project);
                        string key = String.IsNullOrWhiteSpace(path) ? "draft:" + (project.documentId ?? project.@namespace) : Canonical(path);
                        string label = String.IsNullOrWhiteSpace(path) ? project.@namespace + "（未生成 UIC）" : FileLabel(path);
                        if (!rows.Any(r => String.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase)) && (query.Length == 0 || (label + " " + path).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                            rows.Add(new PathRow { Key = key, FullPath = path ?? "尚无 UIC 路径", Name = label, Status = "已打开 / 无已知关系" });
                    }
                    if (!String.IsNullOrWhiteSpace(requestedUic) && !rows.Any(r => Same(r.Key, requestedUic)) && (query.Length == 0 || requestedUic.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                        rows.Add(new PathRow { Key = Canonical(requestedUic), FullPath = requestedUic, Name = FileLabel(requestedUic), Status = "未发现关系 / 范围有限" });
                }
                if (byXml && xmlScope != null && !rows.Any(r => Same(r.Key, xmlScope)))
                    rows.Add(new PathRow { Key = Canonical(xmlScope), FullPath = xmlScope, Name = FileLabel(xmlScope), Status = "无已知关系 / 范围有限" });
                rows = rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Key, StringComparer.OrdinalIgnoreCase).ToList();
                paths.ItemsSource = rows;
                var selected = rows.FirstOrDefault(r => String.Equals(r.Key, selectedKey, StringComparison.OrdinalIgnoreCase)) ?? rows.FirstOrDefault();
                selectedKey = selected == null ? null : selected.Key; paths.SelectedItem = selected;
                if (selected != null) paths.Dispatcher.BeginInvoke(new Action(() => { if (paths.SelectedItem == selected) { paths.UpdateLayout(); paths.ScrollIntoView(selected); } }));
                int totalUic = allRelations.Select(r => Canonical(r.uicPath)).Concat(openProjects.Select(ProjectPath).Where(x => !String.IsNullOrWhiteSpace(x)).Select(Canonical)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                summary.Text = "扫描范围：" + (scan == null ? 0 : scan.targets.Count) + " 个安装 · " + totalUic + " 个有路径 UIC · " + KnownXmlPaths().Count() + " 个 XML 候选清单 · " + allRelations.Count + " 条去重关系；当前筛选 " + matches.Count + " 条。";
                if (scan == null) feedback.Text = "尚未扫描；不能据此判断 UIC 是否已登记。";
                else if (xmlScope != null) feedback.Text = "指定 XML 仅查询扫描已知结果；" + (KnownXmlPaths().Any(x => Same(x, xmlScope)) ? "位于候选扫描范围，读取完整性仍需查看错误。" : "未在扫描范围，结果不完整；请选择所属安装 mon.exe 后重新扫描。 ");
                else feedback.Text = "扫描时间：" + scan.scannedAt + " · " + scan.errors.Count + " 项读取/解析问题；计数只覆盖已知范围，空结果不证明未登记。";
                RenderDiagnostics();
            }
            finally { rendering = false; }
            RenderRelations();
        }

        void RenderRelations()
        {
            bool byXml = direction.SelectedIndex == 1;
            var filtered = FilteredRelations().Where(r => Same(byXml ? r.xmlPath : r.uicPath, selectedKey)).Select(r => new RelationRow { Source = r }).ToList();
            relations.ItemsSource = filtered;
            selectedUic = !byXml && selectedKey != null && !selectedKey.StartsWith("draft:", StringComparison.Ordinal) ? selectedKey : null;
            selectedSummary.Text = selectedKey == null ? "请选择 UIC 或 XML" : (byXml ? "XML → UIC：" : "UIC → XML：") + selectedKey + "\n当前筛选中 " + filtered.Count + " 条已解析关系";
            if (filtered.Count > 0) relations.SelectedIndex = 0;
            else { details.Children.Clear(); details.Children.Add(Ui.Text("没有符合筛选的已解析关系。请核对扫描范围、错误和筛选条件。", 12, Ui.Muted)); UpdateManage(); }
        }

        void FullPath(string label, string value)
        {
            details.Children.Add(Ui.Text(label, 12, Ui.Muted));
            var field = Ui.Input(String.IsNullOrEmpty(value) ? "未知 / 无" : value); field.IsReadOnly = true; field.TextWrapping = TextWrapping.Wrap; details.Children.Add(field);
        }
        void RenderRelationDetails()
        {
            details.Children.Clear(); var row = relations.SelectedItem as RelationRow;
            if (row == null) { UpdateManage(); return; }
            selectedUic = row.Source.uicPath;
            FullPath("UIC 完整路径", row.Source.uicPath); FullPath("XML 完整路径", row.Source.xmlPath);
            FullPath("安装 mon.exe", row.Source.monPath);
            details.Children.Add(Ui.Text("产品：" + row.Source.product + " · 版本：" + row.Source.version + " · 版本来源：" + row.Source.versionSource, 12));
            details.Children.Add(Ui.Text("登记名称：" + row.Source.entryName + " · 来源：" + row.Source.origin, 12));
            UpdateManage();
        }
        void UpdateManage() { manage.IsEnabled = !IsWorking && !String.IsNullOrWhiteSpace(selectedUic) && !selectedUic.StartsWith("draft:", StringComparison.Ordinal); }

        void RenderDiagnostics()
        {
            diagnostics.Children.Clear();
            diagnostics.Children.Add(Ui.Text("扫描器只覆盖发现/配置/指定的安装清单，不递归检索任意磁盘 XML；XML 候选数量不等于全部读取成功。", 12, Ui.Muted));
            if (scan == null) return;
            foreach (var target in scan.targets) diagnostics.Children.Add(Ui.Text("安装：" + target.monPath + "\n" + target.product + " " + target.version + "（" + target.versionSource + "）", 12));
            foreach (var path in KnownXmlPaths()) diagnostics.Children.Add(Ui.Text("XML 候选：" + path, 12, Ui.Muted));
            foreach (var error in scan.errors) diagnostics.Children.Add(Ui.Text("未解析 / 未读取：" + error, 12));
            if (scan.errors.Count == 0) diagnostics.Children.Add(Ui.Text("扫描器未报告读取错误；范围以以上安装和候选清单为限。", 12, Ui.Muted));
        }

        public void SelectPath(string uicPath)
        {
            if (String.IsNullOrWhiteSpace(uicPath)) return;
            requestedUic = Canonical(uicPath); xmlScope = null; selectedKey = requestedUic;
            rendering = true; direction.SelectedIndex = 0; products.SelectedIndex = 0; origins.SelectedIndex = 0; search.Text = ""; scopeInput.Text = requestedUic; rendering = false;
            Render();
        }
        internal void AddMonScope(string monPath)
        {
            if (String.IsNullOrWhiteSpace(monPath)) throw new ArgumentException("请输入 mon.exe 完整路径。");
            extraMons.Add(Canonical(monPath));
        }
        internal void SelectXmlPath(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("请输入 XML 完整路径。");
            SelectXml(path);
        }
        internal XmlRegistration[] RelationSnapshot()
        {
            return allRelations.ToArray();
        }
        internal XmlRegistration[] VisibleRelationSnapshot()
        {
            return relations.Items.OfType<RelationRow>().Select(r => r.Source).ToArray();
        }
        internal string[] DiagnosticMessages()
        {
            return scan == null ? new string[0] : scan.errors.ToArray();
        }
        async Task ChooseMon()
        {
            var path = Ui.Open("AVEVA 主程序|mon.exe"); if (path == null) return;
            extraMons.Add(Canonical(path)); scopeInput.Text = path; await ReloadCore();
        }
        Task ChooseXml()
        {
            var path = Ui.Open("模块 XML 清单|*.xml"); if (path == null) return Task.FromResult(0);
            SelectXml(path); return Task.FromResult(0);
        }
        void SelectXml(string path)
        {
            xmlScope = Canonical(path); selectedKey = xmlScope; requestedUic = null;
            rendering = true; direction.SelectedIndex = 1; products.SelectedIndex = 0; origins.SelectedIndex = 0; search.Text = ""; scopeInput.Text = xmlScope; rendering = false;
            Render();
        }
        async Task QueryPath()
        {
            var path = scopeInput.Text.Trim(); if (path.Length == 0) { xmlScope = null; requestedUic = null; selectedKey = null; Render(); return; }
            if (FileLabel(path).Equals("mon.exe", StringComparison.OrdinalIgnoreCase)) { extraMons.Add(Canonical(path)); await ReloadCore(); }
            else if (Path.GetExtension(path).Equals(".xml", StringComparison.OrdinalIgnoreCase)) SelectXml(path);
            else if (Path.GetExtension(path).Equals(".uic", StringComparison.OrdinalIgnoreCase)) SelectPath(path);
            else throw new ArgumentException("请输入 mon.exe、.xml 或 .uic 的完整路径。");
        }
        async Task AddUic()
        {
            string last = null;
            foreach (var path in Ui.OpenMany("UIC 文件|*.uic")) { await owner.ImportPath(path); last = path; }
            if (last == null) return;
            await owner.OpenRegistrationExplorer(last);
        }
    }
}
