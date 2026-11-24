namespace YuzuhaToolkit.Manager;

/// <summary>一份已交付的产品、Host profile 与框架映射。</summary>
/// <param name="Product">AM、PDMS 或 E3D。</param>
/// <param name="Profile">该产品实际交付的精确配置名。</param>
/// <param name="Framework">net35 或 net48。</param>
/// <param name="Label">界面显示名。标识仍是 <paramref name="Profile"/>。</param>
public sealed record ManagerHostProfile(string Product, string Profile, string Framework, string Label);

/// <summary>管理器所有入口共用的产品与已交付 Host 配置映射。</summary>
public static class ManagerHostProfiles
{
    /// <summary>准备工作区和其他入口只能引用这一份清单。</summary>
    public static IReadOnlyList<ManagerHostProfile> All { get; } =
    [
        new("E3D", "E3D2.1", "net48", "E3D 2.1"),
        new("E3D", "E3D3.1.6", "net48", "E3D 3.1.6"),
        new("AM", "AM", "net35", "AM"),
        new("PDMS", "PDMS", "net35", "PDMS")
    ];

    /// <summary>返回产品与精确配置对应的框架，拒绝跨产品配置及未交付版本。</summary>
    /// <param name="product">明确选择的 AM、PDMS 或 E3D 产品。</param>
    /// <param name="profile">该产品实际交付的精确 Host 配置名。</param>
    /// <returns>已支持的 net35 或 net48 框架标识。</returns>
    public static string Framework(string product, string profile) =>
        All.FirstOrDefault(item => item.Product == product && item.Profile == profile)?.Framework
        ?? throw new ArgumentException("产品与已交付的 Host profile 不匹配。");

    /// <summary>按已发现的产品版本选择默认 profile。无法对应时返回空字符串，不猜测未交付版本。</summary>
    public static string ProfileFor(string product, string? productVersion) => product switch
    {
        "AM" => "AM",
        "PDMS" => "PDMS",
        "E3D" when productVersion?.StartsWith("2.1", StringComparison.Ordinal) == true => "E3D2.1",
        "E3D" when productVersion?.StartsWith("3.1", StringComparison.Ordinal) == true => "E3D3.1.6",
        _ => ""
    };
}
