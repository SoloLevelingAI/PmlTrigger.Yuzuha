using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
namespace RibbonStudio
{
    public partial class MainWindow
    {
        static void LayoutCheck(bool value, string message)
        {
            if (!value)
                throw new Exception("Layout workflow: " + message);
        }
        public async Task VerifyLayoutWorkflow()
        {
            var backup = Json.Copy(Project);
            try
            {
                var t = Project.tabs.First();
                Selected = new Selection { Kind = "tab", Tab = t };
                await Add("tab");
                var second = Selected.Tab;
                LayoutCheck(PlaceSelection(Selected, new Selection { Kind = "tab", Tab = t }, false) && Project.tabs[0] == second, "Tab reorder before");
                LayoutCheck(!PlaceSelection(Selected, Selected, false), "self-drop rejected");
                LayoutCheck(PlaceSelection(Selected, new Selection { Kind = "tab", Tab = t }, true) && Project.tabs.Last() == second, "Tab reorder after");
                Selected = new Selection { Kind = "tab", Tab = t };
                await Add("group");
                var extra = Selected.Group;
                var first = t.groups.First();
                LayoutCheck(PlaceSelection(Selected, new Selection { Kind = "group", Tab = t, Group = first }, false) && t.groups[0] == extra, "Group reorder");
                LayoutCheck(!PlaceSelection(Selected, new Selection { Kind = "tab", Tab = second }, false), "Group ownership retained");
                Selected = new Selection { Kind = "tab", Tab = second };
                await Add("group");
                var destination = Selected.Group;
                var oldGroup = t.groups.First(g => g.items.Count > 0);
                var item = oldGroup.items.First();
                var button = Project.buttons.First(b => b.id == item.buttonId);
                string id = button.id, sourceId = button.sourceId, size = item.size;
                var source = new Selection { Kind = "button", Tab = t, Group = oldGroup, Item = item, Button = button };
                int definitions = Project.buttons.Count;
                LayoutCheck(PlaceSelection(source, new Selection { Kind = "group", Tab = second, Group = destination }, true), "button cross-Tab Group move");
                LayoutCheck(!oldGroup.items.Contains(item) && destination.items.Contains(item) && button.id == id && button.sourceId == sourceId && item.size == size && Project.buttons.Count == definitions, "move preserves identity, size and definitions");
                Selected = source;
                await CopyLayout(true);
                LayoutCheck(destination.items.Contains(item), "cut is deferred");
                Selected = new Selection { Kind = "group", Tab = t, Group = oldGroup };
                await PasteLayout();
                LayoutCheck(oldGroup.items.Contains(item) && !destination.items.Contains(item) && clipboardSelection == null, "cut paste moves once");
                Selected = source;
                await CopyLayout(false);
                Selected = new Selection { Kind = "group", Tab = second, Group = destination };
                await PasteLayout();
                var copy = Selected.Button;
                LayoutCheck(copy.id != id && copy.sourceId == null && copy.commandKey == "" && copy.macro == button.macro && Selected.Item.size == size, "copy clones independent button");
                await PasteLayout();
                LayoutCheck(Selected.Button.id != copy.id && destination.items.Count == 2, "repeat paste has fresh identities");
                var a = destination.items[0];
                var lastItem = destination.items[1];
                LayoutCheck(PlaceSelection(Selected, new Selection { Kind = "button", Tab = second, Group = destination, Item = a, Button = copy }, false) && destination.items[0] == lastItem, "button reorder before");
                LayoutCheck(!PlaceSelection(Selected, new Selection { Kind = "button", Tab = second, Group = destination, Item = a, Button = copy }, false), "adjacent no-op");
                Selected = new Selection { Kind = "group", Tab = t, Group = oldGroup };
                await CopyLayout(false);
                Selected = new Selection { Kind = "tab", Tab = second };
                await PasteLayout();
                LayoutCheck(Selected.Group.id != oldGroup.id && Selected.Group.sourceId == null && Selected.Group.items.All(i => !oldGroup.items.Any(o => o.buttonId == i.buttonId)), "Group clone remaps references");
                Selected = new Selection { Kind = "tab", Tab = t };
                await CopyLayout(false);
                Selected = new Selection { Kind = "project" };
                await PasteLayout();
                LayoutCheck(Selected.Tab.id != t.id && Selected.Tab.sourceId == null, "Tab clone");
                var stale = Selected;
                Load(Json.Copy(backup));
                LayoutCheck(!PlaceSelection(stale, new Selection { Kind = "project" }, true), "stale drag rejected");
                LayoutCheck(TreeNodes(tree.Items).Where(n => n.Tag is Selection).All(n => n.AllowDrop), "tree drag targets wired");
                LayoutCheck(ribbonPreview.Children.OfType<WrapPanel>().Count() == 2 && previewTabs.Children.OfType<Button>().Count() == Project.tabs.Count, "one Tab strip and one active group row");
                await Flush();
                await Deploy(false);
                var page = activeView.Page;
                await ShowDesign();
                await Deploy(false);
                LayoutCheck(activeView.Page == page && activeView.Kind == "deployment", "deployment page reused across switches");
                await Deploy(true);
                LayoutCheck(activeView.Kind == "registration", "separate registration tab");
                await ShowDesign();
            }
            finally { Load(backup); ShowCurrentDocument(); }
        }
    }
}
