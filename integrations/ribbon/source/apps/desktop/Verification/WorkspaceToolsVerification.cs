using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RibbonStudio
{
    public partial class MainWindow
    {
        static bool WorkspaceSamePath(string left, string right)
        {
            return String.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        static string WorkspaceXmlEntry(string name, string path)
        {
            return "<CustomizationFile Name=\"" + System.Security.SecurityElement.Escape(name) + "\" Path=\"" + System.Security.SecurityElement.Escape(path) + "\"/>";
        }
        static void WorkspaceManifest(string path, params string[] entries)
        {
            File.WriteAllText(path, "<UICustomizationSet><UICustomizationFiles>" + String.Join("", entries) + "</UICustomizationFiles></UICustomizationSet>");
        }
        async Task WorkspaceIdle()
        {
            await Dispatcher.InvokeAsync(new Action(() => UpdateLayout()), DispatcherPriority.ApplicationIdle);
        }
        async Task WorkspaceClickLocator(FunctionLocatorPage page, FunctionLocatorRow row, string evidence)
        {
            await WorkspaceIdle();
            var grid = CommandVisuals<DataGrid>(page).FirstOrDefault(g => g.Name == "WorkspaceFunctionLocatorGrid");
            if (grid == null)
            {
                var ancestors = new List<object>();
                for (DependencyObject node = page; node != null; node = VisualTreeHelper.GetParent(node))
                {
                    var element = node as FrameworkElement;
                    if (element != null) ancestors.Add(new { type = element.GetType().Name, name = element.Name, visibility = element.Visibility.ToString(), isVisible = element.IsVisible, isLoaded = element.IsLoaded, width = element.ActualWidth, height = element.ActualHeight });
                }
                string diagnostics = Json.Write(new {
                    activeViewKind = activeView == null ? null : activeView.Kind,
                    selectedWorkspaceItem = Convert.ToString(workPages.SelectedItem),
                    activeTabSelected = activeView != null && activeView.Tab.IsSelected,
                    pageIsVisible = page.IsVisible,
                    pageIsLoaded = page.IsLoaded,
                    pageIsWorking = page.IsWorking,
                    pageContent = page.Content == null ? null : page.Content.GetType().Name,
                    grids = CommandVisuals<DataGrid>(page).Select(g => new { name = g.Name, isVisible = g.IsVisible, isLoaded = g.IsLoaded }).ToArray(),
                    buttonId = row.ButtonId,
                    source = row.Path,
                    ancestors = ancestors
                });
                File.WriteAllText(Path.ChangeExtension(evidence, "locator-visibility.json"), diagnostics);
                Ui.Capture(this, Path.ChangeExtension(evidence, "locator-visibility.png"));
                LayoutCheck(false, "function locator native grid absent after layout: " + diagnostics);
            }
            grid.SelectedItem = row;
            grid.ScrollIntoView(row);
            CommandVisuals<Button>(page).First(b => Convert.ToString(b.Content) == "定位到 Ribbon").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WorkspaceIdle();
            var waiting = System.Diagnostics.Stopwatch.StartNew();
            while (page.IsWorking && waiting.ElapsedMilliseconds < 10000)
            {
                await Task.Delay(20);
                await WorkspaceIdle();
            }
            LayoutCheck(!page.IsWorking, "function locator native click completed");
        }

        static object WorkspaceBoundsData(Rect bounds)
        {
            return new { empty = bounds.IsEmpty, x = bounds.IsEmpty ? 0 : bounds.X, y = bounds.IsEmpty ? 0 : bounds.Y, width = bounds.IsEmpty ? 0 : bounds.Width, height = bounds.IsEmpty ? 0 : bounds.Height };
        }
        bool WorkspaceBoundsFit(Rect bounds)
        {
            return !bounds.IsEmpty && bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= ActualWidth + 1 && bounds.Bottom <= ActualHeight + 1;
        }
        object VerifyWorkspaceGridGeometry(DataGrid grid, string view, double width, string evidence)
        {
            var tableBounds = new Rect(grid.TranslatePoint(new Point(), this), grid.RenderSize);
            ScrollViewer outerScroll = null;
            for (var node = VisualTreeHelper.GetParent(grid); node != null && node != this; node = VisualTreeHelper.GetParent(node))
            {
                outerScroll = node as ScrollViewer;
                if (outerScroll != null) break;
            }
            var viewportBounds = tableBounds;
            string viewportKind = "DataGrid";
            if (outerScroll != null)
            {
                // Inspect the enclosing scroll viewport, not the DataGrid's own
                // template scroll content. The table may be below that viewport.
                var presenter = CommandVisuals<ScrollContentPresenter>(outerScroll).FirstOrDefault(p => p.TemplatedParent == outerScroll);
                FrameworkElement viewport = presenter == null ? (FrameworkElement)outerScroll : presenter;
                viewportBounds = new Rect(viewport.TranslatePoint(new Point(), this), viewport.RenderSize);
                viewportKind = viewport.GetType().Name;
            }
            Rect visible = Rect.Intersect(tableBounds, viewportBounds);
            visible.Intersect(new Rect(0, 0, ActualWidth, ActualHeight));
            bool fits = WorkspaceBoundsFit(viewportBounds);
            var record = new {
                kind = "grid-viewport",
                name = grid.Name,
                columns = grid.Columns.Select(c => Convert.ToString(c.Header)).ToArray(),
                enclosingScroll = outerScroll != null,
                scrollName = outerScroll == null ? null : outerScroll.Name,
                viewportKind = viewportKind,
                tableUnclipped = WorkspaceBoundsData(tableBounds),
                viewport = WorkspaceBoundsData(viewportBounds),
                visibleIntersection = WorkspaceBoundsData(visible),
                tableFitsWindow = WorkspaceBoundsFit(tableBounds),
                viewportFitsWindow = fits,
                verticalOffset = outerScroll == null ? 0 : outerScroll.VerticalOffset,
                horizontalOffset = outerScroll == null ? 0 : outerScroll.HorizontalOffset
            };
            if (!fits)
            {
                string diagnostics = Json.Write(record);
                string prefix = Path.Combine(Path.GetDirectoryName(evidence), Path.GetFileNameWithoutExtension(evidence) + "-" + view + "-" + ((int)width) + "-grid-overflow");
                File.WriteAllText(prefix + ".json", diagnostics);
                Ui.Capture(this, prefix + ".png");
                LayoutCheck(false, "global tool grid viewport " + view + "/" + width + ": " + diagnostics);
            }
            return record;
        }

        async Task<List<object>> CaptureGlobalWorkspaceViews(string evidence, WorkspaceView document, WorkspaceView locator, WorkspaceView explorer)
        {
            double beforeWidth = Width;
            var samples = new List<object>();
            try
            {
                foreach (double width in new[] { 1280.0, 1050.0, 1440.0 })
                {
                    Width = width;
                    foreach (var view in new[] { document, locator, explorer })
                    {
                        if (view.Kind == "locator") await OpenFunctionLocator(); else SelectWorkspaceView(view);
                        if (view.Kind == "document") SwitchCommandView("layout");
                        await WorkspaceIdle();
                        LayoutCheck(Math.Abs(ActualWidth - width) < 2, "global tool actual viewport " + width);
                        var geometry = new List<object>();
                        var toolbar = CommandVisuals<Button>(documentCommands).Where(b => b.IsVisible && b.ActualWidth > 0).ToList();
                        foreach (var row in toolbar.GroupBy(b => Math.Round(b.TranslatePoint(new Point(), this).Y) + ":" + b.ActualHeight))
                        {
                            double top = row.First().TranslatePoint(new Point(), this).Y, height = row.First().ActualHeight;
                            foreach (var button in row)
                            {
                                var point = button.TranslatePoint(new Point(), this);
                                LayoutCheck(Math.Abs(point.Y - top) <= 1 && Math.Abs(button.ActualHeight - height) <= 1, "global toolbar row alignment " + view.Kind + "/" + width);
                                LayoutCheck(point.X >= -1 && point.X + button.ActualWidth <= ActualWidth + 1, "global toolbar command stays in viewport " + view.Kind + "/" + width);
                                geometry.Add(new { kind = "global-command", label = Convert.ToString(button.Content), x = point.X, y = point.Y, width = button.ActualWidth, height = button.ActualHeight, topDelta = point.Y - top, heightDelta = button.ActualHeight - height });
                            }
                        }
                        foreach (var grid in CommandVisuals<DataGrid>(this).Where(g => g.IsVisible && g.ActualWidth > 0))
                        {
                            geometry.Add(VerifyWorkspaceGridGeometry(grid, view.Kind, width, evidence));
                        }
                        string name = view.Kind == "document" ? "ribbon" : view.Kind;
                        string path = Path.Combine(Path.GetDirectoryName(evidence), Path.GetFileNameWithoutExtension(evidence) + "-" + name + "-" + ((int)width) + ".png");
                        Ui.Capture(this, path);
                        samples.Add(new { view = name, actualWidth = ActualWidth, actualHeight = ActualHeight, screenshot = path, geometry = geometry });
                    }
                }
            }
            finally { Width = beforeWidth; SelectWorkspaceView(document); SwitchCommandView("layout"); UpdateLayout(); }
            return samples;
        }

        public async Task VerifyWorkspaceTools(string evidence)
        {
            ShowCurrentDocument();
            SwitchCommandView("layout");
            var first = activeView;
            var backup = Json.Copy(Project);
            var originalViews = workspaceViews.ToList();
            WorkspaceView second = null;
            try
            {
                string fixture = Path.Combine(Path.GetDirectoryName(evidence), "workspace-tools-fixture");
                Directory.CreateDirectory(fixture);
                var firstProject = Project;
                var firstButton = Project.buttons.First(b => CommandReferenceLocations(b.id).Count > 0);
                const string caption = "跨文件同名功能定位夹具";
                ApplyLibraryFields(new[] { firstButton }, new Dictionary<string, string> { { "caption", caption } });
                string firstPath = firstProject.origin.sourcePath;
                string secondPath = Path.Combine(fixture, "SecondWorkspace.uic");
                var copy = Json.Copy(Project); copy.documentId = null; copy.preferredBinding = null;
                copy.origin.sourcePath = secondPath; copy.origin.fileName = Path.GetFileName(secondPath);
                File.WriteAllText(secondPath, await Api.Text("uic/export", copy));
                int documentsBefore = workspaceViews.Count(v => v.Kind == "document");
                await OpenDocument(copy);
                second = activeView;
                LayoutCheck(CurrentCommandView == "layout" && second.Kind == "document" && designLayout.Visibility == Visibility.Visible, "new UIC defaults to actual Ribbon layout editor");
                LayoutCheck(commandLibraryHost.Visibility == Visibility.Collapsed && commandReviewHost.Visibility == Visibility.Collapsed, "document defaults exclude internal library and review compatibility views");
                LayoutCheck(workspaceViews.Count(v => v.Kind == "document") == documentsBefore + 1 && workspaceViews.Count(v => v.Kind == "document" && v.Document.Project == copy) == 1, "one editor document tab per opened UIC");
                LayoutCheck(!CommandVisuals<Button>(designHost).Any(b => b.IsVisible && new[] { "功能库", "Ribbon 编排", "检查与部署" }.Contains(Convert.ToString(b.Content))), "removed per-document three-view navigation is not visible");
                var secondButton = Project.buttons.First(b => b.id == firstButton.id);
                var targetTab = Project.tabs.First(t => t.groups.Count > 0);
                PlaceCommandReferences(new[] { secondButton.id }, targetTab, targetTab.groups.First(), "small");
                var secondReference = CommandReferenceLocations(secondButton.id).Last();

                await OpenFunctionLocator();
                var locatorView = activeView;
                var locator = locatorView.Controller as FunctionLocatorPage;
                LayoutCheck(locator != null && locatorView.Kind == "locator" && locatorView.Document == null, "function locator is a workspace-level tool controller");
                int viewCount = workspaceViews.Count;
                await OpenFunctionLocator();
                LayoutCheck(activeView == locatorView && Object.ReferenceEquals(activeView.Controller, locator) && workspaceViews.Count == viewCount && workspaceViews.Count(v => v.Kind == "locator") == 1, "reopening locator reuses exact global tab and controller");
                locator.SetSearch(caption);
                var rows = locator.Rows().Where(r => r.ButtonId == firstButton.id && (r.Project == firstProject || r.Project == copy)).ToList();
                LayoutCheck(rows.Any(r => r.Project == firstProject) && rows.Any(r => r.Project == copy) && rows.Select(r => r.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2, "same button ID and caption remain separate across UIC sources");
                LayoutCheck(locator.SourceCount >= 2 && locator.ResultCount >= rows.Count, "locator search aggregates multiple UIC sources");
                var firstRow = rows.First(r => r.Project == firstProject && r.ReferenceIndex >= 0);
                await WorkspaceClickLocator(locator, firstRow, evidence);
                LayoutCheck(activeView == first && Project == firstProject && Selected.Item == firstRow.Reference && CurrentCommandView == "layout", "native locator click returns to exact first document and reference");
                await OpenFunctionLocator();
                locator.SetSearch(caption);
                var secondRow = locator.Rows().First(r => r.Project == copy && Object.ReferenceEquals(r.Reference, secondReference.Item));
                await WorkspaceClickLocator(locator, secondRow, evidence);
                LayoutCheck(activeView == second && Project == copy && Selected.Item == secondReference.Item && CurrentCommandView == "layout", "same-ID locator click returns to exact second document and reference");
                await OpenFunctionLocator(); locator.SetSearch(caption); await WorkspaceIdle();
                LayoutCheck(workspaceViews.Count == viewCount && workspaceViews.Count(v => v.Kind == "locator") == 1, "cross-document locator remains one tool tab");
                Ui.Capture(this, Path.ChangeExtension(evidence, "locator.png"));

                string install = Path.Combine(fixture, "install"); Directory.CreateDirectory(install);
                string mon = Path.Combine(install, "mon.exe"); File.WriteAllText(mon, "Non-executable relationship-query fixture");
                string shared = Path.Combine(install, "DesignCustomization.xml"), firstOnly = Path.Combine(install, "DraftCustomization.xml"), secondOnly = Path.Combine(install, "VerifyCustomization.xml"), broken = Path.Combine(install, "BrokenCustomization.xml");
                WorkspaceManifest(shared, WorkspaceXmlEntry("First", firstPath), WorkspaceXmlEntry("FirstDuplicate", firstPath), WorkspaceXmlEntry("Second", secondPath));
                WorkspaceManifest(firstOnly, WorkspaceXmlEntry("First", firstPath), WorkspaceXmlEntry("Unresolved", "%COMMAND_FIRST_UNRESOLVED_TEST%\\Unknown.uic"));
                WorkspaceManifest(secondOnly, WorkspaceXmlEntry("Second", secondPath));
                File.WriteAllText(broken, "<invalid");
                var unchanged = new[] { firstPath, secondPath, mon, shared, firstOnly, secondOnly, broken }.ToDictionary(path => path, path => File.ReadAllBytes(path));
                await OpenRegistrationExplorer();
                var explorerView = activeView;
                var explorer = explorerView.Controller as RegistrationExplorerPage;
                LayoutCheck(explorer != null && explorerView.Kind == "explorer" && explorerView.Document == null, "relationship explorer is an independent global tool");
                explorer.AddMonScope(mon); await explorer.Reload();
                var pairs = explorer.RelationSnapshot().Where(r => WorkspaceSamePath(r.monPath, mon)).ToArray();
                LayoutCheck(pairs.Length == 4 && pairs.Select(r => Path.GetFullPath(r.uicPath) + "\0" + Path.GetFullPath(r.xmlPath)).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 4, "real scan deduplicates repeated UIC-XML registrations");
                explorer.SelectPath(firstPath);
                var forward = explorer.VisibleRelationSnapshot();
                LayoutCheck(forward.Count(r => WorkspaceSamePath(r.monPath, mon)) == 2 && forward.All(r => WorkspaceSamePath(r.uicPath, firstPath)), "UIC to XML query shows unique matching XML relations");
                await WorkspaceIdle(); Ui.Capture(this, Path.ChangeExtension(evidence, "uic-to-xml.png"));
                explorer.SelectXmlPath(shared);
                var reverse = explorer.VisibleRelationSnapshot();
                LayoutCheck(reverse.Length == 2 && reverse.All(r => WorkspaceSamePath(r.xmlPath, shared)) && reverse.Select(r => Path.GetFullPath(r.uicPath)).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2, "XML to UIC query keeps both distinct UIC files and removes duplicate entries");
                LayoutCheck(explorer.DiagnosticMessages().Any(m => m.Contains("COMMAND_FIRST_UNRESOLVED_TEST")) && explorer.DiagnosticMessages().Any(m => m.IndexOf("BrokenCustomization.xml", StringComparison.OrdinalIgnoreCase) >= 0), "unresolved entries and malformed XML are explicit diagnostics");
                await WorkspaceIdle(); Ui.Capture(this, Path.ChangeExtension(evidence, "xml-to-uic.png"));
                int withExplorer = workspaceViews.Count;
                SelectWorkspaceView(first); await OpenRegistrationExplorer(firstPath);
                SelectWorkspaceView(second); await OpenRegistrationExplorer(secondPath);
                LayoutCheck(activeView == explorerView && Object.ReferenceEquals(activeView.Controller, explorer) && workspaceViews.Count == withExplorer && workspaceViews.Count(v => v.Kind == "explorer") == 1, "relationship explorer singleton survives switching active UIC");
                LayoutCheck(unchanged.All(pair => pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))), "both global query tools leave all UIC, XML and mon fixture bytes unchanged");

                SelectWorkspaceView(first); SwitchCommandView("layout"); await WorkspaceIdle();
                var entrance = CommandVisuals<Button>(documentCommands).First(b => Convert.ToString(b.ToolTip) == "UIC ↔ XML");
                entrance.ApplyTemplate();
                var surface = entrance.Template.FindName("Surface", entrance) as Border;
                LayoutCheck(surface != null && surface.CornerRadius.TopLeft >= 8, "global command uses rounded button surface");
                LayoutCheck(Ui.Card(new TextBlock()).CornerRadius.TopLeft >= 12, "shared content cards retain large rounded corners");
                FocusManager.SetFocusedElement(this, entrance);
                LayoutCheck(entrance.Focusable && entrance.IsTabStop && FocusManager.GetFocusedElement(this) == entrance, "rounded global command remains keyboard focusable");
                var samples = await CaptureGlobalWorkspaceViews(evidence, first, locatorView, explorerView);
                Ui.Capture(this, evidence);
                File.WriteAllText(Path.ChangeExtension(evidence, "json"), Json.Write(new { passed = true, defaultRibbonEditor = true, documentCount = workspaceViews.Count(v => v.Kind == "document"), locatorSingleton = true, explorerSingleton = true, sameNameAndIdAcrossFiles = true, nativeLocatorClick = true, relationPairCount = pairs.Length, forwardFixtureCount = 2, reverseFixtureCount = reverse.Length, diagnostics = explorer.DiagnosticMessages(), noFixtureWrites = true, buttonCornerRadius = surface.CornerRadius.TopLeft, cardCornerRadius = Ui.Card(new TextBlock()).CornerRadius.TopLeft, samples = samples, unsupportedWidth768 = true, multipleNativeDpiNotSampled = true }));
            }
            finally
            {
                foreach (var view in workspaceViews.Where(v => !originalViews.Contains(v)).ToList()) RemoveWorkspaceView(view);
                if (activeView != first) SelectWorkspaceView(first);
                Load(backup); SwitchCommandView("layout");
            }
        }
    }
}
