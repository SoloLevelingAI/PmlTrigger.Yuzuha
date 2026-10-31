namespace RibbonStudio
{
    public partial class Version
    {
        public string retention
        {
            get
            {
                return kind == "edit" ? "最近 5 次草稿" : "长期快照";
            }
        }
    }
    public partial class Target
    {
        public override string ToString()
        {
            return directory;
        }
    }
    public partial class Entry
    {
        public string permission
        {
            get
            {
                return origin == "managed" ? "托管 · 可管理加载" : origin == "custom" ? "自定义 · 可编辑" : "只读保护";
            }
        }
    }
    public partial class ModuleRow
    {
        public bool Existing
        {
            get
            {
                return entryNames != null && entryNames.Count > 0;
            }
        }
        public bool Editable
        {
            get
            {
                return !Existing && System.String.IsNullOrEmpty(error);
            }
        }
        public string State
        {
            get
            {
                return Existing ? "已关联" : !System.String.IsNullOrEmpty(error) ? "无法读取" : Selected ? "待加入" : "未关联";
            }
        }
    }
}
