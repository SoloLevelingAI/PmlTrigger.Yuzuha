namespace YuzuhaToolkit.Manager;

/// <summary>管理器所有入口共用的产品与已交付 Host 配置映射。</summary>
public static class ManagerHostProfiles
{
    /// <summary>返回产品与精确配置对应的框架，拒绝跨产品配置及未交付版本。</summary>
    /// <param name="product">明确选择的 AM、PDMS 或 E3D 产品。</param>
    /// <param name="profile">该产品实际交付的精确 Host 配置名。</param>
    /// <returns>已支持的 net35 或 net48 框架标识。</returns>
    public static string Framework(string product, string profile) => (product, profile) switch
    {
        ("AM", "AM") or ("PDMS", "PDMS") => "net35",
        ("E3D", "E3D2.1") or ("E3D", "E3D3.1.6") => "net48",
        _ => throw new ArgumentException("产品与已交付的 Host profile 不匹配。")
    };
}
