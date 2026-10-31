namespace LaunchDesk.Desktop;

/// <summary>使用资源字典中的设计模板呈现页面模型。</summary>
public abstract class DesignedPage : ContentControl
{
    /// <summary>以指定资源模板显示传入的页面模型。</summary>
    /// <param name="model">需呈现的页面模型。</param>
    /// <param name="template">可复用的非秘密参数模板</param>
    protected DesignedPage(object model, string template)
    {
        DataContext = model;
        Content = model;
        ContentTemplate = (DataTemplate)Application.Current.FindResource(template);
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
    }
}
/// <summary>呈现许可摘要、常用登录和所选配置详情。</summary>
/// <param name="shell">组合后的页面模型。</param>
public sealed class DashboardView(ShellViewModel shell) : DesignedPage(shell, "DashboardPage");
/// <summary>呈现登录编辑、密码输入及启动预览。</summary>
/// <param name="shell">组合后的页面模型。</param>
public sealed class ProfilesView(ShellViewModel shell) : DesignedPage(shell, "ProfilesPage");

/// <summary>通过依赖属性装配临时密码输入控件。</summary>
public sealed class PasswordEditor : ContentControl
{
    /// <summary>秘密输入行为的 WPF 依赖属性标识。</summary>
    public static readonly DependencyProperty BehaviorProperty = DependencyProperty.Register(nameof(Behavior), typeof(PasswordEntryBehavior), typeof(PasswordEditor), new PropertyMetadata(null, Changed));
    /// <summary>当前控件使用的临时秘密输入行为。</summary>
    public PasswordEntryBehavior? Behavior { get => (PasswordEntryBehavior?)GetValue(BehaviorProperty); set => SetValue(BehaviorProperty, value); }
    /// <summary>创建填满可用宽度、通过依赖属性绑定临时密码行为的容器。</summary>
    public PasswordEditor() { HorizontalContentAlignment = HorizontalAlignment.Stretch; }
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs e) => ((PasswordEditor)target).Content = e.NewValue is PasswordEntryBehavior behavior ? new PasswordEntryControl(behavior) : null;
}

/// <summary>呈现应用登记、发现规则与入口编辑。</summary>
public sealed class ApplicationsView : ContentControl
{
    /// <summary>创建应用列表与入口编辑表单，并绑定传入的应用页面模型。</summary>
    /// <param name="vm">页面模型。</param>
    public ApplicationsView(ApplicationsPageViewModel vm)
    {
        DataContext = vm;
        var list = Ui.List("Applications", "Selected", "Name", "ExecutablePath");
        list.SetResourceReference(ItemsControl.ItemContainerStyleProperty, "ProfileItem");
        var form = Ui.Stack(Ui.Text("应用入口", 18, bold:true),
            Ui.Pair(Ui.Field("名称", "Draft.Name"), Ui.Field("版本分组", "Draft.Version")),
            Ui.Combo("入口类型", "Kinds", "Draft.Kind"),
            Ui.Field("程序路径 · mon.exe / EXE / BAT", "Draft.Executable"), Ui.Button("选择程序", "BrowseExecutableCommand", "Folder"),
            Ui.Field("初始化文件", "Draft.Initialization"), Ui.Button("选择 INIT", "BrowseInitCommand", "Folder"),
            Ui.Pair(Ui.Field("工作目录", "Draft.WorkingDirectory"), Ui.Field("产品代码", "Draft.ProductCode")),
            Ui.Field("发现目录 · 每行一个", "Draft.Roots", true),
            Ui.Row(Ui.Button("自动查找", "DiscoverCommand", "Search"), Ui.Button("选择工作目录", "BrowseWorkingDirectoryCommand", "Folder")),
            Ui.Combo("发现候选 · 不覆盖人工路径", "Candidates", "SelectedCandidate"),
            Ui.Field("通用参数模板 · 每行一个参数", "Draft.ArgumentTemplate", true), Ui.Check("保留人工绑定路径", "Draft.ManualBinding"));
        Content = Ui.DetailColumns(Ui.HeaderBody(Ui.Text("已登记应用",18,bold:true), list),
            Ui.Card(Ui.HeaderBody(new Border(), Ui.Scroll(form), Ui.Row(Ui.Button("保存应用", "SaveCommand", "Save", true), Ui.Button("移除", "DeleteCommand", "Trash")))), 460);
    }
}

/// <summary>呈现许可连接列表及探测设置。</summary>
public sealed class LicensesView : ContentControl
{
    /// <summary>创建许可连接列表、探测设置和保存操作视图。</summary>
    /// <param name="vm">页面模型。</param>
    public LicensesView(LicensePageViewModel vm)
    {
        DataContext = vm;
        var list = Ui.List("Workspace.Resources", "Selected", "Name", "Address");
        list.ItemTemplate = Ui.Template("<StackPanel><TextBlock Text='{Binding Name}' FontSize='16' FontWeight='SemiBold'/><TextBlock Text='{Binding Address}' FontSize='18' Margin='0,12,0,12'/><TextBlock Text='{Binding State}' Foreground='{Binding Color}' FontWeight='SemiBold'/><TextBlock Text='{Binding Evidence}' FontSize='11' Foreground='#6D7890' Margin='0,12,0,0'/></StackPanel>");
        list.SetResourceReference(ItemsControl.ItemContainerStyleProperty,"ProfileItem");
        var form = Ui.Stack(Ui.Text("连接设置",18,bold:true), Ui.Field("名称", "Draft.Name"), Ui.Field("主机名或 IP", "Draft.Host"),
            Ui.Pair(Ui.Field("RMS 端口", "Draft.RmsPort"), Ui.Field("RULES 端口", "Draft.RulesPort")),
            Ui.Check("此连接需要 RULES", "Draft.RequireRules"), Ui.Check("启用监测", "Draft.Enabled"),
            Ui.Check("检测本机 RMS 服务", "Draft.UseService"), Ui.Field("服务名称", "Draft.ServiceName"),
            Ui.Field("VMware 虚拟机（可选）", "Draft.VmPath"), Ui.Button("选择虚拟机", "BrowseVmCommand", "Folder"),
            Ui.Text("连续复查策略",15,bold:true),
            Ui.Pair(Ui.Field("每轮尝试次数", "Draft.Attempts"), Ui.Field("失败轮数", "Draft.FailedRounds")),
            Ui.Pair(Ui.Field("超时（毫秒）", "Draft.TimeoutMilliseconds"), Ui.Field("轮询（秒）", "Draft.PollSeconds")));
        Content = Ui.DetailColumns(Ui.HeaderBody(Ui.Row(Ui.Text("许可连接",18,bold:true), Ui.Button("检测选中连接", "RefreshCommand", "Refresh")),list),
            Ui.Card(Ui.HeaderBody(new Border(),Ui.Scroll(form), Ui.Row(Ui.Button("保存连接", "SaveCommand", "Save",true),Ui.Button("移除", "DeleteCommand", "Trash")))),398);
    }
}
