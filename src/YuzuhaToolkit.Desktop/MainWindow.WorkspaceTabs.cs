using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace YuzuhaToolkit.Desktop;

/// <summary>无可见页签头的工作区仍向辅助技术暴露当前内容，不暴露隐藏页。</summary>
public sealed class WorkspaceTabs : TabControl
{
    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SelectedWorkspacePeer(this);
    /// <summary>仅为当前工作区内容提供自动化节点，隐藏非活动页签。</summary>
    private sealed class SelectedWorkspacePeer(WorkspaceTabs owner) : FrameworkElementAutomationPeer(owner)
    {
        /// <summary>将无页签头的工作区作为内容窗格暴露给辅助技术。</summary>
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
        /// <summary>返回工作区控件的稳定自动化类名。</summary>
        protected override string GetClassNameCore() => nameof(WorkspaceTabs);
        /// <summary>只返回当前选中内容的自动化节点，无法创建时使用基类行为。</summary>
        protected override List<AutomationPeer>? GetChildrenCore() => owner.SelectedContent is UIElement child && UIElementAutomationPeer.CreatePeerForElement(child) is { } peer ? [peer] : base.GetChildrenCore();
    }
}
