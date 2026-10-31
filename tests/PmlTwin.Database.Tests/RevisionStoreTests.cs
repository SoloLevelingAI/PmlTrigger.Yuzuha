using System.Diagnostics;
using Microsoft.Data.Sqlite;
using PmlTwin.Database;
using PmlTwin.Database.Contracts;
using Xunit;

namespace PmlTwin.Database.Tests;

public sealed class RevisionStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "pmltwin-database-tests", Guid.NewGuid().ToString("N"));
    private readonly DataScope scope = new("E3D", "2.1", "APS", "configuration");
    private string DatabasePath => Path.Combine(directory, "application.db");
    public RevisionStoreTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public async Task IndependentConnectionsRaceOnSameExpectedRevision()
    {
        var first = SqliteRevisionStore.Open(DatabasePath, scope);
        first.Commit([new("profile", 0, "old", "首次")]);
        using var ready = new CountdownEvent(2);
        using var start = new ManualResetEventSlim();
        Task<RevisionCommit> Writer(string value) => Task.Run(() =>
        {
            var session = SqliteRevisionStore.Open(DatabasePath, scope);
            ready.Signal(); start.Wait();
            return session.Commit([new("profile", 1, value, "并发")]);
        });
        var a = Writer("GUI"); var b = Writer("CLI");
        Assert.True(ready.Wait(TimeSpan.FromSeconds(15))); start.Set();
        var results = await Task.WhenAll(a, b);
        Assert.Single(results, r => r.Committed);
        var loser = Assert.Single(results, r => !r.Committed);
        Assert.Equal(2, loser.Current!.Revision);
        Assert.Equal(first.Read("profile"), loser.Current);
        Assert.Equal("old", first.ReadRevision("profile", 1)!.Content);
        Assert.Null(first.ReadRevision("profile", 3));
    }

    [Fact]
    public void ConflictAfterEarlierInsertRollsBackEntireBatch()
    {
        var session = SqliteRevisionStore.Open(DatabasePath, scope);
        session.Commit([new("existing", 0, "old", "首次")]);
        var result = session.Commit([new("new", 0, "candidate", "批次"), new("existing", 0, "wrong", "冲突")]);
        Assert.False(result.Committed); Assert.Equal("existing", result.ConflictIdentity);
        Assert.Empty(result.Snapshots); Assert.Null(session.Read("new"));
        Assert.Equal("old", session.Read("existing")!.Content);
    }

    [Fact]
    public void SqliteFailureAfterEarlierInsertRollsBackEntireBatch()
    {
        var session = SqliteRevisionStore.Open(DatabasePath, scope);
        using (var connection = Raw())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TRIGGER reject_bad BEFORE INSERT ON revisions WHEN NEW.identity='bad' BEGIN SELECT RAISE(ABORT,'fixture failure'); END;";
            command.ExecuteNonQuery();
        }
        Assert.Throws<SqliteException>(() => session.Commit([new("good", 0, "content", "批次"), new("bad", 0, "content", "失败") ]));
        Assert.Null(session.Read("good")); Assert.Null(session.Read("bad"));
        Assert.True(session.Commit([new("good", 0, "retry", "显式重试")]).Committed);
    }

    [Fact]
    public void ScopeAndHistoryAreIsolatedAndReopenPreservesRevisions()
    {
        var first = SqliteRevisionStore.Open(DatabasePath, scope);
        first.Commit([new("profile", 0, "first", "首次")]);
        first.Commit([new("profile", 1, "second", "修改")]);
        foreach (var other in new[] { scope with { Product = "AM" }, scope with { Version = "12.1.SP4" }, scope with { Workspace = "OTHER" }, scope with { Kind = "document" } })
        {
            var session = SqliteRevisionStore.Open(DatabasePath, other);
            Assert.Null(session.Read("profile"));
            Assert.True(session.Commit([new("profile", 0, "isolated", "隔离")]).Committed);
        }
        var reopened = SqliteRevisionStore.Open(DatabasePath, scope);
        Assert.Equal("second", reopened.Read("profile")!.Content);
        Assert.Equal("first", reopened.ReadRevision("profile", 1)!.Content);
        Assert.Equal(64, reopened.Read("profile")!.Sha256.Length);
    }

    [Fact]
    public void ReadOnlySessionReadsHistoryAndRefusesWrites()
    {
        SqliteRevisionStore.Open(DatabasePath, scope).Commit([new("profile", 0, "original", "首次")]);
        var session = SqliteRevisionStore.Open(DatabasePath, scope, readOnly: true);
        Assert.True(session.ReadOnly); Assert.Equal(1, session.SchemaVersion);
        Assert.Equal("original", session.ReadRevision("profile", 1)!.Content);
        Assert.Throws<InvalidOperationException>(() => session.Commit([new("profile", 1, "changed", "拒绝") ]));
        Assert.Equal("original", session.Read("profile")!.Content);
    }

    [Fact]
    public void FutureSchemaRefusesOpenAndExistingSessionWriteWithoutChangingBytes()
    {
        var session = SqliteRevisionStore.Open(DatabasePath, scope);
        session.Commit([new("profile", 0, "original", "首次")]);
        using (var connection = Raw())
        using (var command = connection.CreateCommand()) { command.CommandText = "PRAGMA user_version=99;"; command.ExecuteNonQuery(); }
        var bytes = File.ReadAllBytes(DatabasePath);
        Assert.Throws<DatabaseVersionException>(() => SqliteRevisionStore.Open(DatabasePath, scope));
        Assert.Throws<DatabaseVersionException>(() => SqliteRevisionStore.Open(DatabasePath, scope, true));
        Assert.Throws<DatabaseVersionException>(() => session.Commit([new("profile", 1, "wrong", "拒绝") ]));
        Assert.Equal(bytes, File.ReadAllBytes(DatabasePath));
    }

    [Fact]
    public void UnknownExistingDatabaseIsNotAdopted()
    {
        using (var connection = Raw())
        using (var command = connection.CreateCommand()) { command.CommandText = "CREATE TABLE foreign_data(value TEXT);"; command.ExecuteNonQuery(); }
        var bytes = File.ReadAllBytes(DatabasePath);
        Assert.Throws<DatabaseVersionException>(() => SqliteRevisionStore.Open(DatabasePath, scope));
        Assert.Equal(bytes, File.ReadAllBytes(DatabasePath));
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("missing-column")]
    [InlineData("wrong-primary-key")]
    public void VersionOneForeignOrMalformedSchemaIsRejectedBeforeSessionCreation(string defect)
    {
        SqliteRevisionStore.Open(DatabasePath, scope);
        using (var connection = Raw())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = defect switch
            {
                "foreign" => "PRAGMA application_id=123;",
                "missing-column" => "ALTER TABLE revisions DROP COLUMN reason;",
                _ => """
                    DROP TABLE revisions;
                    CREATE TABLE revisions(product TEXT NOT NULL,version TEXT NOT NULL,workspace TEXT NOT NULL,kind TEXT NOT NULL,
                        identity TEXT NOT NULL,revision INTEGER NOT NULL,content TEXT NOT NULL,sha256 TEXT NOT NULL,
                        saved_utc TEXT NOT NULL,reason TEXT NOT NULL,PRIMARY KEY(identity,revision));
                    """
            };
            command.ExecuteNonQuery();
        }
        var bytes = File.ReadAllBytes(DatabasePath);
        Assert.Throws<InvalidDataException>(() => SqliteRevisionStore.Open(DatabasePath, scope));
        Assert.Throws<InvalidDataException>(() => SqliteRevisionStore.Open(DatabasePath, scope, true));
        Assert.Equal(bytes, File.ReadAllBytes(DatabasePath));
    }

    [Fact]
    public void InterruptedUninitializedFileRequiresExplicitRecoveryInsteadOfAdoption()
    {
        File.WriteAllBytes(DatabasePath, []);
        Assert.Throws<DatabaseVersionException>(() => SqliteRevisionStore.Open(DatabasePath, scope));
        Assert.Empty(File.ReadAllBytes(DatabasePath));
    }

    [Fact]
    public void ContentTamperingIsDetected()
    {
        var session = SqliteRevisionStore.Open(DatabasePath, scope);
        session.Commit([new("profile", 0, "original", "首次")]);
        using (var connection = Raw())
        using (var command = connection.CreateCommand()) { command.CommandText = "UPDATE revisions SET content='tampered';"; command.ExecuteNonQuery(); }
        Assert.Throws<InvalidDataException>(() => session.Read("profile"));
        Assert.Throws<InvalidDataException>(() => session.ReadRevision("profile", 1));
    }

    [Fact]
    public async Task ProcessRestartPreservesCommitAndKilledTransactionRecovers()
    {
        using (var commit = StartFixture("commit"))
        {
            await commit.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(0, commit.ExitCode);
        }
        using (var crash = StartFixture("crash"))
        {
            try
            {
                Assert.Equal("transaction-open", await crash.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20)));
            }
            finally
            {
                if (!crash.HasExited) crash.Kill(entireProcessTree: true);
                await crash.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            }
        }
        var recovered = SqliteRevisionStore.Open(DatabasePath, scope);
        Assert.Equal("committed", recovered.Read("profile")!.Content);
        Assert.True(recovered.Commit([new("profile", 1, "after-recovery", "恢复后提交")]).Committed);
        Assert.Equal("committed", recovered.ReadRevision("profile", 1)!.Content);
    }

    [Fact]
    public void ContractsAndImplementationDoNotReferenceUiAvevaOrDeployment()
    {
        var contracts = typeof(IRevisionSession).Assembly;
        var implementation = typeof(SqliteRevisionStore).Assembly;
        Assert.NotEqual(contracts, implementation);
        Assert.Equal("PmlTwin.Database.dll", Path.GetFileName(implementation.Location));
        Assert.DoesNotContain(contracts.GetReferencedAssemblies(), r => r.Name!.Contains("Sqlite", StringComparison.OrdinalIgnoreCase));
        foreach (var assembly in new[] { contracts, implementation })
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), r => new[] { "Aveva", "Presentation", "WindowsBase", "Workspace", "Manager", "Desktop", "Setup" }.Any(forbidden => r.Name!.Contains(forbidden, StringComparison.OrdinalIgnoreCase)));
        Assert.All(typeof(IRevisionSession).GetMethods().SelectMany(m => m.GetParameters()).Select(p => p.ParameterType), type => Assert.DoesNotContain("Sqlite", type.FullName!));
    }

    private SqliteConnection Raw()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ConnectionString);
        connection.Open(); return connection;
    }

    private Process StartFixture(string mode)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("PMLTWIN_TEST_DOTNET") ?? "C:/Users/26648/.dotnet/dotnet.exe")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "DatabaseFixture", "PmlTwin.Database.ProcessFixture.dll"));
        start.ArgumentList.Add(DatabasePath); start.ArgumentList.Add(mode);
        return Process.Start(start)!;
    }
}
