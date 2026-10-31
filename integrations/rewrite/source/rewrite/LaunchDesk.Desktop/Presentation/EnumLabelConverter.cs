using System.Globalization;

namespace LaunchDesk.Desktop;

/// <summary>将配置枚举转换为界面可读文字。</summary>
public sealed class EnumLabelConverter : IValueConverter
{
    /// <summary>把已知入口、包来源或变量作用域枚举转换为中文标签。</summary>
    /// <param name="value">待显示的枚举或文本值。</param>
    /// <param name="targetType">WPF 目标类型；本转换器返回文本。</param>
    /// <param name="parameter">WPF 转换参数；本转换器不使用此值。</param>
    /// <param name="culture">WPF 区域信息；标签采用固定中文说明。</param>
    /// <returns>已知枚举的中文标签；其他值使用其文本，空值返回空字符串。</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value?.ToString() switch
    {
        "Monitor" => "mon.exe · PROD / INIT", "MarineBatch" => "Marine · BAT / CMD", "Executable" => "通用 EXE",
        "Zip" => "ZIP 压缩包", "SevenZip" => "7z 压缩包", "Folder" => "完整文件夹", "Files" => "普通文件（可多选）",
        "Process" => "仅启动进程", "User" => "当前用户", "Machine" => "系统（需管理员）", _ => value?.ToString() ?? ""
    };
    /// <summary>忽略界面文本到枚举的反向转换，保留原绑定源值。</summary>
    /// <param name="value">界面提供的文本；不参与写回。</param>
    /// <param name="targetType">绑定源目标类型；不参与写回。</param>
    /// <param name="parameter">WPF 转换参数；不参与写回。</param>
    /// <param name="culture">WPF 区域信息；不参与写回。</param>
    /// <returns>固定返回 Binding.DoNothing。</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
