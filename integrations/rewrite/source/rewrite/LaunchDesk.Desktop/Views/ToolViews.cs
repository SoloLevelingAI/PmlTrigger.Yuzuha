namespace LaunchDesk.Desktop;

/// <summary>呈现按工具组织的卡片入口。</summary>
/// <param name="vm">页面模型。</param>
public sealed class ToolsView(ToolPageViewModel vm) : DesignedPage(vm, "ToolsPage");

/// <summary>呈现独立工具信息、变量与分享操作。</summary>
/// <param name="vm">页面模型。</param>
public sealed class ToolSettingsView(ToolPageViewModel vm) : DesignedPage(vm, "ToolSettingsPage");

/// <summary>呈现来源选择、普通文件映射及冻结部署预览。</summary>
public sealed class DeploymentView : ContentControl
{
    /// <summary>创建文件变更预览表及明确确认后的部署操作视图。</summary>
    /// <param name="vm">页面模型。</param>
    public DeploymentView(ToolPageViewModel vm)
    {
        DataContext = vm;
        var table = Ui.Table("Changes", ("相对路径", "RelativePath", false), ("字节数", "SourceBytes", false),
            ("移除", "Delete", false));
        var source = Ui.Card(Ui.HeaderBody(Ui.Stack(Ui.Text("更新来源",18,bold:true),Ui.Text("刷新可用来源后，明确选择本次使用的文件包。",12,"#6D7890")),
            Ui.Scroll(Ui.Stack(Ui.Field("查找目录", "Draft.SearchDirectory"), Ui.Row(Ui.Button("浏览目录","BrowseSearchCommand","Folder"),Ui.Button("刷新来源","RefreshSourcesCommand","Refresh"),Ui.Button("选择最新","LatestSourceCommand","Check")),
                Ui.Combo("可用来源", "Sources", "SelectedSource", "Path"),Ui.Combo("来源类型", "SourceKinds", "Draft.Kind"),Ui.Field("当前来源", "Draft.Source"),
                Ui.Row(Ui.Button("选择来源","BrowseSourceCommand","Folder"),Ui.Button("管理普通文件","EditFilesCommand","Folder")),
                Ui.Field("目标目录", "Draft.Target"),Ui.Button("选择目标目录","BrowseTargetCommand","Folder"),
                Ui.Check("展开单层根目录","Draft.FlattenRoot"),Ui.Check("保留备份","Draft.KeepBackup"))),
            Ui.Row(Ui.Button("预览变更","PreviewCommand","Search",true),Ui.Button("工具配置","EditSelectedCommand","Settings"))));
        var body = Ui.DetailColumns(source, Ui.Card(Ui.HeaderBody(
            Ui.Stack(Ui.Bound("Draft.Name",24,"#0F6CBD",true),Ui.Bound("PlanSummary",14,bold:true),
                Ui.Text("预览包含内容指纹与目标基线。来源或目标改变后需重新预览。",11,"#6D7890")),table,
            Ui.Stack(Ui.Check("以管理员身份执行这份计划", "UseAdmin"), Ui.Button("确认执行", "ExecuteCommand", "Check", true)))));
        var mappings = Ui.Table("Draft.Files",("源文件","SourcePath",true),("目标相对路径","RelativePath",true));
        mappings.Bind(System.Windows.Controls.Primitives.Selector.SelectedItemProperty,"SelectedMapping",BindingMode.TwoWay);
        var mappingBody = Ui.Card(Ui.HeaderBody(Ui.Stack(Ui.Text("普通文件包",20,bold:true),Ui.Text("按文件指定目标相对路径，支持子目录。",12,"#6D7890"),
                Ui.Row(Ui.Button("添加文件","AddFilesCommand","Plus"),Ui.Button("移除映射","RemoveFileCommand","Trash"))),mappings,
            Ui.Button("完成文件映射","CloseFilesCommand","Check",true)));
        mappingBody.Width=650; mappingBody.Height=440; mappingBody.HorizontalAlignment=HorizontalAlignment.Center; mappingBody.VerticalAlignment=VerticalAlignment.Center;
        var overlay = new Border {Background=Ui.Brush("#550F172A"),CornerRadius=new CornerRadius(12),Child=mappingBody};
        overlay.SetBinding(VisibilityProperty,new Binding("IsEditingFiles"){Converter=new BooleanToVisibilityConverter()});
        Grid.SetColumnSpan(overlay,3); Panel.SetZIndex(overlay,10); body.Children.Add(overlay);
        Content=body;
    }
}

/// <summary>呈现持久交付回执及恢复、回滚预览入口。</summary>
public sealed class HistoryView : ContentControl
{
    /// <summary>创建按工具筛选的交付历史与恢复、回滚操作视图。</summary>
    /// <param name="vm">页面模型。</param>
    public HistoryView(ToolPageViewModel vm)
    {
        DataContext = vm;
        var table = Ui.Table("History", ("时间", "Time", false), ("状态", "Status", false), ("目标", "Target", false),
            ("范围", "Count", false));
        table.Bind(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, "SelectedReceipt",
            BindingMode.TwoWay);
        var timeline = Ui.List("History", "SelectedReceipt", "Status", "Time");
        timeline.ItemTemplate=Ui.Template("<StackPanel><TextBlock Text='{Binding Time}' Foreground='#6D7890' FontSize='11'/><TextBlock Text='{Binding Status}' FontWeight='SemiBold' FontSize='16' Margin='0,10,0,8'/><TextBlock Text='{Binding Target}' Foreground='#6D7890' FontSize='11' TextTrimming='CharacterEllipsis'/><TextBlock Text='{Binding Count}' Foreground='#0F6CBD' FontSize='11' Margin='0,8,0,0'/></StackPanel>");
        Content=Ui.DetailColumns(Ui.HeaderBody(Ui.Row(Ui.Text("更新记录",18,bold:true),Ui.Button("刷新记录","RefreshHistoryCommand","Refresh")),timeline),
            Ui.Card(Ui.HeaderBody(Ui.Text("交付详情",18,bold:true),Ui.Scroll(Ui.Stack(Ui.Bound("SelectedReceipt.Status",22,"#0F6CBD",true),
                Ui.Bound("SelectedReceipt.Time",12,"#6D7890"),Ui.Text("目标目录",12,"#6D7890"),Ui.Bound("SelectedReceipt.Target",13),
                Ui.Text("操作编号",12,"#6D7890"),Ui.Bound("SelectedReceipt.Id",11),Ui.Bound("SelectedReceipt.Detail",11,"#6D7890"),
                Ui.Text("先按当前差异生成预览，再确认恢复或回滚。历史读取不会执行写入。",12,"#6D7890"))),
                Ui.Row(Ui.Button("预览恢复","RecoverCommand","Refresh",true),Ui.Button("预览回滚","RollbackCommand","History")))),398);
    }
}

/// <summary>呈现选择性导出与明确确认的配置导入。</summary>
public sealed class SharingView : ContentControl
{
    /// <summary>创建选择性配置导出、导入预览与确认提交视图。</summary>
    /// <param name="vm">页面模型。</param>
    public SharingView(SharingPageViewModel vm)
    {
        DataContext = vm;
        var profiles = new ItemsControl().Bind(ItemsControl.ItemsSourceProperty,"ProfileChoices");
        var tools = new ItemsControl().Bind(ItemsControl.ItemsSourceProperty,"ToolChoices");
        profiles.ItemTemplate = tools.ItemTemplate = Ui.Template("<CheckBox Content='{Binding Name}' IsChecked='{Binding Selected}' Margin='0,4,0,8'/>");
        var export = Ui.Card(Ui.HeaderBody(Ui.Stack(Ui.Text("导出配置",20,"#4E5DDB",true),Ui.Text("选择要分享的登录与工具",12,"#6D7890"),Ui.Row(Ui.Button("全选","SelectAllCommand","Check"),Ui.Button("清空选择","ClearSelectionCommand","Trash"))),
            Ui.Scroll(Ui.Stack(Ui.Text("登录配置",15,bold:true),profiles,Ui.Text("独立工具",15,bold:true),tools,Ui.Check("包含关联工具定义","IncludeTools"))),
            Ui.Stack(Ui.Text("密码和本机凭据关联不会导出。",11,"#6D7890"),Ui.Button("导出选中配置","ExportCommand","Export",true))));
        var import = Ui.Card(Ui.HeaderBody(Ui.Stack(Ui.Text("导入配置",20,"#0F6CBD",true),Ui.Text("先预览，再确认本机路径和连接",12,"#6D7890"),Ui.Button("选择并预览导入","PreviewCommand","Import")),
            Ui.Scroll(Ui.Bound("Preview",14,"#6D7890")),
            Ui.Stack(Ui.Check("在本机核验路径、许可与工具目录","Confirmed"),Ui.Button("确认导入配置","ImportCommand","Check",true),Ui.Button("核验原导入","VerifyImportCommand","Check"))));
        Content = Ui.DetailColumns(export,import,420);
    }
}

/// <summary>呈现数据目录、迁移与原结果核验。</summary>
public sealed class SettingsView : ContentControl
{
    /// <summary>创建工作空间目录展示、打开目录和旧版迁移视图。</summary>
    /// <param name="vm">页面模型。</param>
    public SettingsView(SettingsPageViewModel vm)
    {
        DataContext = vm;
        var form = Ui.Stack(Ui.Text("数据与迁移", 20, bold: true), Ui.Text("当前工作空间", 12, "#748198"),
            Ui.Bound("DataDirectory", 14, "#0874CB"),
            Ui.Row(Ui.Button("打开配置目录", "OpenDirectoryCommand", "Folder"),
                Ui.Button("核验未确定提交", "VerifyCommitCommand", "Check")), Ui.Text("旧版迁移", 18, bold: true),
            Ui.Field("旧配置文件", "SourcePath"),
            Ui.Row(Ui.Button("选择文件", "BrowseCommand", "Folder"), Ui.Button("生成迁移预览", "PreviewCommand", "Refresh")),
            Ui.Card(Ui.Bound("Preview", 13, "#748198")),Ui.Button("核验原迁移结果","VerifyMigrationCommand","Check"));
        Content = Ui.Card(Ui.HeaderBody(new Border(), Ui.Scroll(form),
            Ui.Button("确认迁移", "MigrateCommand", "Check", true)));
    }
}
