namespace YuzuhaToolkit.Mcp;

/// <summary>
///     Deprecated, unused contract for raw BFS rows, retained for source compatibility.
///     No implementation is shipped. New integrations must use the existing query
///     tools without depending on this optional cache contract.
/// </summary>
/// <remarks>
/// Deprecated since 2026-10-23; see docs/reference/deprecations.md. The public
/// contract remains available without a compiler warning, so existing consumers
/// that treat warnings as errors can still build during the migration period.
/// </remarks>
public interface IBfsCache
{
    /// <summary>为 PML 命令与全局变量计算一致的缓存键。</summary>
    /// <param name="pmlCommand">用户明确要求执行或查询的完整 PML 文本。</param>
    /// <param name="globalVar">用于承载结果的 PML 全局数组变量名。</param>
    /// <param name="deleteGlobalVar">读取结束时是否清理该结果全局变量。</param>
    string ComputeKey(
        string pmlCommand,
        string globalVar,
        bool deleteGlobalVar);

    /// <summary>尝试读取缓存中的原始 BFS 行，不执行命令。</summary>
    /// <param name="key">由命令和目标全局变量计算出的缓存键。</param>
    IReadOnlyList<string>? TryGet(string key);

    /// <summary>按键保存原始 BFS 行，供后续只读解析复用。</summary>
    /// <param name="key">由命令和目标全局变量计算出的缓存键。</param>
    /// <param name="rows">原始 BFS 行，后续解析保留其来源内容。</param>
    void Put(string key, IReadOnlyList<string> rows);
}
