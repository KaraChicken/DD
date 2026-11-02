using DDTank.LocalData;
using Microsoft.Data.Sqlite;

var root = Path.Combine(Path.GetTempPath(), "DDTank.LocalData.SmokeTest", Guid.NewGuid().ToString("N"));
var databasePath = Path.Combine(root, "data", "ddtank.sqlite3");
var backupPath = Path.Combine(root, "backups", "smoke-test.sqlite3");
var suppliedSeedPath = args.Length > 0 ? Path.GetFullPath(args[0]) : null;
var seedPath = suppliedSeedPath ?? Path.Combine(root, "character-reference-seed.sqlite3");

try
{
    var database = new LocalSqliteDatabase(databasePath);
    database.Initialize();
    database.Initialize();

    if (suppliedSeedPath == null)
    {
        Directory.CreateDirectory(root);
        var seedBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = seedPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };
        using (var seedConnection = new SqliteConnection(seedBuilder.ToString()))
        {
            seedConnection.Open();
            using (var seedCommand = seedConnection.CreateCommand())
            {
                seedCommand.CommandText = @"
CREATE TABLE ShopGoods (
    TemplateId INTEGER NOT NULL PRIMARY KEY,
    Picture TEXT NULL,
    CategoryId INTEGER NOT NULL,
    MaxStack INTEGER NOT NULL,
    Attack INTEGER NOT NULL,
    Defence INTEGER NOT NULL,
    Agility INTEGER NOT NULL,
    Luck INTEGER NOT NULL
);
CREATE TABLE BagInit (
    Id INTEGER NOT NULL PRIMARY KEY,
    Sex INTEGER NOT NULL,
    TemplateId INTEGER NOT NULL,
    ItemCount INTEGER NOT NULL,
    ValidDate INTEGER NOT NULL,
    StrengthenLevel INTEGER NOT NULL,
    AttackCompose INTEGER NOT NULL,
    DefendCompose INTEGER NOT NULL,
    LuckCompose INTEGER NOT NULL,
    AgilityCompose INTEGER NOT NULL,
    IsBinds INTEGER NOT NULL,
    BagType INTEGER NOT NULL,
    Place INTEGER NOT NULL
);
INSERT INTO ShopGoods VALUES (1001, 'smoke.png', 1, 99, 1, 2, 3, 4);
PRAGMA user_version = 1;";
                seedCommand.ExecuteNonQuery();
            }
        }
    }

    long expectedShopGoods;
    long expectedBagInit;
    var seedBuilderForRead = new SqliteConnectionStringBuilder
    {
        DataSource = seedPath,
        Mode = SqliteOpenMode.ReadOnly,
        Pooling = false
    };
    using (var seedConnection = new SqliteConnection(seedBuilderForRead.ToString()))
    {
        seedConnection.Open();
        using (var seedCommand = seedConnection.CreateCommand())
        {
            seedCommand.CommandText = "PRAGMA user_version;";
            if (Convert.ToInt32(seedCommand.ExecuteScalar()) != 1)
                throw new InvalidOperationException("Unexpected character reference seed schema version.");

            seedCommand.CommandText = "SELECT COUNT(*) FROM ShopGoods;";
            expectedShopGoods = Convert.ToInt64(seedCommand.ExecuteScalar());

            seedCommand.CommandText = "SELECT COUNT(*) FROM BagInit;";
            expectedBagInit = Convert.ToInt64(seedCommand.ExecuteScalar());
        }
    }

    if (expectedShopGoods <= 0)
        throw new InvalidOperationException("The character reference seed contains no ShopGoods rows.");

    database.ImportCharacterReferenceSeed(seedPath);
    database.ImportCharacterReferenceSeed(seedPath);

    using (var referenceConnection = database.OpenConnection())
    using (var referenceCommand = referenceConnection.CreateCommand())
    {
        referenceCommand.CommandText = "SELECT COUNT(*) FROM ShopGoods;";
        if (Convert.ToInt64(referenceCommand.ExecuteScalar()) != expectedShopGoods)
            throw new InvalidOperationException("ShopGoods reference rows were not imported exactly.");

        referenceCommand.CommandText = "SELECT COUNT(*) FROM BagInit;";
        if (Convert.ToInt64(referenceCommand.ExecuteScalar()) != expectedBagInit)
            throw new InvalidOperationException("BagInit reference rows were not imported exactly.");
    }

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
    using (var backupConnection = backup.OpenConnection())
    using (var backupCommand = backupConnection.CreateCommand())
    {
        backupCommand.CommandText = "SELECT COUNT(*) FROM ShopGoods;";
        if (Convert.ToInt64(backupCommand.ExecuteScalar()) != expectedShopGoods)
            throw new InvalidOperationException("The standalone backup did not retain imported reference data.");
    }

    Console.WriteLine($"PASS: seed import ({expectedShopGoods} ShopGoods, {expectedBagInit} BagInit), transactional migrations, persistence, integrity check, and backup.");
}
finally
{
    SqliteConnection.ClearAllPools();
    if (Directory.Exists(root))
        Directory.Delete(root, recursive: true);
}
