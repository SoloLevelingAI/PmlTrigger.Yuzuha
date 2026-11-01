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
    public sealed class DeploymentFileRow
    {
        public Project Project; public bool Marked { get; set; }
        public string Path { get; set; } public string Name { get; set; } public int XmlCount { get; set; }
        public string XmlSummary { get { return XmlCount + " 个 XML  ▸"; } }
        public string Products { get; set; } public string Status { get; set; }
    }
    public sealed class StudioToolPage : UserControl
    {
        readonly MainWindow main; readonly bool registration; readonly DockPanel root = new DockPanel(); readonly Grid content = new Grid();
        readonly TextBlock feedback = Ui.Text("读取真实登记数据…", 11, Ui.Muted); readonly Button profileButton; Button reviewButton;
        readonly List<DeploymentFileRow> files = new List<DeploymentFileRow>(); readonly List<PendingRegistration> pending = new List<PendingRegistration>();
        readonly List<PreparedDeployment> prepared = new List<PreparedDeployment>();
        readonly DataGrid fileGrid = Ui.Grid("UIC 文件|Name", "完整位置|Path", "已登记 XML|XmlSummary", "产品 / 版本|Products", "状态|Status");
        readonly ListBox uicList = new ListBox { BorderThickness = new Thickness(0), Background = Brushes.White };
        readonly StackPanel details = new StackPanel(); readonly TextBox search = Ui.Input();
        RegistrationScan scan; DeploymentProfiles profiles; DeploymentProfile profile; DeploymentFileRow selected; string focusPath;
        internal readonly List<string> ExtraMons = new List<string>();
        public bool IsWorking { get; private set; }
        ApiClient Api { get { return main.Api; } }
        readonly FrozenDeploymentClient frozen;
        sealed class PendingRegistration { public Project Project; public Target Target; public string Manifest, Name; public Plan Plan; public FrozenPlan Frozen; }
        sealed class PreparedDeployment { public Project Project; public Plan Plan; public string Fingerprint; public FrozenPlan Frozen; }
        public StudioToolPage(MainWindow owner, bool registrations, bool compact = false)
        {
            main = owner; registration = registrations; Content = root;
            frozen = new FrozenDeploymentClient(Api);
            var commands = new DockPanel { Background = Brushes.White, Height = 58, Margin = new Thickness(0, 0, 0, 8) };
            var heading = Ui.Text(registration ? "加载登记" : "部署", 15); heading.Margin = new Thickness(20, 15, 20, 0); commands.Children.Add(heading);
            commands.Children.Add(Ui.Button("＋ 添加 UIC", AddUic));
            if (!registration) commands.Children.Add(Ui.Button("从已打开文件加入", Reload));
            profileButton = Ui.Button("部署配置 ▾", ChooseProfile); DockPanel.SetDock(profileButton, Dock.Right); commands.Children.Add(profileButton);
            commands.Children.Add(Ui.Button("重新扫描", () => Run(ReloadCore)));
            commands.Children.Add(Ui.Sync("人工核对后解除写入暂停", () => { if (FrozenDeploymentClient.ConfirmManualCheck(main)) feedback.Text = "写入暂停已解除；请重新预览。实际 Ribbon 加载未核验。"; }));
            var commandItems = commands.Children.Cast<UIElement>().Where(c => c != profileButton).ToList(); commands.Children.Clear(); commands.Children.Add(profileButton);
            var commandRow = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var c in commandItems) { var b = c as Button; if (b != null) { b.Height = 32; b.MinHeight = 0; b.VerticalAlignment = VerticalAlignment.Center; b.Padding = new Thickness(12, 4, 12, 4); } commandRow.Children.Add(c); } commands.Children.Add(commandRow);
            profileButton.Height = 32; profileButton.MinHeight = 0; profileButton.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(commands, Dock.Top); root.Children.Add(commands);
            var bottom = new DockPanel { Background = Brushes.White, MinHeight = 64 };
            var apply = Ui.Button(registration ? "预览 XML 登记变更" : "预览所选 UIC", () => Run(registration ? (Func<Task>)ReviewRegistration : ReviewDeployment), true); DockPanel.SetDock(apply, Dock.Right); bottom.Children.Add(apply);
            reviewButton = apply; apply.Height = 36; apply.MinHeight = 0; apply.VerticalAlignment = VerticalAlignment.Center; apply.Margin = new Thickness(12, 10, 20, 10);
            feedback.Margin = new Thickness(20, 14, 10, 10); bottom.Children.Add(feedback); DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
            content.Margin = new Thickness(18, 8, 18, 12); root.Children.Add(content);
            search.ToolTip = "筛选 UIC 文件名或完整路径"; search.TextChanged += (s,e) => RenderFiles();
            if (registration) BuildRegistration(); else BuildDeployment();
            if (compact) {
                commands.Height = 38; commands.Margin = new Thickness(0); heading.Visibility = Visibility.Collapsed;
                foreach (var button in commandRow.Children.OfType<Button>()) { button.FontSize = 10; button.Padding = new Thickness(6,2,6,2); button.Margin = new Thickness(2); }
                profileButton.FontSize = 10; profileButton.MaxWidth = 190; profileButton.Padding = new Thickness(6,2,6,2);
                bottom.MinHeight = 38; apply.Height = 28; apply.Margin = new Thickness(6,4,6,4); apply.FontSize = 10; feedback.Margin = new Thickness(8,4,4,4); feedback.FontSize = 10;
                content.Margin = new Thickness(8,2,8,2); search.Height = 28; search.MinHeight = 0; search.Margin = new Thickness(0,2,0,2);
                fileGrid.FontSize = 11; fileGrid.RowHeight = 32; fileGrid.Columns[1].Width = 120; fileGrid.Columns[3].Width = 85;
                var fileHeading = content.Children.OfType<DockPanel>().FirstOrDefault();
                if (fileHeading != null) foreach (var block in fileHeading.Children.OfType<StackPanel>().SelectMany(p => p.Children.OfType<TextBlock>())) block.Visibility = Visibility.Collapsed;
                if (registration) content.ColumnDefinitions[0].Width = new GridLength(180);
            }
        }
        async Task Run(Func<Task> action)
        {
            if (IsWorking) return; IsWorking = true; root.IsEnabled = false;
            try { await action(); } catch (Exception e) { feedback.Text = e.Message; } finally { IsWorking = false; root.IsEnabled = true; }
        }
        public Task Reload() { return Run(ReloadCore); }
        async Task ReloadCore()
        {
            var docs = (await Api.Get<Documents>("api/v1/workspace/documents")).documents;
            foreach (var project in main.OpenProjects())
            {
                var row = files.FirstOrDefault(f => f.Project.documentId == project.documentId);
                if (row == null) { row = new DeploymentFileRow(); files.Add(row); }
                row.Project = project;
                var doc = docs.FirstOrDefault(d => d.id == project.documentId);
                row.Path = doc != null && !String.IsNullOrEmpty(doc.sourcePath) ? doc.sourcePath : project.origin == null ? "" : project.origin.sourcePath;
                row.Name = String.IsNullOrEmpty(row.Path) ? project.@namespace + ".uic" : Path.GetFileName(row.Path);
            }
            profiles = await Api.Post<DeploymentProfiles>("registration/profiles", new { });
            profile = profile == null ? profiles.profiles.FirstOrDefault() : profiles.profiles.FirstOrDefault(p => p.id == profile.id);
            profileButton.Content = "部署配置：" + (profile == null ? "选择 / 新建" : profile.name) + " ▾";
            scan = await Api.Post<RegistrationScan>("registration/scan", new { monPaths = ExtraMons.Concat(profile == null ? new string[0] : new[] { profile.monPath }).ToArray() });
            foreach (var row in files)
            {
                var refs = References(row.Path); row.XmlCount = refs.Select(r => r.xmlPath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                row.Products = String.Join(" · ", refs.Select(r => r.product + " " + r.version).Distinct());
                row.Status = String.IsNullOrEmpty(row.Path) ? "尚未部署" : row.XmlCount == 0 ? "未登记 / 可更新文件" : "已登记";
            }
            RenderFiles();
            if (selected == null) selected = files.FirstOrDefault();
            if (focusPath != null) selected = files.FirstOrDefault(f => Same(f.Path, focusPath)) ?? selected;
            uicList.SelectedItem = selected; RenderDetails();
            feedback.Text = "已读取 " + scan.targets.Count + " 个安装 · " + scan.scannedAt + (scan.errors.Count > 0 ? " · 未读取 " + scan.errors.Count + " 项（右键状态查看）" : " · 尚未写入");
            feedback.ToolTip = String.Join("\n", scan.errors);
            if (frozen.Locked) feedback.Text = frozen.Status;
            var errors = new ContextMenu(); var show = new MenuItem { Header = "查看扫描范围与未读取项" }; show.Click += (s,e) => Ui.Message(String.Join("\n", scan.targets.Select(t=>t.monPath)) + "\n" + String.Join("\n", scan.errors), "扫描详情"); errors.Items.Add(show); feedback.ContextMenu = errors;
        }
        internal static bool Same(string a, string b) { if (String.IsNullOrEmpty(a) || String.IsNullOrEmpty(b)) return false; try { return String.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); } catch { return false; } }
        List<XmlRegistration> References(string path) { return scan == null ? new List<XmlRegistration>() : scan.rows.Where(r => Same(r.uicPath, path)).ToList(); }
        public void SelectPath(string path) { focusPath = path; selected = files.FirstOrDefault(f => Same(f.Path, path)) ?? selected; uicList.SelectedItem = selected; fileGrid.SelectedItem = selected; RenderDetails(); }
        void BuildDeployment()
        {
            var body = new DockPanel(); content.Children.Add(body);
            var heading = new StackPanel(); heading.Children.Add(Ui.Text("UIC 文件", 20)); heading.Children.Add(Ui.Text("批量加入，按需部署；点击 XML 数量查看登记详情。", 12, Ui.Muted)); heading.Children.Add(search); DockPanel.SetDock(heading, Dock.Top); body.Children.Add(heading);
            var detail = new Expander { Header = "所选文件 · 位置、XML 登记与历史", Content = Ui.Scroll(details), MaxHeight = 220 }; DockPanel.SetDock(detail, Dock.Bottom); body.Children.Add(detail);
            fileGrid.IsReadOnly = false; foreach(var col in fileGrid.Columns) col.IsReadOnly = true;
            var checkbox = new FrameworkElementFactory(typeof(CheckBox)); checkbox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new System.Windows.Data.Binding("Marked") { Mode = System.Windows.Data.BindingMode.TwoWay, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged }); checkbox.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            fileGrid.Columns.Insert(0, new DataGridTemplateColumn { Header = "选", Width = 40, CellTemplate = new DataTemplate { VisualTree = checkbox } });
            fileGrid.Columns[1].Width = 165; fileGrid.Columns[2].Width = new DataGridLength(2, DataGridLengthUnitType.Star); fileGrid.Columns[3].Width = 115;
            fileGrid.SelectionChanged += (s,e) => { selected = fileGrid.SelectedItem as DeploymentFileRow; RenderDetails(); };
            fileGrid.PreviewMouseLeftButtonUp += async (s,e) => { if (fileGrid.CurrentCell.Column != null && Convert.ToString(fileGrid.CurrentCell.Column.Header) == "已登记 XML" && fileGrid.CurrentItem is DeploymentFileRow) await main.OpenRegistrationExplorer(((DeploymentFileRow)fileGrid.CurrentItem).Path); };
            body.Children.Add(fileGrid);
        }
        void BuildRegistration()
        {
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(288) }); content.ColumnDefinitions.Add(new ColumnDefinition());
            var left = new DockPanel { Margin = new Thickness(0, 0, 16, 0) }; var top = new StackPanel(); top.Children.Add(Ui.Text("UIC 文件", 16)); top.Children.Add(search); DockPanel.SetDock(top, Dock.Top); left.Children.Add(top); left.Children.Add(uicList); content.Children.Add(left);
            var template = new FrameworkElementFactory(typeof(StackPanel));
            var name = new FrameworkElementFactory(typeof(TextBlock)); name.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Name")); name.SetValue(TextBlock.FontSizeProperty,12.0); name.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis); template.AppendChild(name);
            var count = new FrameworkElementFactory(typeof(TextBlock)); count.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("XmlSummary")); count.SetValue(TextBlock.FontSizeProperty,10.0); count.SetValue(TextBlock.ForegroundProperty,Ui.Muted); count.SetValue(FrameworkElement.MarginProperty,new Thickness(0,6,0,0)); template.AppendChild(count);
            uicList.ItemTemplate = new DataTemplate { VisualTree = template }; uicList.SelectionChanged += (s,e) => { if (uicList.SelectedItem != null) { selected = uicList.SelectedItem as DeploymentFileRow; RenderDetails(); } };
            var right = Ui.Card(Ui.Scroll(details)); right.Margin = new Thickness(0); Grid.SetColumn(right, 1); content.Children.Add(right);
        }
        void RenderFiles()
        {
            string q = search.Text ?? ""; var filtered = files.Where(f => (f.Name + " " + f.Path).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            fileGrid.ItemsSource = filtered; uicList.ItemsSource = filtered;
        }
        void RenderDetails()
        {
            details.Children.Clear(); if (selected == null || scan == null) return;
            var row = selected; details.Children.Add(Ui.Text(row.Name, 18));
            var location = new DockPanel();
            var open = Ui.Button("打开编辑 ↗", async () => { if (!String.IsNullOrEmpty(row.Path)) await main.OpenUicLocation(row.Path); else await main.OpenDocument(row.Project); }); open.ToolTip="已打开的 UIC 直接跳转，保留编辑和撤销栈"; open.Height=32; open.MinHeight=0; open.VerticalAlignment=VerticalAlignment.Top; DockPanel.SetDock(open,Dock.Right); location.Children.Add(open);
            var path = Ui.Input(String.IsNullOrEmpty(row.Path) ? "尚未部署，请先在部署页生成文件" : row.Path); path.IsReadOnly = true; location.Children.Add(path); details.Children.Add(location);
            var refs = References(row.Path); int waiting = pending.Count(p => p.Project.documentId == row.Project.documentId);
            var summary = new DockPanel { Margin = new Thickness(0,6,0,8) };
            var add = registration ? Ui.Button("＋ 选择 XML", ChooseXml) : Ui.Button("查看 XML 登记 ↗", () => main.OpenRegistrationExplorer(row.Path)); add.Height=32;add.MinHeight=0;DockPanel.SetDock(add,Dock.Right);summary.Children.Add(add);
            summary.Children.Add(Ui.Text("已登记 " + refs.Select(r => r.xmlPath).Distinct(StringComparer.OrdinalIgnoreCase).Count() + " 个 XML" + (waiting > 0 ? " · 待新增 " + waiting : ""), 15)); details.Children.Add(summary);
            if(registration) { reviewButton.IsEnabled=pending.Count>0; reviewButton.Content=pending.Count>0?"预览登记变更 · "+pending.Count+" 项":"预览 XML 登记变更"; }
            var groups = refs.GroupBy(r => r.product + "\0" + r.version);
            foreach (var group in groups)
            {
                var first = group.First(); var header = new DockPanel();
                var badge = new StackPanel(); badge.Children.Add(Ui.Text(first.product, 11)); badge.Children.Add(Ui.Text(first.version, 9, Ui.Muted));
                var color = first.product == "E3D" ? Color.FromRgb(234,240,255) : first.product == "AM" ? Color.FromRgb(231,244,239) : first.product == "PDMS" ? Color.FromRgb(255,242,228) : Color.FromRgb(237,237,237);
                var badgeBox = new Border { Child = badge, Background = new SolidColorBrush(color), CornerRadius = new CornerRadius(4), Padding = new Thickness(10, 2, 10, 2), MinWidth = 72, ToolTip = first.versionSource }; DockPanel.SetDock(badgeBox,Dock.Left); header.Children.Add(badgeBox);
                var groupCount=Ui.Text("已登记 " + group.Select(r=>r.xmlPath).Distinct(StringComparer.OrdinalIgnoreCase).Count() + " 个 XML", 12, Ui.Muted); groupCount.HorizontalAlignment=HorizontalAlignment.Right;groupCount.VerticalAlignment=VerticalAlignment.Center;groupCount.Margin=new Thickness(20,0,8,0);header.Children.Add(groupCount);
                var list = Ui.Grid("XML 完整路径|xmlPath", "登记名称|entryName", "来源|origin"); list.ItemsSource = group.ToList(); list.Height = Math.Min(240, 38 * group.Count() + 36); list.ToolTip = "XML 路径来自实际安装；原生/未接管条目保持只读";
                var headerBox = new Border { Child=header,Background=new SolidColorBrush(Color.FromRgb(247,249,253)),BorderBrush=Ui.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5),Padding=new Thickness(10,4,10,4),HorizontalAlignment=HorizontalAlignment.Stretch };
                details.Children.Add(new Expander { Header = headerBox, Content = list, Margin = new Thickness(0, 4, 0, 4) });
            }
            foreach (var p in pending.Where(p => p.Project.documentId == row.Project.documentId).ToList()) { var r = new StackPanel { Orientation = Orientation.Horizontal }; r.Children.Add(Ui.Text("待新增 · " + p.Target.product + " " + p.Target.version + " / " + p.Manifest, 11)); r.Children.Add(Ui.Sync("取消", () => { pending.Remove(p); RenderDetails(); })); details.Children.Add(r); }
            if (scan.errors.Count > 0) details.Children.Add(Ui.Text("有 " + scan.errors.Count + " 项未读取；数量仅覆盖成功扫描的 XML。", 11, Ui.Muted));
            var history=Ui.Button("查看历史与恢复记录 ↗", () => main.ShowLibrary()); history.BorderThickness=new Thickness(0);history.Foreground=new SolidColorBrush(Color.FromRgb(49,89,232));details.Children.Add(history);
            foreach (var b in details.Children.OfType<Button>()) { b.HorizontalAlignment = HorizontalAlignment.Left; b.Height = 32; b.MinHeight = 0; b.Padding = new Thickness(12, 4, 12, 4); }
        }
        async Task AddUic() { foreach (var path in Ui.OpenMany("UIC / 项目|*.uic;*.json")) await main.OpenUicLocation(path); await main.OpenTool(registration); }
        async Task<Project> Latest(Project p) { var open = main.OpenProjects().FirstOrDefault(v => v.documentId == p.documentId); return open ?? await Api.Post<Project>("workspace/open", new { documentId = p.documentId }); }
        Task ChooseProfile()
        {
            var menu = new ContextMenu(); foreach (var p in profiles == null ? new List<DeploymentProfile>() : profiles.profiles) { var choice = p; var item = new MenuItem { Header = p.name + " · " + p.monPath, IsCheckable = true, IsChecked = profile != null && p.id == profile.id }; item.Click += async(s,e) => { profile = choice; await Reload(); }; menu.Items.Add(item); }
            menu.Items.Add(new Separator()); var create = new MenuItem { Header = "新增配置…" }; create.Click += async(s,e) => { profile = null; await ManageProfile(); }; menu.Items.Add(create); var manage = new MenuItem { Header = "修改当前配置…", IsEnabled = profile != null }; manage.Click += async(s,e) => await ManageProfile(); menu.Items.Add(manage); menu.PlacementTarget = profileButton; menu.IsOpen = true; return Task.FromResult(0);
        }
        async Task ManageProfile()
        {
            var dialog = Ui.Dialog("部署配置", main, 720, 620); var panel = new StackPanel { Margin = new Thickness(22) }; dialog.Content = Ui.Scroll(panel);
            var name = Ui.Input(profile == null ? "新配置" : profile.name); var mon = Ui.Input(profile == null ? "" : profile.monPath); panel.Children.Add(Ui.Text("配置名称")); panel.Children.Add(name); panel.Children.Add(Ui.Text("安装 mon.exe 完整路径")); panel.Children.Add(mon);
            panel.Children.Add(Ui.Sync("浏览 mon.exe", () => { var file = Ui.Open("AVEVA 主程序|mon.exe"); if (file != null) mon.Text = file; }));
            var modules = new StackPanel(); var checks = new List<CheckBox>();
            Func<Task> readModules = async () => { var target = await Api.Post<Target>("deployment/inspect", new { monPath = mon.Text.Trim() }); modules.Children.Clear(); checks.Clear(); foreach (var m in target.manifests) { var check = new CheckBox { Content = m, Tag = m, Margin = new Thickness(4), IsChecked = profile != null && Same(profile.monPath,target.monPath) && profile.manifests.Contains(m) }; checks.Add(check); modules.Children.Add(check); } };
            panel.Children.Add(Ui.Button("读取模块", readModules)); panel.Children.Add(modules); if (profile != null) await readModules();
            panel.Children.Add(Ui.Button("保存配置", async () => { profile = await Api.Post<DeploymentProfile>("registration/profile-save", new DeploymentProfile { id = profile == null ? null : profile.id, name = name.Text, monPath = mon.Text.Trim(), manifests = checks.Where(c => c.IsChecked == true).Select(c => Convert.ToString(c.Tag)).ToList() }); dialog.DialogResult = true; }, true)); Chrome.Install(dialog); dialog.ShowDialog(); await Reload();
        }
        async Task ChooseXml()
        {
            if (selected == null || String.IsNullOrEmpty(selected.Path)) { feedback.Text = "请先部署或选择有实际路径的 UIC"; return; }
            if (profile == null) { await ManageProfile(); if (profile == null) return; }
            var row = selected; var target = await Api.Post<Target>("deployment/inspect", new { monPath = profile.monPath });
            var dialog = Ui.Dialog("选择要登记的 XML", main, 720, 570); var panel = new StackPanel { Margin = new Thickness(22) }; dialog.Content = Ui.Scroll(panel); panel.Children.Add(Ui.Text(target.label, 13)); panel.Children.Add(Ui.Text(row.Path, 11, Ui.Muted));
            var entry = Ui.Input(Path.GetFileNameWithoutExtension(row.Name)); panel.Children.Add(Ui.Text("CustomizationFile Name")); panel.Children.Add(entry); var checks = new List<CheckBox>();
            foreach (var manifest in target.manifests) { bool exists = References(row.Path).Any(r => Same(r.xmlPath,Path.Combine(target.directory,manifest))) || pending.Any(p => p.Project.documentId == row.Project.documentId && Same(p.Target.monPath,target.monPath) && p.Manifest == manifest); var c = new CheckBox { Content = manifest + (exists ? " · 已登记 / 已选" : ""), Tag = manifest, IsChecked = exists, IsEnabled = !exists, Margin = new Thickness(4) }; checks.Add(c); panel.Children.Add(c); }
            panel.Children.Add(Ui.Sync("加入待登记列表", () => { foreach (var c in checks.Where(c => c.IsEnabled && c.IsChecked == true)) pending.Add(new PendingRegistration { Project = row.Project, Target = target, Manifest = Convert.ToString(c.Tag), Name = entry.Text }); dialog.DialogResult = true; RenderDetails(); })); Chrome.Install(dialog); dialog.ShowDialog();
        }
        async Task ReviewRegistration()
        {
            frozen.EnsureAvailable();
            if (pending.Count == 0) { feedback.Text = "先选择 UIC，再选择需要加入的 XML"; return; }
            await main.SaveOpenProjects(); int applied = 0;
            foreach (var p in pending.ToList())
            {
                try
                {
                    p.Project = await Latest(p.Project);
                    p.Frozen = await frozen.Preview("registration", new { project = p.Project, monPath = p.Target.monPath, manifestName = p.Manifest, entryName = p.Name });
                    p.Plan = p.Frozen.targetPlan;
                    var review = new StackPanel(); review.Children.Add(Ui.Text(p.Plan.manifestPath, 13));
                    review.Children.Add(Ui.Text("冻结计划 " + p.Frozen.planId + " · 源版本 " + p.Frozen.expectedVersionId + " · 有效期 " + p.Frozen.expiresAt, 11));
                    review.Children.Add(Ui.Text("本次仅确认这一项；剩余 " + (pending.Count - 1) + " 项将各自预览并确认。", 12));
                    review.Children.Add(new Expander { Header = "查看目标 XML", Content = Ui.Code(p.Plan.afterXml) });
                    if (!ConfirmReview("v2 冻结登记 · 核对本次 XML", review)) { feedback.Text = "已停止；先前完成 " + applied + " 项，其余尚未确认。"; return; }
                    await frozen.Apply("registration", p.Frozen); pending.Remove(p); applied++;
                }
                catch (Exception error) { p.Frozen = null; throw new Exception("已完成 " + applied + " 项；剩余项未继续。" + error.Message); }
            }
            await ReloadCore(); feedback.Text = "已登记 " + applied + " 个 XML；UIC 内容保持不变。" + FrozenDeploymentClient.ActivationMessage;
        }
        async Task ReviewDeployment()
        {
            frozen.EnsureAvailable();
            await main.SaveOpenProjects(); prepared.Clear(); var selectedRows = files.Where(f => f.Marked).ToList(); if (selectedRows.Count == 0) { feedback.Text = "请勾选需要部署的 UIC"; return; }
            foreach (var row in selectedRows)
            {
                var current = await Latest(row.Project); FrozenPlan envelope = null; Plan plan; var p = current;
                if (!String.IsNullOrEmpty(row.Path)) { envelope = await frozen.Preview("file", new { project = p }); plan = envelope.targetPlan; }
                else { p = await Api.Post<Project>("projects/normalize", current); if (profile == null || profile.manifests.Count == 0) throw new Exception("新文件首次部署需先选择包含首次模块的部署配置"); plan = await Api.Post<Plan>("deployment/preview", new { project = p, monPath = profile.monPath, manifestName = profile.manifests[0] }); }
                prepared.Add(new PreparedDeployment { Project = p, Plan = plan, Frozen = envelope, Fingerprint = Json.Write(current) });
            }
            var review = new StackPanel();
            foreach (var p in prepared) review.Children.Add(Ui.Text(p.Plan.uicPath + "\n" + (p.Plan.unchanged ? "内容相同" : "将备份并写入") + " · " + p.Plan.uicBytes + " bytes\n" + (p.Frozen == null ? "v1 兼容入口：新文件首次部署，不属于 v2 冻结计划" : "v2 冻结计划 " + p.Frozen.planId + " · 源版本 " + p.Frozen.expectedVersionId + " · 有效期 " + p.Frozen.expiresAt), 13));
            if (!ConfirmReview("核对 UIC 部署 · 已有文件 v2 / 首次生成 v1", review)) return;
            foreach (var p in prepared) if (Json.Write(await Latest(p.Project)) != p.Fingerprint) throw new Exception("编辑内容已变化，请重新预览");
            int count = 0;
            foreach (var p in prepared)
            {
                try
                {
                    frozen.EnsureAvailable();
                    if (p.Frozen != null) await frozen.Apply("file", p.Frozen);
                    else await frozen.ApplyLegacy(new { project = p.Project, monPath = p.Plan.monPath, manifestName = profile.manifests[0], expectedUicHash = p.Plan.uicHash, expectedManifestHash = p.Plan.manifestHash });
                    count++;
                }
                catch (Exception error) { prepared.Clear(); throw new Exception("已完成 " + count + " 项；其余未继续。" + error.Message); }
            }
            prepared.Clear(); await ReloadCore(); feedback.Text = "已完成 " + count + " 个 UIC；备份与历史已保留。" + FrozenDeploymentClient.ActivationMessage;
        }
        bool ConfirmReview(string title, Panel review)
        {
            var dialog = Ui.Dialog(title, main, 940, 620); var root = new DockPanel(); var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16) }; actions.Children.Add(Ui.Sync("取消", () => dialog.DialogResult = false)); actions.Children.Add(Ui.Sync("确认备份并写入", () => dialog.DialogResult = true)); DockPanel.SetDock(actions,Dock.Bottom); root.Children.Add(actions); review.Margin = new Thickness(20); root.Children.Add(Ui.Scroll(review)); dialog.Content = root; Chrome.Install(dialog); return dialog.ShowDialog() == true;
        }
        public async Task VerifyFrozenPreview(string snapshot)
        {
            await ReloadCore(); var row = files.FirstOrDefault(f => !String.IsNullOrEmpty(f.Path));
            if (row == null) throw new Exception("No existing file for native v2 preview verification");
            foreach (var file in files) file.Marked = file == row;
            bool observed = false; var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            timer.Tick += (s, e) => {
                var dialog = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "核对 UIC 部署 · 已有文件 v2 / 首次生成 v1");
                if (dialog == null) return;
                observed = prepared.Count == 1 && prepared[0].Frozen != null && prepared[0].Frozen.contractVersion == 2 && prepared[0].Frozen.kind == "file-update";
                timer.Stop(); dialog.UpdateLayout(); Ui.Capture(dialog, snapshot); dialog.DialogResult = false;
            };
            timer.Start(); try { await ReviewDeployment(); } finally { timer.Stop(); }
            if (!observed) throw new Exception("Native StudioToolPage did not display a v2 frozen preview");
        }
    }
}
