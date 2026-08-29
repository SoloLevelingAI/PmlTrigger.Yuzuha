namespace YuzuhaToolkit.Mcp;

/// <summary>
///     Optional cache contract for raw BFS rows. The SQLite implementation is
///     intentionally excluded from this delivery until storage and deployment
///     policy are decided. Consumers must work when no implementation is present.
/// </summary>
public interface IBfsCache
{
    string ComputeKey(
        string pmlCommand,
        string globalVar,
        bool deleteGlobalVar);

    IReadOnlyList<string>? TryGet(string key);

    void Put(string key, IReadOnlyList<string> rows);
}