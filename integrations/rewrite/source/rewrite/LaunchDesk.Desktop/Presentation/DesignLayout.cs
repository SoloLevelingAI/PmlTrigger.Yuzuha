namespace LaunchDesk.Desktop;

/// <summary>按窗口尺寸提供原设计的布局度量，基础操作保持在固定区域。</summary>
public sealed class DesignLayout : ObservableObject
{
    /// <summary>是否采用紧凑窗口布局。</summary>
    public bool Compact { get; private set; }
    /// <summary>是否采用较低窗口布局。</summary>
    public bool Short { get; private set; }
    /// <summary>是否采用完整宽度布局。</summary>
    public bool Wide => !Compact;
    /// <summary>工作台详情区宽度。</summary>
    public double DetailWidth => Compact ? 326 : 412;
    /// <summary>编辑页面右侧信息区宽度。</summary>
    public double EditorWidth => Compact ? 300 : 398;
    /// <summary>更新详情面板宽度。</summary>
    public double HistoryWidth => Compact ? 326 : 430;
    /// <summary>相邻面板间距。</summary>
    public double Gap => Compact ? 20 : 24;
    /// <summary>主内容区的设计边距。</summary>
    public Thickness ContentMargin => new(32, Short ? 24 : 44, 32, 12);
    /// <summary>页面头部固定高度。</summary>
    public GridLength HeaderHeight => new(Short ? 82 : 102);
    /// <summary>状态与提示区固定高度。</summary>
    public GridLength FooterHeight => new(Short ? 42 : Compact ? 64 : 70);
    /// <summary>卡片表单的内边距。</summary>
    public Thickness FormPadding => new(Compact ? 16 : 24);
    /// <summary>当前布局中的输入框高度。</summary>
    public double FieldHeight => Compact ? 35 : 40;
    /// <summary>工作台许可及工具摘要卡片高度。</summary>
    public double SummaryHeight => Compact ? 112 : 132;
    /// <summary>登录卡片行高度。</summary>
    public double ProfileRowHeight => 98;
    /// <summary>详情卡片的标题字号。</summary>
    public double DetailTitleSize => Compact ? 20 : 24;
    /// <summary>详情字段行高度。</summary>
    public double MetaRowHeight => Compact ? 20 : 39;
    /// <summary>关联工具信息区高度。</summary>
    public GridLength AssociatedHeight => new(Compact ? 42 : 130);
    /// <summary>工具库卡片高度。</summary>
    public double ToolCardHeight => Compact ? 214 : 250;
    /// <summary>工具基础信息卡片高度。</summary>
    public double ToolInfoHeight => Compact ? 286 : 366;
    /// <summary>迁移说明卡片高度。</summary>
    public double MigrationCardHeight => Short ? 108 : Compact ? 128 : 226;
    /// <summary>部署来源选择区高度。</summary>
    public double DeploySourceHeight => Short ? 190 : Compact ? 216 : 274;
    /// <summary>部署操作确认区高度。</summary>
    public double DeployActionHeight => Compact ? 86 : 116;
    /// <summary>部署工具摘要区高度。</summary>
    public double DeployToolHeight => Compact ? 122 : 172;
    /// <summary>应用登记卡片高度。</summary>
    public double AppCardHeight => Compact ? 142 : 164;
    /// <summary>表单分组之间的垂直间距。</summary>
    public Thickness FormGroupMargin => new(0, Compact ? 8 : 20, 0, 0);
    /// <summary>面板右侧设计间距。</summary>
    public Thickness SectionGap => new(0, 0, Gap, 0);
    /// <summary>投影新快照；过期或错序事实不得覆盖更新状态。</summary>
    /// <param name="width">当前窗口的设备无关宽度。</param>
    /// <param name="height">当前窗口的设备无关高度。</param>
    public void Update(double width, double height)
    {
        var compact = width < 1320 || height < 900; var shortLayout = height < 780;
        if (compact == Compact && shortLayout == Short) return;
        Compact = compact; Short = shortLayout;
        foreach (var property in GetType().GetProperties()) Raise(property.Name);
    }
}
