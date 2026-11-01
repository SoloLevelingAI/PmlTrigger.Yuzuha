using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
namespace RibbonStudio
{
    public partial class DeploymentWindow : Window
    {
        readonly DockPanel embeddedBody; readonly bool embedded; public bool IsWorking
        {
            get
            {
                return working;
            }
        }
        public string SourceDocumentId
        {
            get
            {
                return project.documentId;
            }
        }
        public UIElement TakeContent()
        {
            Content = null;
            return embeddedBody;
        }
        public void SwitchSection(bool modules)
        {
            tabs.SelectedIndex = modules ? 1 : 0;
        }
        public void RestrictSection(bool modules)
        {
            tabs.SelectedIndex = modules ? 1 : 0;
            var hidden = (TabItem)tabs.Items[modules ? 0 : 1];
            hidden.Visibility = Visibility.Collapsed;
        }
        readonly MainWindow main; readonly Project project; ApiClient Api
        {
            get
            {
                return main.Api;
            }
        }
        readonly TabControl tabs = new TabControl(); readonly ComboBox targets = Ui.Combo(), firstModule = Ui.Combo(); readonly TextBox mon = Ui.Input(), entryName = Ui.Input("MyRibbon"), leftSearch = Ui.Input(), rightSearch = Ui.Input();
        readonly StackPanel update = new StackPanel { Margin = new Thickness(18) }, moduleEditor = new StackPanel { Margin = new Thickness(18) }, review = new StackPanel { Margin = new Thickness(18) };
        readonly TextBlock feedback = Ui.Text("", 11, Ui.Muted), counts = Ui.Text("", 11, Ui.Muted), leftCount = Ui.Text("未关联", 13), rightCount = Ui.Text("已关联 / 已选", 13);
        readonly DataGrid left = ModuleGrid(), right = ModuleGrid(); readonly Button updateApply, modulesApply; Plan filePlan; List<Plan> plans = new List<Plan>(); List<ModuleRow> rows = new List<ModuleRow>(); string sourcePath, scannedMon; bool working, loadingTargets; Task initializeTask;
        readonly FrozenDeploymentClient frozen; FrozenPlan frozenFile;
        readonly Dictionary<string, FrozenPlan> frozenModules = new Dictionary<string, FrozenPlan>();
        public Task Ready()
        {
            return initializeTask ?? (initializeTask = Initialize());
        }
        public DeploymentWindow(MainWindow owner, Project value, bool modules, bool embed = false)
        {
            embedded = embed;
            main = owner;
            frozen = new FrozenDeploymentClient(Api);
            project = Json.Copy(value);
            Owner = owner;
            Title = "Ribbon Studio · 部署与模块关联";
            Width = 1080;
            Height = 800;
            MinWidth = 850;
            MinHeight = 580;
            FontFamily = owner.FontFamily;
            Background = owner.Background;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var root = new DockPanel { Margin = new Thickness(20) };
            embeddedBody = root;
            Content = root;
            var heading = Ui.Text("部署与模块关联", 21);
            DockPanel.SetDock(heading, Dock.Top);
            root.Children.Add(heading);
            var recover = Ui.Sync("人工核对后解除写入暂停", () => { if (FrozenDeploymentClient.ConfirmManualCheck(main)) feedback.Text = "写入暂停已解除，请重新预览。实际 Ribbon 加载未核验。"; }); DockPanel.SetDock(recover, Dock.Top); root.Children.Add(recover);
            DockPanel.SetDock(feedback, Dock.Bottom);
            root.Children.Add(feedback);
            root.Children.Add(tabs);
            updateApply = Ui.Button("备份并写入 UIC", () => Run(ApplyFile), true);
            modulesApply = Ui.Button("确认写入所列模块", () => Run(ApplyModules), true);
            updateApply.IsEnabled = false;
            modulesApply.IsEnabled = false;
            tabs.Items.Add(new TabItem { Header = "更新 / 部署 UIC", Content = Ui.Scroll(update) });
            var moduleRoot = new Grid();
            moduleRoot.Children.Add(Ui.Scroll(moduleEditor));
            moduleRoot.Children.Add(Ui.Scroll(review));
            review.Visibility = Visibility.Collapsed;
            tabs.Items.Add(new TabItem { Header = "关联模块 · 穿梭框", Content = moduleRoot });
            tabs.SelectedIndex = modules ? 1 : 0;
            BuildModules();
            if (!embedded)
                Chrome.Install(this);
            InstallMenus();
            Loaded += async (s, e) => { try { await Ready(); } catch (Exception error) { feedback.Text = error.Message; } };
            Closing += (s, e) => { if (working) e.Cancel = true; };
            mon.TextChanged += (s, e) => { if (!loadingTargets) { filePlan = null; updateApply.IsEnabled = false; rows.Clear(); plans.Clear(); Filter(); } };
            entryName.TextChanged += (s, e) => InvalidateModules();
            targets.SelectionChanged += async (s, e) => { var target = targets.SelectedItem as Target; if (target == null || loadingTargets) return; try { loadingTargets = true; mon.Text = target.monPath; loadingTargets = false; await Run(Inspect); } catch (Exception error) { feedback.Text = error.Message; } };
        }
        async Task Run(Func<Task> action)
        {
            if (working)
                return;
            working = true;
            if (embedded)
                embeddedBody.IsEnabled = false;
            else
                Chrome.Busy(this, true);
            try
            {
                await action();
            }
            catch (Exception error) { plans.Clear(); modulesApply.IsEnabled = false; feedback.Text = error.Message; }
            finally { working = false; if (embedded) embeddedBody.IsEnabled = true; else Chrome.Busy(this, false); }
        }
        async Task Initialize()
        {
            if (!String.IsNullOrEmpty(project.documentId))
            {
                var doc = await Api.Post<Document>("workspace/context", new
                {
                    documentId = project.documentId
                });
                sourcePath = doc.sourcePath;
                if (project.preferredBinding == null && doc.bindings != null && doc.bindings.Count > 0)
                    project.preferredBinding = doc.bindings[0];
            }
            var available = (await Api.Get<Targets>("api/v1/deployment/targets")).targets;
            loadingTargets = true;
            targets.ItemsSource = available;
            if (project.preferredBinding != null)
            {
                var preferred = available.FirstOrDefault(t => String.Equals(t.monPath, project.preferredBinding.monPath, StringComparison.OrdinalIgnoreCase));
                targets.SelectedItem = preferred;
                mon.Text = project.preferredBinding.monPath;
            }
            loadingTargets = false;
            BuildUpdate();
            if (!String.IsNullOrEmpty(mon.Text))
                await Run(Inspect);
            if (tabs.SelectedIndex == 0 && !String.IsNullOrEmpty(sourcePath))
                await Run(PreviewFile);
            else if (tabs.SelectedIndex == 1 && String.IsNullOrEmpty(sourcePath))
                feedback.Text = "请先导入本机 UIC，或完成新项目首次部署后，再加入其他模块。";
            if (frozen.Locked) feedback.Text = frozen.Status;
        }
        void BuildUpdate()
        {
            update.Children.Clear();
            update.Children.Add(Ui.Text(String.IsNullOrEmpty(sourcePath) ? "新项目首次部署" : "备份并更新共享 UIC", 18));
            update.Children.Add(Ui.Text(String.IsNullOrEmpty(sourcePath) ? "选择安装与首次加载模块。后端将生成 UIC 文件，备份清单并登记加载。" : "原文件：" + sourcePath + "\n覆盖一次，共用该文件的全部模块重新加载后使用新内容；已有 XML 登记保持不变。", 12, Ui.Muted));
            if (String.IsNullOrEmpty(sourcePath))
            {
                var choice = Ui.Button("选择安装与首次模块", () => Run(async () => { var input = Ui.Prompt("安装路径", "请输入 mon.exe 完整路径", mon.Text, embedded ? (Window)main : this); if (input == null) return; mon.Text = input; var target = await Api.Post<Target>("deployment/inspect", new { monPath = mon.Text.Trim() }); mon.Text = target.monPath; firstModule.ItemsSource = target.manifests; firstModule.SelectedIndex = 0; }));
                update.Children.Add(choice);
                update.Children.Add(firstModule);
                firstModule.SelectionChanged += (s, e) => { filePlan = null; updateApply.IsEnabled = false; };
            }
            update.Children.Add(Ui.Button("预览 UIC 更新", () => Run(PreviewFile)));
            update.Children.Add(updateApply);
        }
        async Task Inspect()
        {
            var target = await Api.Post<Target>("deployment/inspect", new
            {
                monPath = mon.Text.Trim()
            });
            loadingTargets = true;
            mon.Text = target.monPath;
            loadingTargets = false;
            firstModule.ItemsSource = target.manifests;
            firstModule.SelectedIndex = 0;
            if (!String.IsNullOrEmpty(sourcePath))
            {
                var result = await Api.Post<Modules>("deployment/modules", new
                {
                    project,
                    monPath = target.monPath
                });
                rows = result.modules;
                scannedMon = result.monPath;
                var existing = rows.FirstOrDefault(r => r.Existing);
                if (existing != null)
                    entryName.Text = existing.entryNames[0];
                Filter();
                feedback.Text = "已按实际 XML 收集模块；左右移动只调整本次待加入项。";
            }
        }
        async Task PreviewFile()
        {
            frozen.EnsureAvailable(); frozenFile = null;
            filePlan = null;
            updateApply.IsEnabled = false;
            feedback.Text = "正在生成并收集关联…";
            var existing = !String.IsNullOrEmpty(sourcePath);
            if (existing) frozenFile = await frozen.Preview("file", new { project });
            var plan = existing ? frozenFile.targetPlan : await Api.Post<Plan>("deployment/preview", new
            {
                project,
                monPath = mon.Text.Trim(),
                manifestName = Convert.ToString(firstModule.SelectedItem)
            });
            BuildUpdate();
            var content = new StackPanel();
            content.Children.Add(Ui.Text(plan.unchanged ? "文件已是当前版本" : "确认前检查实际目标", 15));
            content.Children.Add(Ui.Text(existing ? "v2 冻结计划 " + frozenFile.planId + " · 源版本 " + frozenFile.expectedVersionId + " · 有效期 " + frozenFile.expiresAt : "v1 兼容入口：新文件首次部署，不属于 v2 冻结计划", 11));
            content.Children.Add(Ui.Text("UIC：" + plan.uicPath + "\n生成大小：" + (plan.uicBytes / 1024.0).ToString("0.0") + " KiB · 图标 32×32 / 16×16", 12));
            if (plan.references != null)
            {
                content.Children.Add(Ui.Text("关联模块（" + plan.references.Count + "）", 14));
                var filter = Ui.Input();
                filter.ToolTip = "筛选模块、安装或名称";
                var list = Ui.Grid("模块|manifestName", "名称|entryName", "安装|monPath");
                list.Height = 190;
                list.ItemsSource = plan.references;
                filter.TextChanged += (s, e) => { var q = filter.Text.ToLower(); list.ItemsSource = plan.references.Where(r => (r.manifestName + " " + r.entryName + " " + r.monPath).ToLower().Contains(q)).ToList(); };
                content.Children.Add(filter);
                content.Children.Add(list);
                if (plan.scanErrors != null && plan.scanErrors.Count > 0)
                    content.Children.Add(Ui.Text("未读取：" + String.Join("\n", plan.scanErrors), 11));
            }
            if (!existing)
                content.Children.Add(Ui.Code(plan.registration));
            var xml = await Api.Text("uic/export", project);
            var details = new Expander { Header = "查看本次生成的 UIC XML", Content = Ui.Code(xml), Margin = new Thickness(0, 12, 0, 12) };
            content.Children.Add(details);
            update.Children.Add(Ui.Card(content));
            filePlan = plan;
            updateApply.IsEnabled = true;
            feedback.Text = "只读预览已就绪；点击备份并写入后才修改实际文件。";
        }
        async Task ApplyFile()
        {
            frozen.EnsureAvailable();
            if (filePlan == null)
                throw new Exception("请先预览");
            var plan = filePlan;
            filePlan = null;
            updateApply.IsEnabled = false;
            Plan result;
            if (!String.IsNullOrEmpty(sourcePath))
            {
                var envelope = frozenFile; frozenFile = null;
                var receipt = await frozen.Apply("file", envelope);
                feedback.Text = (receipt.state == "unchanged" ? "磁盘内容相同。" : "UIC 已部署到磁盘。") + FrozenDeploymentClient.ActivationMessage;
                return;
            }
            else
                result = await frozen.ApplyLegacy(new
                {
                    project,
                    monPath = plan.monPath,
                    manifestName = Convert.ToString(firstModule.SelectedItem),
                    expectedManifestHash = plan.manifestHash,
                    expectedUicHash = plan.uicHash
                });
            feedback.Text = (result.unchanged ? "磁盘内容相同。" : "首次部署已写入磁盘。备份：" + result.backupDirectory + "。") + FrozenDeploymentClient.ActivationMessage;
            var doc = await Api.Post<Document>("workspace/context", new
            {
                documentId = project.documentId
            });
            sourcePath = doc.sourcePath;
        }
        static DataGrid ModuleGrid()
        {
            var grid = Ui.Grid("模块 XML|manifestName", "状态|State");
            grid.Height = 245;
            grid.IsReadOnly = false;
            grid.SelectionMode = DataGridSelectionMode.Extended;
            var checkbox = new FrameworkElementFactory(typeof(CheckBox));
            checkbox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new System.Windows.Data.Binding("Marked") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            checkbox.SetBinding(UIElement.IsEnabledProperty, new System.Windows.Data.Binding("Editable"));
            checkbox.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkbox.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            grid.Columns.Insert(0, new DataGridTemplateColumn { Header = "勾选", CellTemplate = new DataTemplate { VisualTree = checkbox }, Width = 50 });
            foreach (var column in grid.Columns.Skip(1))
                column.IsReadOnly = true;
            var style = new Style(typeof(DataGridRow), (Style)Application.Current.FindResource(typeof(DataGridRow)));
            var trigger = new DataTrigger { Binding = new System.Windows.Data.Binding("Editable"), Value = false };
            trigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.65));
            style.Triggers.Add(trigger);
            grid.RowStyle = style;
            return grid;
        }
        void BuildModules()
        {
            moduleEditor.Children.Add(Ui.Text("未关联 ⇄ 已关联 / 本次待加入", 18));
            moduleEditor.Children.Add(Ui.Text("已有加载固定在右侧；左移只取消本次待加入选择。真实移除加载在管理窗口操作。", 11, Ui.Muted));
            moduleEditor.Children.Add(targets);
            moduleEditor.Children.Add(mon);
            moduleEditor.Children.Add(Ui.Button("检测并收集模块", () => Run(Inspect)));
            var transfer = new Grid();
            transfer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            transfer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });
            transfer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            moduleEditor.Children.Add(transfer);
            var l = new StackPanel();
            l.Children.Add(leftCount);
            l.Children.Add(leftSearch);
            l.Children.Add(Ui.Sync("全选左侧筛选结果", () => Mark(left)));
            l.Children.Add(left);
            transfer.Children.Add(l);
            var arrows = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            arrows.Children.Add(Ui.Sync("→", () => Move(true)));
            arrows.Children.Add(Ui.Sync("←", () => Move(false)));
            Grid.SetColumn(arrows, 1);
            transfer.Children.Add(arrows);
            var r = new StackPanel();
            r.Children.Add(rightCount);
            r.Children.Add(rightSearch);
            r.Children.Add(Ui.Sync("全选待加入", () => Mark(right)));
            r.Children.Add(right);
            Grid.SetColumn(r, 2);
            transfer.Children.Add(r);
            leftSearch.ToolTip = "搜索未关联";
            rightSearch.ToolTip = "搜索已关联 / 待加入";
            leftSearch.TextChanged += (s, e) => Filter();
            rightSearch.TextChanged += (s, e) => Filter();
            moduleEditor.Children.Add(counts);
            moduleEditor.Children.Add(Ui.Text("新增加载名称", 11, Ui.Muted));
            moduleEditor.Children.Add(entryName);
            moduleEditor.Children.Add(Ui.Button("预览全部待加入模块", () => Run(PreviewModules)));
        }
        void InvalidateModules()
        {
            plans.Clear();
            modulesApply.IsEnabled = false;
            review.Visibility = Visibility.Collapsed;
            moduleEditor.Visibility = Visibility.Visible;
        }
        void Filter()
        {
            if (left == null)
                return;
            var a = leftSearch.Text.ToLower();
            var b = rightSearch.Text.ToLower();
            left.ItemsSource = rows.Where(r => !r.Existing && !r.Selected && r.manifestName.ToLower().Contains(a)).ToList();
            right.ItemsSource = rows.Where(r => (r.Existing || r.Selected) && (r.manifestName + " " + String.Join(" ", r.entryNames ?? new List<string>())).ToLower().Contains(b)).OrderBy(r => r.Existing).ToList();
            leftCount.Text = "未关联模块 · " + rows.Count(r => !r.Existing && !r.Selected);
            rightCount.Text = "已关联 / 已选 · " + rows.Count(r => r.Existing || r.Selected);
            counts.Text = "已关联 " + rows.Count(r => r.Existing) + " 个 · 本次待加入 " + rows.Count(r => r.Selected && !r.Existing) + " 个";
        }
        void Mark(DataGrid grid)
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
            var list = grid.ItemsSource as List<ModuleRow>;
            if (list == null)
                return;
            bool mark = list.Any(r => r.Editable && !r.Marked);
            foreach (var row in list.Where(r => r.Editable))
                row.Marked = mark;
            grid.Items.Refresh();
        }
        void Move(bool toRight)
        {
            left.CommitEdit(DataGridEditingUnit.Cell, true);
            left.CommitEdit(DataGridEditingUnit.Row, true);
            right.CommitEdit(DataGridEditingUnit.Cell, true);
            right.CommitEdit(DataGridEditingUnit.Row, true);
            var source = (toRight ? left : right).ItemsSource as List<ModuleRow>;
            if (source == null)
                return;
            foreach (var row in source.Where(r => r.Editable && r.Marked))
            {
                row.Selected = toRight;
                row.Marked = false;
            }
            InvalidateModules();
            Filter();
        }
        async Task PreviewModules()
        {
            frozen.EnsureAvailable(); frozenModules.Clear();
            InvalidateModules();
            if (String.IsNullOrEmpty(sourcePath))
                throw new Exception("请先导入本机文件或完成首次部署");
            if (scannedMon != mon.Text.Trim())
                throw new Exception("路径已改变，请重新收集模块");
            var selected = rows.Where(r => r.Selected && !r.Existing && r.Editable).ToList();
            if (selected.Count == 0)
                throw new Exception("请将需要加入的模块移到右侧");
            var next = new List<Plan>();
            foreach (var row in selected.Take(1))
            {
                feedback.Text = "正在预览 " + row.manifestName;
                var envelope = await frozen.Preview("registration", new
                {
                    project,
                    monPath = scannedMon,
                    manifestName = row.manifestName,
                    entryName = entryName.Text.Trim()
                });
                var plan = envelope.targetPlan; frozenModules[row.manifestName] = envelope;
                plan.monPath = scannedMon;
                plan.manifestName = row.manifestName;
                next.Add(plan);
            }
            plans = next;
            RenderReview();
        }
        void RenderReview()
        {
            review.Children.Clear();
            review.Children.Add(Ui.Text("加载清单变更预览", 20));
            review.Children.Add(Ui.Text("预览 · 尚未写入   |   " + plans.Count + " 份 XML   |   +" + plans.Sum(p => p.addedEntries.Count) + " 条加载登记   |   UIC 内容保持不变", 12, Ui.Muted));
            foreach (var plan in plans)
            {
                var body = new StackPanel();
                body.Children.Add(Ui.Text(plan.manifestName, 15));
                body.Children.Add(Ui.Text(plan.manifestPath, 11, Ui.Muted));
                var compare = new Grid();
                compare.ColumnDefinitions.Add(new ColumnDefinition());
                compare.ColumnDefinitions.Add(new ColumnDefinition());
                var before = new StackPanel { Margin = new Thickness(8) };
                before.Children.Add(Ui.Text("修改前 · " + plan.beforeEntries.Count + " 条登记", 13));
                before.Children.Add(Ui.Text("此模块尚未登记这个 UIC\n原有加载项保留", 12, Ui.Muted));
                compare.Children.Add(before);
                var after = new StackPanel { Margin = new Thickness(8), Background = new SolidColorBrush(Color.FromRgb(239, 248, 241)) };
                after.Children.Add(Ui.Text("修改后 · " + plan.afterEntries.Count + " 条登记", 13));
                foreach (var entry in plan.addedEntries)
                {
                    after.Children.Add(Ui.Text("＋ Name：" + entry.name + "\nPath：" + entry.path, 12));
                }
                Grid.SetColumn(after, 1);
                compare.Children.Add(after);
                body.Children.Add(compare);
                var full = new Grid();
                full.ColumnDefinitions.Add(new ColumnDefinition());
                full.ColumnDefinitions.Add(new ColumnDefinition());
                var a = Ui.Code(plan.beforeXml);
                var b = Ui.Code(plan.afterXml);
                Grid.SetColumn(b, 1);
                full.Children.Add(a);
                full.Children.Add(b);
                body.Children.Add(new Expander { Header = "完整 XML · 修改前 / 修改后", Content = full, Margin = new Thickness(6, 12, 6, 6) });
                review.Children.Add(Ui.Card(body));
            }
            var actions = new WrapPanel();
            actions.Children.Add(Ui.Sync("返回修改选择", () => { InvalidateModules(); feedback.Text = "返回选择；没有写入实际 XML。"; }));
            modulesApply.Content = "确认写入 " + plans.Count + " 个模块清单";
            modulesApply.IsEnabled = true;
            if (modulesApply.Parent is Panel)
                ((Panel)modulesApply.Parent).Children.Remove(modulesApply);
            actions.Children.Add(modulesApply);
            review.Children.Add(actions);
            moduleEditor.Visibility = Visibility.Collapsed;
            review.Visibility = Visibility.Visible;
            feedback.Text = "本次只确认一个模块，其余保留选择并需另行预览。冻结计划 " + frozenModules.Values.First().planId + " · 有效期 " + frozenModules.Values.First().expiresAt;
        }
        async Task ApplyModules()
        {
            frozen.EnsureAvailable();
            if (plans.Count == 0)
                throw new Exception("请先预览");
            var batch = plans.ToList();
            plans.Clear();
            modulesApply.IsEnabled = false;
            int completed = 0;
            try
            {
                foreach (var plan in batch)
                {
                    feedback.Text = "正在加入 " + plan.manifestName;
                    await frozen.Apply("registration", frozenModules[plan.manifestName]);
                    var row = rows.First(r => r.manifestName == plan.manifestName);
                    row.entryNames = new List<string> { plan.entryName };
                    row.Selected = false;
                    row.Marked = false;
                    completed++;
                }
                feedback.Text = "已登记 " + completed + " 个模块；其余选择须重新预览。" + FrozenDeploymentClient.ActivationMessage;
            }
            catch (Exception error) { throw new Exception("已成功加入 " + completed + " 个，剩余保留待加入。" + error.Message); }
            finally { InvalidateModules(); Filter(); }
        }
        public async Task VerifyReadOnly(string monPath, string snapshot)
        {
            await Ready();
            tabs.SelectedIndex = 1;
            loadingTargets = true;
            mon.Text = monPath;
            loadingTargets = false;
            await Inspect();
            var candidate = rows.FirstOrDefault(r => r.Editable && !r.Existing);
            if (candidate == null)
                throw new Exception("No available module for preview test");
            candidate.Marked = true;
            Filter();
            Move(true);
            if (!candidate.Selected)
                throw new Exception("Transfer right failed");
            candidate.Marked = true;
            Move(false);
            if (candidate.Selected)
                throw new Exception("Transfer left failed");
            candidate.Marked = true;
            Move(true);
            Ui.Capture(this, snapshot.Replace(".png", "-transfer.png"));
            await PreviewModules();
            if (plans.Count != 1 || plans[0].addedEntries.Count != 1)
                throw new Exception("Native preview content missing");
            if (frozenModules.Count != 1 || frozenModules.Values.First().contractVersion != 2 || frozenModules.Values.First().kind != "registration-add")
                throw new Exception("Native module preview did not use frozen v2");
            InvalidateModules();
            await PreviewModules();
            Ui.Capture(this, snapshot);
        }
    }
}
