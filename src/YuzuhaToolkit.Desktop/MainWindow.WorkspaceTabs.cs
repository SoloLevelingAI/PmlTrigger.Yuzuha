using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace YuzuhaToolkit.Desktop;

/// <summary>无可见页签头的工作区仍向辅助技术暴露当前内容，不暴露隐藏页。</summary>
public sealed class WorkspaceTabs : TabControl
{
    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SelectedWorkspacePeer(this);
    private sealed class SelectedWorkspacePeer(WorkspaceTabs owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
        protected override string GetClassNameCore() => nameof(WorkspaceTabs);
        protected override List<AutomationPeer>? GetChildrenCore() => owner.SelectedContent is UIElement child && UIElementAutomationPeer.CreatePeerForElement(child) is { } peer ? [peer] : base.GetChildrenCore();
    }
}
