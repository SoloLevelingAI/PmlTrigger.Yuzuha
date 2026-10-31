using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using PmlTwin.Database.Contracts;

namespace PmlTwin.Database;

/// <summary>现代管理进程的应用修订库；仅显式 Open 时访问指定文件，无静态装配副作用。</summary>
public static class SqliteRevisionStore
{
    /// <summary>当前支持的应用数据库格式。</summary>
    public const int CurrentSchemaVersion = 1;
    private const int ApplicationId = 0x504D4C54;

    /// <summary>打开固定范围会话；只读模式要求文件已存在，未知格式一律拒绝。</summary>
    public static IRevisionSession Open(string path, DataScope scope, bool readOnly = false)
    {
        ArgumentNullException.ThrowIfNull(scope);
        foreach (var value in new[] { scope.Product, scope.Version, scope.Workspace, scope.Kind }) ValidateKey(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath, Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Pooling = false, DefaultTimeout = 10
        };
        // 打开既有文件时先只读检查，拒绝未知格式时不改 journal_mode 或 schema。
        if (File.Exists(fullPath))
        {
            var probeBuilder = new SqliteConnectionStringBuilder(builder.ConnectionString) { Mode = SqliteOpenMode.ReadOnly };
            using var probe = new SqliteConnection(probeBuilder.ConnectionString);
            probe.Open();
            var version = Version(probe);
            if (version != CurrentSchemaVersion) throw new DatabaseVersionException(version, CurrentSchemaVersion);
            ValidateSchema(probe);
        }
        using var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        if (!readOnly)
        {
            using var transaction = connection.BeginTransaction();
            var version = Version(connection, transaction);
            if (version == 0)
            {
                // 不接管已有其他用途的 SQLite 文件。
                using var tables = Command(connection, transaction, "SELECT count(*) FROM sqlite_master WHERE name NOT LIKE 'sqlite_%';");
                if (Convert.ToInt64(tables.ExecuteScalar(), CultureInfo.InvariantCulture) != 0)
                    throw new DatabaseVersionException(version, CurrentSchemaVersion);
                using var create = Command(connection, transaction, """
                    CREATE TABLE revisions (
                        product TEXT NOT NULL, version TEXT NOT NULL, workspace TEXT NOT NULL, kind TEXT NOT NULL,
                        identity TEXT NOT NULL, revision INTEGER NOT NULL CHECK(revision > 0),
                        content TEXT NOT NULL, sha256 TEXT NOT NULL, saved_utc TEXT NOT NULL, reason TEXT NOT NULL,
                        PRIMARY KEY(product,version,workspace,kind,identity,revision));
                    PRAGMA user_version=1;
                    PRAGMA application_id=1347243092;
                    """);
                create.ExecuteNonQuery();
            }
            else if (version != CurrentSchemaVersion) throw new DatabaseVersionException(version, CurrentSchemaVersion);
            ValidateSchema(connection, transaction);
            transaction.Commit();
        }
        if (Version(connection) != CurrentSchemaVersion) throw new DatabaseVersionException(Version(connection), CurrentSchemaVersion);
        ValidateSchema(connection);
        return new Session(builder.ConnectionString, scope, readOnly);
    }

    private static int Version(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = Command(connection, transaction, "PRAGMA user_version;");
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql; return command;
    }

    private static void ValidateSchema(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var identity = Command(connection, transaction, "PRAGMA application_id;");
        if (Convert.ToInt32(identity.ExecuteScalar(), CultureInfo.InvariantCulture) != ApplicationId)
            throw new InvalidDataException("数据库应用身份不匹配；拒绝接管其他用途的数据文件。");
        using var table = Command(connection, transaction, "PRAGMA table_info(revisions);");
        using var reader = table.ExecuteReader();
        var actual = new List<string>();
        while (reader.Read()) actual.Add($"{reader.GetString(1)}:{reader.GetString(2)}:{reader.GetInt32(3)}:{reader.GetInt32(5)}");
        string[] expected = ["product:TEXT:1:1", "version:TEXT:1:2", "workspace:TEXT:1:3", "kind:TEXT:1:4", "identity:TEXT:1:5", "revision:INTEGER:1:6",
            "content:TEXT:1:0", "sha256:TEXT:1:0", "saved_utc:TEXT:1:0", "reason:TEXT:1:0"];
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidDataException("数据库修订表的列或主键不兼容；拒绝读取或写入。");
    }

    private static void ValidateKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.Any(char.IsControl))
            throw new ArgumentException("范围与对象身份必须为 1–256 字符且不能包含控制字符。");
    }

    /// <summary>在固定产品、版本和工作区范围内读取修订，并用 SQLite 事务执行预期修订比较与原子提交。</summary>
    private sealed class Session(string connectionString, DataScope scope, bool readOnly) : IRevisionSession
    {
        /// <summary>此会话所有查询和写入共用的数据隔离范围。</summary>
        public DataScope Scope => scope;
        /// <summary>每次打开连接时要求匹配的数据库结构版本。</summary>
        public int SchemaVersion => CurrentSchemaVersion;
        /// <summary>是否禁止提交；只读会话仍可查询当前或历史修订。</summary>
        public bool ReadOnly => readOnly;

        private SqliteConnection Connect()
        {
            var connection = new SqliteConnection(connectionString);
            try
            {
                connection.Open();
                var version = Version(connection);
                if (version != CurrentSchemaVersion) throw new DatabaseVersionException(version, CurrentSchemaVersion);
                ValidateSchema(connection);
                return connection;
            }
            catch { connection.Dispose(); throw; }
        }

        /// <summary>读取范围内指定对象的最新修订；对象不存在时返回 null。</summary>
        public RevisionSnapshot? Read(string identity)
        {
            ValidateKey(identity);
            using var connection = Connect();
            return ReadCore(connection, null, identity, null);
        }

        /// <summary>读取指定对象的正整数修订号；未找到该修订时返回 null。</summary>
        public RevisionSnapshot? ReadRevision(string identity, long revision)
        {
            ValidateKey(identity);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
            using var connection = Connect();
            return ReadCore(connection, null, identity, revision);
        }

        /// <summary>验证并冻结 1–100 项写入，在同一事务中核对全部预期修订；冲突返回当前快照且不提交任何项。</summary>
        public RevisionCommit Commit(IReadOnlyList<RevisionWrite> writes)
        {
            if (readOnly) throw new InvalidOperationException("只读数据库会话不能写入。");
            ArgumentNullException.ThrowIfNull(writes);
            // 冻结调用者可变集合；验证所有输入后才开启事务。
            var batch = writes.ToArray();
            if (batch.Length is < 1 or > 100) throw new ArgumentException("批次必须包含 1–100 个对象。", nameof(writes));
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var write in batch)
            {
                ArgumentNullException.ThrowIfNull(write);
                ValidateKey(write.Identity);
                if (!keys.Add(write.Identity)) throw new ArgumentException("同一批次不能重复写入一个对象。", nameof(writes));
                ArgumentOutOfRangeException.ThrowIfNegative(write.ExpectedRevision);
                ArgumentNullException.ThrowIfNull(write.Content);
                if (Encoding.UTF8.GetByteCount(write.Content) > 1024 * 1024) throw new ArgumentException("修订文本不能超过 1 MiB。");
                if (string.IsNullOrWhiteSpace(write.Reason) || write.Reason.Length > 1000) throw new ArgumentException("请提供 1–1000 字符的提交原因。");
            }
            using var connection = Connect();
            // BEGIN IMMEDIATE 在读取 CAS 前取得 SQLite 写租约，跨实例与跨进程并发使用同一约束。
            using var transaction = connection.BeginTransaction(deferred: false);
            var version = Version(connection, transaction);
            if (version != CurrentSchemaVersion) throw new DatabaseVersionException(version, CurrentSchemaVersion);
            ValidateSchema(connection, transaction);
            var snapshots = new List<RevisionSnapshot>();
            foreach (var write in batch)
            {
                var current = ReadCore(connection, transaction, write.Identity, null);
                if ((current?.Revision ?? 0) != write.ExpectedRevision)
                    return new(false, Array.Empty<RevisionSnapshot>(), write.Identity, current);
                var next = new RevisionSnapshot(write.Identity, checked(write.ExpectedRevision + 1), write.Content,
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(write.Content))).ToLowerInvariant(), DateTimeOffset.UtcNow, write.Reason);
                using var command = Command(connection, transaction, """
                    INSERT INTO revisions(product,version,workspace,kind,identity,revision,content,sha256,saved_utc,reason)
                    VALUES($product,$version,$workspace,$kind,$identity,$revision,$content,$sha256,$saved,$reason);
                    """);
                AddScope(command, write.Identity);
                command.Parameters.AddWithValue("$revision", next.Revision);
                command.Parameters.AddWithValue("$content", next.Content);
                command.Parameters.AddWithValue("$sha256", next.Sha256);
                command.Parameters.AddWithValue("$saved", next.SavedUtc.ToString("O", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$reason", next.Reason);
                command.ExecuteNonQuery(); snapshots.Add(next);
            }
            transaction.Commit();
            return new(true, snapshots.AsReadOnly());
        }

        private void AddScope(SqliteCommand command, string identity)
        {
            command.Parameters.AddWithValue("$product", scope.Product);
            command.Parameters.AddWithValue("$version", scope.Version);
            command.Parameters.AddWithValue("$workspace", scope.Workspace);
            command.Parameters.AddWithValue("$kind", scope.Kind);
            command.Parameters.AddWithValue("$identity", identity);
        }

        private RevisionSnapshot? ReadCore(SqliteConnection connection, SqliteTransaction? transaction, string identity, long? revision)
        {
            using var command = Command(connection, transaction, """
                SELECT revision,content,sha256,saved_utc,reason FROM revisions
                WHERE product=$product AND version=$version AND workspace=$workspace AND kind=$kind AND identity=$identity
                """ + (revision.HasValue ? " AND revision=$revision" : "") + " ORDER BY revision DESC LIMIT 1;");
            AddScope(command, identity);
            if (revision.HasValue) command.Parameters.AddWithValue("$revision", revision.Value);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return null;
            var content = reader.GetString(1);
            var hash = reader.GetString(2);
            if (!string.Equals(hash, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant(), StringComparison.Ordinal))
                throw new InvalidDataException("修订内容 SHA-256 不匹配；拒绝使用损坏快照。");
            return new(identity, reader.GetInt64(0), content, hash,
                DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), reader.GetString(4));
        }
    }
}
