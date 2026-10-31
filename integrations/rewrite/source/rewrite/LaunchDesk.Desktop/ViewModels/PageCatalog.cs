namespace LaunchDesk.Desktop;

public sealed partial class ShellViewModel
{
    // 每页的标题、职责、按钮文案、图标和命令在同一处登记，避免平行 switch 漂移。
    // 命令通过当前外壳解析，继续使用各页面已装配的命令实例和各自的可执行条件。
    private sealed record PageAction(string Text, string Icon, Func<ShellViewModel, ICommand> Command);
    private sealed record PageDefinition(string Title, string Subtitle, PageAction Header, PageAction? Secondary = null);

    private static readonly PageAction NewProfile = new("新增登录", "Plus", shell => shell.Profiles.NewCommand);
    private static readonly PageAction RefreshPage = new("刷新状态", "Refresh", shell => shell.RefreshCommand);
    private static readonly PageAction RefreshHistory = new("刷新记录", "Plus", shell => shell.Tools.RefreshHistoryCommand);
    private const string SettingsSubtitle = "管理数据位置、旧版迁移与未确定提交。";
    private static readonly PageDefinition FallbackPage = new("设置与迁移", SettingsSubtitle, RefreshHistory);
    private static readonly IReadOnlyDictionary<PageId, PageDefinition> PageDefinitions = new Dictionary<PageId, PageDefinition>
    {
        [PageId.Dashboard] = new("每个版本，都有自己的入口", "按项目启动，按许可连接。每次启动独立使用对应的许可地址。", NewProfile,
            new("导入配置", "Import", shell => shell.GoSharingCommand)),
        [PageId.Profiles] = new("登录配置", "把项目、版本与工具组合成独立登录配置。", NewProfile,
            new("复制配置", "Copy", shell => shell.Profiles.CopyCommand)),
        [PageId.Licenses] = new("许可监测", "分别观察服务、端口与虚拟机；连续失败后再判定掉线。",
            new("新增许可", "Plus", shell => shell.Licenses.NewCommand)),
        [PageId.Tools] = new("工具与扩展", "统一管理文件包、更新来源、交付目标与环境变量。",
            new("添加工具", "Plus", shell => shell.Tools.NewCommand)),
        [PageId.History] = new("更新记录", "查看实际写入与备份回执，按当前差异恢复或回滚。",
            RefreshHistory with { Icon = "Refresh" }),
        [PageId.Applications] = new("应用与版本", "登记任意应用入口，自动发现路径并保留人工选择。",
            new("手动添加", "Plus", shell => shell.Applications.NewCommand),
            new("自动查找", "Search", shell => shell.Applications.DiscoverCommand)),
        [PageId.Sharing] = new("导入与分享", "分享配置结构，在接收电脑重新核验本机绑定。",
            new("选择导入文件", "Import", shell => shell.Sharing.PreviewCommand)),
        [PageId.Settings] = new("设置与迁移", SettingsSubtitle,
            new("选择旧配置", "Folder", shell => shell.Settings.BrowseCommand)),
        [PageId.Deployment] = new("工具部署与更新", "核对来源与目标，确认后执行这份固定计划。",
            new("返回工具库", "Back", shell => shell.GoToolsCommand)),
        [PageId.ToolSettings] = new("工具配置与环境变量", SettingsSubtitle,
            new("保存配置", "Save", shell => shell.Tools.SaveCommand))
    };

    private PageDefinition CurrentPage => PageDefinitions.GetValueOrDefault(Page) ?? FallbackPage;
    private PageAction SecondaryAction => CurrentPage.Secondary ?? RefreshPage;
}
