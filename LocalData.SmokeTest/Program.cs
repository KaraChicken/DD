using DDTank.LocalData;
using Microsoft.Data.Sqlite;

var root = Path.Combine(Path.GetTempPath(), "DDTank.LocalData.SmokeTest", Guid.NewGuid().ToString("N"));
var databasePath = Path.Combine(root, "data", "ddtank.sqlite3");
var backupPath = Path.Combine(root, "backups", "smoke-test.sqlite3");

try
{
    var database = new LocalSqliteDatabase(databasePath);
    database.Initialize();
    database.Initialize();

    var migrationApplied = database.ApplyMigration(1, @"
CREATE TABLE ""SmokeTest"" (
    ""Id"" INTEGER NOT NULL PRIMARY KEY,
    ""Value"" TEXT NOT NULL
);");
    if (!migrationApplied)
        throw new InvalidOperationException("The first migration was not applied.");

    if (database.ApplyMigration(1, "SELECT 1;"))
        throw new InvalidOperationException("A migration version was applied more than once.");

    using (var connection = database.OpenConnection())
    using (var command = connection.CreateCommand())
    {
        command.CommandText = @"INSERT INTO ""SmokeTest"" (""Id"", ""Value"") VALUES (1, 'portable-ok');";
        command.ExecuteNonQuery();

        command.CommandText = @"SELECT ""Value"" FROM ""SmokeTest"" WHERE ""Id"" = 1;";
        if (!string.Equals(Convert.ToString(command.ExecuteScalar()), "portable-ok", StringComparison.Ordinal))
            throw new InvalidOperationException("SQLite did not persist the inserted value.");
    }

    if (!database.VerifyIntegrity())
        throw new InvalidOperationException("The initialized database failed PRAGMA integrity_check.");

    database.CreateBackup(backupPath);
    var backup = new LocalSqliteDatabase(backupPath);
    if (!backup.VerifyIntegrity())
        throw new InvalidOperationException("The standalone backup failed PRAGMA integrity_check.");

    Console.WriteLine("PASS: initialization, transactional migrations, persistence, integrity check, and backup.");
}
finally
{
    SqliteConnection.ClearAllPools();
    if (Directory.Exists(root))
        Directory.Delete(root, recursive: true);
}
