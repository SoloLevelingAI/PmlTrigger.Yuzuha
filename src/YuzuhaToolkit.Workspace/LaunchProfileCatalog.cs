namespace YuzuhaToolkit.Workspace;

/// <summary>启动配置库的纯筛选与副本生成；不启动进程、不读取或复制凭据。</summary>
public static class LaunchProfileCatalog
{
    /// <summary>组合搜索、产品与收藏/最近筛选，默认配置优先展示。</summary>
    /// <param name="profiles">本工作区保存的配置。</param>
    /// <param name="query">按名称、项目和入口路径搜索的文本。</param>
    /// <param name="product">产品代码；空值表示全部产品。</param>
    /// <param name="scope">all、favorites 或 recent。</param>
    /// <returns>筛选后的配置，仍保留原对象身份。</returns>
    public static LaunchProfile[] Select(IEnumerable<LaunchProfile> profiles, string query, string? product, string scope)
    {
        if (scope is not ("all" or "favorites" or "recent")) throw new ArgumentException("Unknown profile scope.", nameof(scope));
        var term = query.Trim();
        var selected = profiles.Where(profile =>
            (term.Length == 0 || new[] { profile.Name, profile.Project, profile.Executable, profile.WorkingDirectory }
                .Any(value => value.Contains(term, StringComparison.OrdinalIgnoreCase))) &&
            (string.IsNullOrEmpty(product) || string.Equals(profile.Product, product, StringComparison.OrdinalIgnoreCase)) &&
            (scope != "favorites" || profile.IsFavorite) && (scope != "recent" || profile.LastStartedUtc is not null));
        return scope == "recent" ? selected.OrderByDescending(profile => profile.LastStartedUtc).ToArray()
            : selected.OrderByDescending(profile => profile.IsDefault).ThenBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    /// <summary>生成独立配置副本；不继承默认、启动历史或原配置的凭据引用。</summary>
    /// <param name="source">用户明确选择的源配置。</param>
    /// <returns>尚未保存的副本；密码需在编辑界面单独输入。</returns>
    public static LaunchProfile Duplicate(LaunchProfile source) => new()
    {
        Name = source.Name + " · 副本", Kind = source.Kind, Executable = source.Executable,
        Initialization = source.Initialization, Product = source.Product, WorkingDirectory = source.WorkingDirectory,
        Project = source.Project, User = source.User, Mdb = source.Mdb, Module = source.Module, License = source.License,
        Environment = new(source.Environment, StringComparer.OrdinalIgnoreCase), RememberPassword = source.RememberPassword
    };
}
