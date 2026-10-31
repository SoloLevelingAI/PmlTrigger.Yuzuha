using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace RibbonStudio
{
    public partial class MainWindow
    {
        static IEnumerable<T> CommandVisuals<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T) yield return (T)child;
                foreach (var nested in CommandVisuals<T>(child)) yield return nested;
            }
        }

        static bool CommandHasScrollAncestor(DependencyObject element, DependencyObject stop)
        {
            for (var parent = VisualTreeHelper.GetParent(element); parent != null && parent != stop; parent = VisualTreeHelper.GetParent(parent))
                if (parent is ScrollViewer) return true;
            return false;
        }

        async Task CaptureCommandViews(string evidence)
        {
            var records = new List<object>();
            double beforeWidth = Width;
            string beforeView = CurrentCommandView;
            try
            {
                foreach (double width in new[] { 1280.0, 1440.0, 1050.0 })
                {
                    Width = width;
                    foreach (var kind in new[] { "library", "layout", "review" })
                    {
                        SwitchCommandView(kind);
                        await Dispatcher.InvokeAsync(new Action(() => UpdateLayout()), DispatcherPriority.ApplicationIdle);
                        LayoutCheck(Math.Abs(ActualWidth - width) < 2, "command-first actual viewport " + width);
                        LayoutCheck(CurrentCommandView == kind, "command-first view switch " + kind);
                        var alignment = new List<object>();
                        var toolbarButtons = CommandVisuals<Button>(documentCommands).Where(b => b.IsVisible && b.ActualWidth > 0 && b.ActualHeight > 0).ToList();
                        LayoutCheck(toolbarButtons.Count >= 3, "global tool command entrances are visible");
                        // The global toolbar may wrap at the supported minimum width.
                        // Compare real controls within each rendered row rather than
                        // requiring the removed per-document three-view navigation.
                        var toolbarRows = toolbarButtons.GroupBy(b => Math.Round(b.TranslatePoint(new Point(), this).Y));
                        foreach (var row in toolbarRows)
                        {
                            var origin = row.First().TranslatePoint(new Point(), this);
                            double height = row.First().ActualHeight;
                            foreach (var entrance in row)
                            {
                                var top = entrance.TranslatePoint(new Point(), this);
                                double topDelta = top.Y - origin.Y, heightDelta = entrance.ActualHeight - height;
                                LayoutCheck(Math.Abs(topDelta) <= 1 && Math.Abs(heightDelta) <= 1, "global tool entrances share row top and height " + kind + " / " + width);
                                alignment.Add(new { check = "global-toolbar-row-top-and-height", label = Convert.ToString(entrance.Content), top = top.Y, height = entrance.ActualHeight, topDelta = topDelta, heightDelta = heightDelta, toleranceDip = 1.0 });
                            }
                        }
                        if (kind == "library" && commandLibraryDetailCard.IsVisible && commandLibraryDetailCard.ActualWidth > 0)
                        {
                            var gridTop = commandLibraryGrid.TranslatePoint(new Point(), this);
                            var detailTop = commandLibraryDetailCard.TranslatePoint(new Point(), this);
                            double topDelta = detailTop.Y - gridTop.Y;
                            double bottomDelta = detailTop.Y + commandLibraryDetailCard.ActualHeight - gridTop.Y - commandLibraryGrid.ActualHeight;
                            LayoutCheck(Math.Abs(topDelta) <= 1 && Math.Abs(bottomDelta) <= 1, "library table and detail card share top and bottom " + width);
                            alignment.Add(new { check = "library-table-detail-edges", tableTop = gridTop.Y, tableBottom = gridTop.Y + commandLibraryGrid.ActualHeight, detailTop = detailTop.Y, detailBottom = detailTop.Y + commandLibraryDetailCard.ActualHeight, topDelta = topDelta, bottomDelta = bottomDelta, toleranceDip = 1.0 });
                        }
                        var positions = new List<object>();
                        foreach (var button in CommandVisuals<Button>(this).Where(b => b.IsVisible && b.ActualWidth > 0 && b.ActualHeight > 0))
                        {
                            var point = button.TranslatePoint(new Point(), this);
                            bool scrolled = CommandHasScrollAncestor(button, this);
                            bool fits = point.X >= -1 && point.Y >= -1 && point.X + button.ActualWidth <= ActualWidth + 1 && point.Y + button.ActualHeight <= ActualHeight + 1;
                            // Scroll content may legitimately extend beyond its viewport;
                            // record those controls rather than counting them as visible overflow.
                            if (!scrolled) LayoutCheck(fits, "command-first command bounds " + kind + " / " + width + " / " + Convert.ToString(button.Content));
                            positions.Add(new { label = Convert.ToString(button.Content), x = point.X, y = point.Y, width = button.ActualWidth, height = button.ActualHeight, fitsWindow = fits, scrollContent = scrolled });
                        }
                        string image = Path.Combine(Path.GetDirectoryName(evidence), Path.GetFileNameWithoutExtension(evidence) + "-" + kind + "-" + ((int)width) + ".png");
                        Ui.Capture(this, image);
                        records.Add(new { view = kind, requestedWidth = width, actualWidth = ActualWidth, actualHeight = ActualHeight, image = image, controls = positions, alignment = alignment });
                    }
                }
                var source = PresentationSource.FromVisual(this);
                var transform = source == null || source.CompositionTarget == null ? Matrix.Identity : source.CompositionTarget.TransformToDevice;
                File.WriteAllText(Path.ChangeExtension(evidence, "layout.json"), Json.Write(new {
                    samples = records,
                    nativeDpiScaleX = transform.M11,
                    nativeDpiScaleY = transform.M22,
                    unsupportedWidth = 768,
                    width768 = "Not supported: current native minimum width is 1050 DIPs",
                    dpi125And150 = "Not independently sampled; screenshots retain current native process DPI",
                    opticalBalance = "Manual review required; geometric fit does not establish optical balance"
                }));
            }
            finally { Width = beforeWidth; SwitchCommandView(beforeView); UpdateLayout(); }
        }

        async Task VerifyCommandReviewRouting(RibbonButton fresh, RibbonButton other, string evidence)
        {
            string originalMacro = fresh.macro;
            string issuePath = "buttons[" + Project.buttons.IndexOf(fresh) + "].macro";
            ApplyLibraryFields(new[] { fresh }, new Dictionary<string, string> { { "macro", "" } });
            SwitchCommandView("review");
            await RefreshCommandReview();
            await Dispatcher.InvokeAsync(new Action(() => UpdateLayout()), DispatcherPriority.ApplicationIdle);
            var issue = commandValidation.issues.FirstOrDefault(i => i.path == issuePath);
            LayoutCheck(!commandValidation.valid && issue != null, "review receives real backend issue for empty command Macro");
            LayoutCheck(commandReviewRows.IsVisible && CommandVisuals<TextBlock>(commandReviewRows).Any(t => t.IsVisible && t.Text == issue.message), "backend issue is rendered in visible native review list");
            Ui.Capture(this, Path.ChangeExtension(evidence, "review-invalid.png"));
            LocateValidationIssue(issue.path);
            LayoutCheck(CurrentCommandView == "library" && Selected.Button == fresh && LibraryState().PrimaryId == fresh.id && ((CommandLibraryRow)commandLibraryGrid.SelectedItem).Button == fresh, "backend issue locates exact command in native library");
            ApplyLibraryFields(new[] { fresh }, new Dictionary<string, string> { { "macro", originalMacro } });
            SelectLibraryCommand(other.id);
            SwitchCommandView("review");
            var before = Selected;
            LocateValidationIssue(issue.path);
            LayoutCheck(CurrentCommandView == "review" && Object.ReferenceEquals(Selected, before) && LibraryState().PrimaryId == other.id && status.Text.Contains("过期"), "old revision issue cannot relocate after command changes");
            LayoutCheck(commandReviewSummary.Text.Contains("尚未检查"), "review visibly marks changed revision as unchecked");
            await RefreshCommandReview();
            LayoutCheck(!commandValidation.issues.Any(i => i.path == issuePath), "fresh backend check clears repaired Macro issue");
            File.WriteAllText(Path.ChangeExtension(evidence, "review-routing.json"), Json.Write(new { backendIssuePath = issue.path, backendIssueMessage = issue.message, visibleNativeIssue = true, exactLibraryLocation = true, staleRevisionRejected = true, repairedIssueCleared = true }));
        }

        public async Task VerifyCommandFirst(string evidence)
        {
            var backup = Json.Copy(Project);
            var first = workspaceViews.First(v => v.Kind == "document" && v.Document == activeDocument);
            string originalView = CurrentCommandView;
            WorkspaceView second = null;
            try
            {
                SwitchCommandView("library");
                RenderCommandLibrary();
                await Dispatcher.InvokeAsync(new Action(() => UpdateLayout()), DispatcherPriority.ApplicationIdle);
                if (!commandLibraryGrid.IsVisible)
                {
                    var ancestors = new List<object>();
                    for (DependencyObject node = commandLibraryGrid; node != null; node = VisualTreeHelper.GetParent(node))
                    {
                        var element = node as FrameworkElement;
                        if (element != null) ancestors.Add(new { type = element.GetType().Name, name = element.Name, visibility = element.Visibility.ToString(), isVisible = element.IsVisible, isLoaded = element.IsLoaded, width = element.ActualWidth, height = element.ActualHeight });
                    }
                    string diagnostics = Json.Write(new {
                        activeViewKind = activeView == null ? null : activeView.Kind,
                        currentCommandView = CurrentCommandView,
                        libraryHostVisibility = commandLibraryHost.Visibility.ToString(),
                        libraryHostIsVisible = commandLibraryHost.IsVisible,
                        designHostIsVisible = designHost.IsVisible,
                        selectedWorkspaceItem = Convert.ToString(workPages.SelectedItem),
                        activeTabIsSelected = activeView != null && activeView.Tab.IsSelected,
                        ancestors = ancestors
                    });
                    File.WriteAllText(Path.ChangeExtension(evidence, "visibility.json"), diagnostics);
                    Ui.Capture(this, Path.ChangeExtension(evidence, "visibility.png"));
                    LayoutCheck(false, "command library is not visible after native layout: " + diagnostics);
                }
                LayoutCheck(commandLibraryGrid.IsVisible, "command library is an actual visible native grid");
                LayoutCheck(commandLibraryGrid.Items.Count == Project.buttons.Select(b => b.id).Distinct().Count(), "library contains one row per definition");
                int definitions = Project.buttons.Count;
                int references = Project.tabs.SelectMany(t => t.groups).Sum(g => g.items.Count);
                int steps = undoEdits.Count;
                var fresh = AddLibraryCommand();
                LayoutCheck(fresh != null && Project.buttons.Count == definitions + 1 && CommandReferenceLocations(fresh.id).Count == 0, "new command is an unplaced definition");
                LayoutCheck(Project.tabs.SelectMany(t => t.groups).Sum(g => g.items.Count) == references && undoEdits.Count == steps + 1, "new definition creates no automatic placement and one undo step");
                await UndoEdit();
                LayoutCheck(!Project.buttons.Contains(fresh), "undo unplaced definition");
                await RedoEdit();
                LayoutCheck(Project.buttons.Contains(fresh), "redo preserves definition identity");

                var state = LibraryState();
                state.Unplaced = true;
                state.Query = fresh.caption;
                RenderCommandLibrary();
                LayoutCheck(commandLibraryGrid.Items.OfType<CommandLibraryRow>().Any(r => r.Button == fresh), "unplaced filter exposes unplaced definition");
                state.Unplaced = false; state.Query = ""; RenderCommandLibrary();
                var other = Project.buttons.First(b => b != fresh);
                state.PrimaryId = other.id;
                state.Marked.Clear(); state.Marked.Add(fresh.id);
                LayoutCheck(CommandLibrarySelection().Count == 1 && CommandLibrarySelection()[0] == fresh, "marked set excludes unrelated primary selection");
                state.Marked.Clear();
                LayoutCheck(CommandLibrarySelection().Count == 1 && CommandLibrarySelection()[0] == other, "unmarked single operations fall back to primary selection");

                var tab = Project.tabs.First(t => t.groups.Count > 0);
                var group = tab.groups.First();
                var beforeItems = group.items.ToList();
                steps = undoEdits.Count;
                LayoutCheck(PlaceCommandReferences(new[] { fresh.id, other.id }, tab, group, "small") == 2, "place selected definitions without drag");
                var inserted = group.items.Skip(beforeItems.Count).ToList();
                LayoutCheck(inserted.Count == 2 && inserted.All(i => i.size == "small") && undoEdits.Count == steps + 1, "batch placement is one incremental history entry");
                await UndoEdit();
                LayoutCheck(group.items.SequenceEqual(beforeItems), "undo placement restores exact order");
                await RedoEdit();
                LayoutCheck(group.items.Skip(beforeItems.Count).SequenceEqual(inserted), "redo placement reuses item instances");
                LayoutCheck(PlaceCommandReferences(new[] { fresh.id }, tab, group, "large") == 1, "second placement shares the definition");
                var locations = CommandReferenceLocations(fresh.id);
                LayoutCheck(locations.Count == 2 && locations.All(l => l.Button == fresh) && locations.Select(l => l.Item.size).SequenceEqual(new[] { "small", "large" }), "two independent references share one definition and retain sizes");
                string originalOtherMacro = other.macro;
                steps = undoEdits.Count;
                ApplyLibraryFields(new[] { fresh }, new Dictionary<string, string> { { "macro", "show !!CommandFirstFixture" }, { "tooltipText", "共享功能 <>& Tooltip" } });
                LayoutCheck(undoEdits.Count == steps + 1 && CommandReferenceLocations(fresh.id).All(l => l.Button.macro == "show !!CommandFirstFixture") && other.macro == originalOtherMacro, "definition batch updates both references only and one undo step");
                await UndoEdit();
                LayoutCheck(fresh.macro != "show !!CommandFirstFixture", "undo shared definition fields");
                await RedoEdit();
                LayoutCheck(fresh.macro == "show !!CommandFirstFixture", "redo shared definition fields");
                SwitchCommandView("library");
                SelectLibraryCommand(fresh.id);
                RenderCommandLibrary();
                var tooltipInput = commandLibraryDetails.Children.OfType<TextBox>().First(b => Convert.ToString(b.Tag) == "command-library:tooltipText");
                tooltipInput.Text = "原生详情输入回放";
                LayoutCheck(HasPendingLibraryDrafts(Project) && fresh.tooltipText == "共享功能 <>& Tooltip", "native detail input remains explicitly unapplied");
                SwitchCommandView("layout"); SwitchCommandView("library");
                LayoutCheck(commandLibraryDetails.Children.OfType<TextBox>().First(b => Convert.ToString(b.Tag) == "command-library:tooltipText").Text == "原生详情输入回放" && HasPendingLibraryDrafts(Project), "unapplied detail input survives work-view switch");
                bool pendingBlocked = false;
                try { RequireAppliedCommandDrafts(Project); }
                catch (InvalidOperationException) { pendingBlocked = true; }
                LayoutCheck(pendingBlocked && HasPendingLibraryDrafts(Project), "pending detail input blocks completion without losing draft");
                commandLibraryDetails.Children.OfType<Button>().First(b => Convert.ToString(b.Content) == "取消待应用修改").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                LayoutCheck(!HasPendingLibraryDrafts(Project) && fresh.tooltipText == "共享功能 <>& Tooltip", "native cancel clears pending draft without mutating definition");
                commandLibraryDetails.Children.OfType<TextBox>().First(b => Convert.ToString(b.Tag) == "command-library:tooltipText").Text = "原生详情输入回放";
                steps = undoEdits.Count;
                commandLibraryDetails.Children.OfType<Button>().First(b => Convert.ToString(b.Content) == "应用定义修改").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                LayoutCheck(fresh.tooltipText == "原生详情输入回放" && undoEdits.Count == steps + 1, "native details input and apply button route to shared definition");
                await UndoEdit();
                LayoutCheck(fresh.tooltipText == "共享功能 <>& Tooltip", "native details apply can be undone");
                var removed = locations[0]; var untouched = locations[1].Item;
                int at = group.items.IndexOf(removed.Item); steps = undoEdits.Count;
                LayoutCheck(RemoveCommandReference(removed), "remove current reference");
                LayoutCheck(Project.buttons.Contains(fresh) && CommandReferenceLocations(fresh.id).Count == 1 && group.items.Contains(untouched) && undoEdits.Count == steps + 1, "remove retains definition and other reference");
                await UndoEdit();
                LayoutCheck(group.items[at] == removed.Item && group.items.Contains(untouched), "undo reference removal restores same instance and position");
                LayoutCheck(LocateCommandReference(removed) && Selected.Item == removed.Item && activePreviewTab == removed.Tab, "use location lookup reaches exact layout reference");
                await VerifyCommandReviewRouting(fresh, other, evidence);

                SwitchCommandView("library");
                state = LibraryState(); state.Query = fresh.caption; state.Marked.Clear(); state.Marked.Add(fresh.id); state.PrimaryId = fresh.id;
                RenderCommandLibrary();
                SwitchCommandView("layout");
                var firstProject = Project;
                int firstUndo = undoEdits.Count;
                var copy = Json.Copy(Project); copy.documentId = null; copy.origin = null; copy.preferredBinding = null; copy.@namespace = "CommandFirstSecondFixture";
                await OpenDocument(copy);
                second = activeView;
                SwitchCommandView("library");
                LayoutCheck(LibraryState().Marked.Count == 0 && LibraryState().Query == "", "second document has independent library filter and marked set");
                SelectWorkspaceView(first);
                LayoutCheck(Object.ReferenceEquals(Project, firstProject) && CurrentCommandView == "layout" && undoEdits.Count == firstUndo, "document view and undo retained across files");
                SwitchCommandView("library");
                LayoutCheck(LibraryState().Query == fresh.caption && LibraryState().Marked.Contains(fresh.id) && LibraryState().PrimaryId == fresh.id, "first document library state retained");
                state = LibraryState(); state.Query = ""; state.Unplaced = false; state.Marked.Clear(); RenderCommandLibrary();
                await CaptureCommandViews(evidence);
                SwitchCommandView("library"); Ui.Capture(this, evidence);
            }
            finally
            {
                if (second != null && workspaceViews.Contains(second)) RemoveWorkspaceView(second);
                if (activeView != first) SelectWorkspaceView(first);
                Load(backup); SwitchCommandView(originalView);
            }
        }
    }
}
