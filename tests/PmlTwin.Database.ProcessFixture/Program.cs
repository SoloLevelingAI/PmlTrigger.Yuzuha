using Microsoft.Data.Sqlite;
using PmlTwin.Database;
using PmlTwin.Database.Contracts;

var scope = new DataScope("E3D", "2.1", "APS", "configuration");
var session = SqliteRevisionStore.Open(args[0], scope);
if (args[1] == "commit")
{
    if (!session.Commit([new("profile", 0, "committed", "进程提交")]).Committed) return 2;
    return 0;
}
using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = args[0], Pooling = false }.ConnectionString);
connection.Open();
using var transaction = connection.BeginTransaction();
using var command = connection.CreateCommand();
command.Transaction = transaction;
command.CommandText = "UPDATE revisions SET content='uncommitted' WHERE identity='profile';";
command.ExecuteNonQuery();
Console.WriteLine("transaction-open");
Console.Out.Flush();
Console.ReadLine();
return 0;
