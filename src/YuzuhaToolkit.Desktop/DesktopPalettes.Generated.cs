// Generated from ion/themes.json SHA256 10d4c99d74b8dc28076018bbbf876ea0f617fe182dd6d5d6611f9408baf59753
namespace YuzuhaToolkit.Desktop;

/// <summary>来自同一ION设计源的原生主题身份。</summary>
/// <param name="Id">持久化主题标识。</param>
/// <param name="Name">显示名称。</param>
/// <param name="Scheme">原生窗口使用的明暗模式。</param>
internal sealed record DesktopPalette(string Id, string Name, string Scheme);

/// <summary>Web、FigCraft与WPF共同设计源的生成目录。</summary>
internal static class DesktopPalettes
{
    /// <summary>六套ION主题，旧Light/Dark身份由主题服务保持兼容。</summary>
    internal static readonly DesktopPalette[] All =
    [
        new("ion-lime", "离子柠檬", "Light"),
        new("ion-cyan", "冰川青电", "Light"),
        new("ion-violet", "紫外频段", "Light"),
        new("ion-orange", "熔橙航道", "Light"),
        new("ion-rose", "脉冲玫粉", "Light"),
        new("ion-graphite", "石墨夜航", "Dark"),
    ];
}
