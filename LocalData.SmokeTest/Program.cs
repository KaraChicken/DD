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
CREATE TABLE CharacterDefaults (
    Sex INTEGER NOT NULL PRIMARY KEY,
    ArmTemplateId INTEGER NOT NULL,
    HairTemplateId INTEGER NOT NULL,
    FaceTemplateId INTEGER NOT NULL,
    ClothTemplateId INTEGER NOT NULL,
    HatTemplateId INTEGER NOT NULL
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
INSERT INTO ShopGoods VALUES
    (1001, 'smoke.png', 1, 99, 1, 2, 3, 4),
    (7008, 'dart', 7, 1, 60, 30, 250, 40),
    (3158, 'hair58', 3, 1, 5, 5, 5, 5),
    (6103, 'face2', 6, 1, 5, 5, 5, 5),
    (5160, 'cloth60', 5, 1, 5, 5, 5, 5),
    (1142, 'head51', 1, 1, 5, 5, 5, 5),
    (3244, 'hair44', 3, 1, 5, 5, 5, 5),
    (6204, 'face3', 6, 1, 5, 5, 5, 5),
    (5276, 'cloth76', 5, 1, 5, 5, 5, 5),
    (1214, 'head13', 1, 1, 5, 5, 5, 5);
INSERT INTO CharacterDefaults VALUES (1, 7008, 3158, 6103, 5160, 1142);
INSERT INTO CharacterDefaults VALUES (0, 7008, 3244, 6204, 5276, 1214);
PRAGMA user_version = 2;";
                seedCommand.ExecuteNonQuery();
            }
        }
    }

    long expectedShopGoods;
    long expectedBagInit;
    long expectedCharacterDefaults;
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
            if (Convert.ToInt32(seedCommand.ExecuteScalar()) != 2)
                throw new InvalidOperationException("Unexpected character reference seed schema version.");

            seedCommand.CommandText = "SELECT COUNT(*) FROM ShopGoods;";
            expectedShopGoods = Convert.ToInt64(seedCommand.ExecuteScalar());

            seedCommand.CommandText = "SELECT COUNT(*) FROM BagInit;";
            expectedBagInit = Convert.ToInt64(seedCommand.ExecuteScalar());

            seedCommand.CommandText = "SELECT COUNT(*) FROM CharacterDefaults;";
            expectedCharacterDefaults = Convert.ToInt64(seedCommand.ExecuteScalar());
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

        referenceCommand.CommandText = "SELECT COUNT(*) FROM CharacterDefaults;";
        if (Convert.ToInt64(referenceCommand.ExecuteScalar()) != expectedCharacterDefaults)
            throw new InvalidOperationException("CharacterDefaults rows were not imported exactly.");
    }

    var characterRepository = new CharacterCreationRepository(database);
    characterRepository.InitializeSchema();

    var firstAccountId = characterRepository.CreateAccount("first.player", "test-password-hash");
    var firstTicket = "first-player-game-ticket";
    var firstPlayerId = characterRepository.CreatePendingPlayer(firstAccountId, firstTicket, true, "127.0.0.1", "portable");
    if (!characterRepository.VerifyGameTicket(firstAccountId, firstTicket) ||
        characterRepository.VerifyGameTicket(firstAccountId, "not-the-ticket"))
        throw new InvalidOperationException("Short-lived game ticket validation failed.");

    var invalidTicket = characterRepository.CompleteCharacterCreation(firstAccountId, "wrong-ticket", "WrongTicket", 1, 3);
    if (invalidTicket.ResultCode != 3)
        throw new InvalidOperationException("Character creation accepted an invalid game ticket.");

    var firstCharacter = characterRepository.CompleteCharacterCreation(firstAccountId, firstTicket, "TestPlayer", 1, 3);
    if (!firstCharacter.Success || firstCharacter.PlayerId != firstPlayerId)
        throw new InvalidOperationException("The first character was not created successfully.");

    using (var characterConnection = database.OpenConnection())
    using (var characterCommand = characterConnection.CreateCommand())
    {
        characterCommand.CommandText = "SELECT IsExist FROM Players WHERE PlayerId = $playerId;";
        characterCommand.Parameters.AddWithValue("$playerId", firstPlayerId);
        if (Convert.ToInt32(characterCommand.ExecuteScalar()) != 1)
            throw new InvalidOperationException("The pending player did not preserve the legacy IsExist flag.");

        characterCommand.Parameters.Clear();
        characterCommand.CommandText = "SELECT COUNT(*) FROM PlayerInventory WHERE PlayerId = $playerId;";
        characterCommand.Parameters.AddWithValue("$playerId", firstPlayerId);
        if (Convert.ToInt32(characterCommand.ExecuteScalar()) != 5)
            throw new InvalidOperationException("The first character did not receive the five configured appearance items.");

        characterCommand.Parameters.Clear();
        characterCommand.CommandText = "SELECT Attack, Defence, Agility, Luck FROM PlayerFightStats WHERE PlayerId = $playerId;";
        characterCommand.Parameters.AddWithValue("$playerId", firstPlayerId);
        using (var stats = characterCommand.ExecuteReader())
        {
            if (!stats.Read() || stats.GetInt32(0) != 60 || stats.GetInt32(1) != 30 ||
                stats.GetInt32(2) != 250 || stats.GetInt32(3) != 40)
                throw new InvalidOperationException("The starting combat attributes were not copied from the configured arm item.");
        }
    }

    var secondAccountId = characterRepository.CreateAccount("second.player", "test-password-hash");
    var secondTicket = "second-player-game-ticket";
    var secondPlayerId = characterRepository.CreatePendingPlayer(secondAccountId, secondTicket, false, "127.0.0.1", "portable");
    characterRepository.UpdateGameTicket(secondAccountId, "rotated-second-player-ticket");
    if (characterRepository.VerifyGameTicket(secondAccountId, secondTicket) ||
        !characterRepository.VerifyGameTicket(secondAccountId, "rotated-second-player-ticket"))
        throw new InvalidOperationException("Game ticket rotation did not invalidate the previous ticket.");
    var duplicateNickname = characterRepository.CompleteCharacterCreation(secondAccountId, "rotated-second-player-ticket", "testplayer", 0, 3);
    if (duplicateNickname.ResultCode != 2)
        throw new InvalidOperationException("Nickname uniqueness did not match the case-insensitive SQL Server collation.");

    var alreadyCreated = characterRepository.CompleteCharacterCreation(firstAccountId, firstTicket, "AnotherName", 1, 3);
    if (alreadyCreated.ResultCode != 3)
        throw new InvalidOperationException("The repository allowed a second character creation for the same account.");

    var thirdAccountId = characterRepository.CreateAccount("third.player", "test-password-hash");
    var femaleTicket = "female-player-game-ticket";
    var femalePlayerId = characterRepository.CreatePendingPlayer(thirdAccountId, femaleTicket, false, "127.0.0.1", "portable");
    var femaleCharacter = characterRepository.CompleteCharacterCreation(thirdAccountId, femaleTicket, "FemaleTest", 0, 3);
    if (!femaleCharacter.Success || femaleCharacter.PlayerId != femalePlayerId)
        throw new InvalidOperationException("The female first-character flow did not complete.");

    using (var femaleConnection = database.OpenConnection())
    using (var femaleCommand = femaleConnection.CreateCommand())
    {
        femaleCommand.CommandText = "SELECT Style FROM Players WHERE PlayerId = $playerId;";
        femaleCommand.Parameters.AddWithValue("$playerId", femalePlayerId);
        var femaleStyle = Convert.ToString(femaleCommand.ExecuteScalar()) ?? string.Empty;
        if (!femaleStyle.Contains("3244|hair44", StringComparison.Ordinal) ||
            !femaleStyle.Contains("6204|face3", StringComparison.Ordinal))
            throw new InvalidOperationException("The female default appearance set was not applied.");

        femaleCommand.Parameters.Clear();
        femaleCommand.CommandText = "SELECT COUNT(*) FROM PlayerInventory WHERE PlayerId = $playerId;";
        femaleCommand.Parameters.AddWithValue("$playerId", femalePlayerId);
        if (Convert.ToInt32(femaleCommand.ExecuteScalar()) != 5)
            throw new InvalidOperationException("The female character did not receive the five configured appearance items.");
    }

    using (var pendingConnection = database.OpenConnection())
    using (var pendingCommand = pendingConnection.CreateCommand())
    {
        pendingCommand.CommandText = "SELECT NickName FROM Players WHERE PlayerId = $playerId;";
        pendingCommand.Parameters.AddWithValue("$playerId", secondPlayerId);
        if (!string.Equals(Convert.ToString(pendingCommand.ExecuteScalar()), string.Empty, StringComparison.Ordinal))
            throw new InvalidOperationException("A rejected duplicate nickname partially modified the pending player.");
    }

    var migrationApplied = database.ApplyMigration(999, @"
CREATE TABLE ""SmokeTest"" (
    ""Id"" INTEGER NOT NULL PRIMARY KEY,
    ""Value"" TEXT NOT NULL
);");
    if (!migrationApplied)
        throw new InvalidOperationException("The first migration was not applied.");

    if (database.ApplyMigration(999, "SELECT 1;"))
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

        backupCommand.CommandText = "SELECT COUNT(*) FROM CharacterDefaults;";
        if (Convert.ToInt64(backupCommand.ExecuteScalar()) != expectedCharacterDefaults)
            throw new InvalidOperationException("The standalone backup did not retain character defaults.");
        
        backupCommand.CommandText = "SELECT COUNT(*) FROM Players;";
        if (Convert.ToInt32(backupCommand.ExecuteScalar()) != 3)
            throw new InvalidOperationException("The standalone backup did not retain player progress.");

        backupCommand.CommandText = "SELECT COUNT(*) FROM PlayerInventory;";
        if (Convert.ToInt32(backupCommand.ExecuteScalar()) != 10)
            throw new InvalidOperationException("The standalone backup did not retain both players' starting inventories.");
    }

    Console.WriteLine($"PASS: seed import ({expectedShopGoods} ShopGoods, {expectedBagInit} BagInit, {expectedCharacterDefaults} CharacterDefaults), pending player, male/female character creation, nickname uniqueness, migrations, persistence, integrity check, and backup.");
}
finally
{
    SqliteConnection.ClearAllPools();
    if (Directory.Exists(root))
        Directory.Delete(root, recursive: true);
}
