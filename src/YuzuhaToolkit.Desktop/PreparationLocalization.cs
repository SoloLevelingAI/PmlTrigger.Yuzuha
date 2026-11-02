using System.Windows.Markup;

namespace YuzuhaToolkit.Desktop;

/// <summary>XAML 中的中英文句子。默认中文，英文 UI 文化使用英文。</summary>
public sealed class BiExtension : MarkupExtension
{
    /// <summary>建立空句子，供 XAML 属性赋值。</summary>
    public BiExtension() { }

    /// <summary>建立一对中英文句子。</summary>
    public BiExtension(string zh, string en) { Zh = zh; En = en; }

    /// <summary>简体中文。</summary>
    public string Zh { get; set; } = "";
    /// <summary>英文。</summary>
    public string En { get; set; } = "";

    /// <summary>按当前 UI 文化返回一句。</summary>
    public override object ProvideValue(IServiceProvider serviceProvider) => PreparationCopy.T(Zh, En);
}
