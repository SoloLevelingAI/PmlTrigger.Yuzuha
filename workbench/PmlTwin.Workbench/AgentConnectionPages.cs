using System.Windows;
using System.Windows.Controls;
using Core30.Core;
using static Core30.Workbench.Views;

namespace PmlTwin.Workbench;

public partial class MainWindow
{
    private void OpenAgentConnection()
    {
        var guides = PhaytonAgentGuide.Clients;
        var clients = Choice(guides.Select(item => item.Name), guides[0].Name, "AgentClient");
        var programs = RegisteredPhaytonCli.FindWorkbenchPrograms();
        var paths = Choice(programs, programs.Length == 1 ? programs[0] : null, "AgentCliPath");
        var guidance = Muted("");
        var command = Code("", "AgentReadOnlyCommand", 86);
        var probe = Btn("复制检查命令", "CopyAgentReadOnlyCommand", () => CopyText(command.Text));
        PhaytonAgentGuide Selection() => guides.FirstOrDefault(item => item.Name == clients.SelectedItem as string) ?? guides[0];

        void Refresh()
        {
            guidance.Text = Selection().Instructions;
            var executable = paths.SelectedItem as string;
            command.Text = executable is null ? "" : PhaytonAgentGuide.ReadOnlyCommand(executable);
            probe.IsEnabled = executable is not null;
        }

        clients.SelectionChanged += (_, _) => Refresh();
        paths.SelectionChanged += (_, _) => Refresh();
        Refresh();
        var pathControl = programs.Length switch
        {
            0 => Muted("未找到 Phayton CLI。请先从设置中的更新入口安装 Phayton。") as FrameworkElement,
            1 => Muted(programs[0]),
            _ => Field("选择 Phayton CLI", paths)
        };
        Modal("Agent 接入", Stack(Field("客户端", clients), Below(guidance, 7),
            Below(pathControl, 8),
            Below(Muted("在 AVEVA 所在的 Windows 运行。使用原生 CLI，Skill 用于说明步骤。"), 7),
            Below(command, 8),
            Below(Actions(probe, Btn("客户端官方文档", "OpenAgentDocumentation",
                () => OpenExternal(Selection().Documentation))), 7)), "完成", () => true, cancel: false, width: 675);
    }
}
