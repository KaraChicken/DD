using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DDTank.LocalData
{
    /// <summary>
    /// SQLite implementation of the legacy account-to-first-character persistence slice.
    /// This is intentionally separate from the old SqlClient business layer until the
    /// Flash endpoint is switched over and the remaining gameplay domains are migrated.
    /// </summary>
    public sealed class CharacterCreationRepository
    {
        private const string InitialPvePermission = "FFFFFFFFFFF311111113311111111111111111111111111111";
        private readonly LocalSqliteDatabase _database;

        /// <summary>Creates the first-character repository over the supplied local database.</summary>
        public CharacterCreationRepository(LocalSqliteDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>Creates the empty local account/player tables used by the first-character flow.</summary>
        public void InitializeSchema()
        {
            _database.Initialize();
            _database.ApplyMigration(100, @"
CREATE TABLE IF NOT EXISTS Accounts (
    AccountId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    UserName TEXT NOT NULL,
    PasswordHash TEXT NOT NULL,
    Email TEXT NULL,
    CreatedUtc TEXT NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1
);
CREATE UNIQUE INDEX IF NOT EXISTS UX_Accounts_UserName
    ON Accounts(UserName COLLATE SQL_Latin1_General_CP1_CI_AS);

CREATE TABLE IF NOT EXISTS Players (
    PlayerId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    AccountId INTEGER NOT NULL UNIQUE REFERENCES Accounts(AccountId),
    UserName TEXT NOT NULL,
    NickName TEXT NOT NULL DEFAULT '',
    Sex INTEGER NOT NULL CHECK (Sex IN (0, 1)),
    GP INTEGER NOT NULL DEFAULT 1,
    Grade INTEGER NOT NULL DEFAULT 1,
    Gold INTEGER NOT NULL DEFAULT 0,
    Money INTEGER NOT NULL DEFAULT 0,
    State INTEGER NOT NULL DEFAULT 0,
    IsFirst INTEGER NOT NULL DEFAULT 0,
    Hide INTEGER NOT NULL DEFAULT 1111111111,
    Style TEXT NOT NULL DEFAULT ',,,,,,',
    Colors TEXT NOT NULL DEFAULT ',,,,,,',
    ActiveIP TEXT NOT NULL DEFAULT '',
    Skin TEXT NOT NULL DEFAULT '',
    Site TEXT NOT NULL DEFAULT '',
    PvePermission TEXT NOT NULL,
    CreatedUtc TEXT NOT NULL,
    UpdatedUtc TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS UX_Players_NickName
    ON Players(NickName COLLATE SQL_Latin1_General_CP1_CI_AS)
    WHERE NickName <> '';

CREATE TABLE IF NOT EXISTS PlayerFightStats (
    PlayerId INTEGER NOT NULL PRIMARY KEY REFERENCES Players(PlayerId) ON DELETE CASCADE,
    Attack INTEGER NOT NULL DEFAULT 0,
    Defence INTEGER NOT NULL DEFAULT 0,
    Luck INTEGER NOT NULL DEFAULT 0,
    Agility INTEGER NOT NULL DEFAULT 0,
    Delay INTEGER NOT NULL DEFAULT 0,
    Honor TEXT NOT NULL DEFAULT '',
    Map TEXT NOT NULL DEFAULT '',
    Directory TEXT NOT NULL DEFAULT '',
    IsExist INTEGER NOT NULL DEFAULT 1
);
CREATE TABLE IF NOT EXISTS PlayerVip (
    PlayerId INTEGER NOT NULL PRIMARY KEY REFERENCES Players(PlayerId) ON DELETE CASCADE,
    VipLevel INTEGER NOT NULL DEFAULT 0,
    VipExp INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS PlayerTexp (
    PlayerId INTEGER NOT NULL PRIMARY KEY REFERENCES Players(PlayerId) ON DELETE CASCADE,
    TotalTexp INTEGER NOT NULL DEFAULT 0,
    AttackTexp INTEGER NOT NULL DEFAULT 0,
    DefenceTexp INTEGER NOT NULL DEFAULT 0,
    LuckTexp INTEGER NOT NULL DEFAULT 0,
    AgilityTexp INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS DailyLogList (
    PlayerId INTEGER NOT NULL PRIMARY KEY REFERENCES Players(PlayerId) ON DELETE CASCADE,
    LastLoginUtc TEXT NULL
);
CREATE TABLE IF NOT EXISTS PlayerInventory (
    InventoryId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    PlayerId INTEGER NOT NULL REFERENCES Players(PlayerId) ON DELETE CASCADE,
    BagType INTEGER NOT NULL,
    TemplateId INTEGER NOT NULL REFERENCES ShopGoods(TemplateId),
    Place INTEGER NOT NULL,
    ItemCount INTEGER NOT NULL,
    IsJudge INTEGER NOT NULL,
    Color TEXT NOT NULL,
    IsExist INTEGER NOT NULL,
    StrengthenLevel INTEGER NOT NULL,
    AttackCompose INTEGER NOT NULL,
    DefendCompose INTEGER NOT NULL,
    LuckCompose INTEGER NOT NULL,
    AgilityCompose INTEGER NOT NULL,
    IsBinds INTEGER NOT NULL,
    BeginUtc TEXT NOT NULL,
    ValidDate INTEGER NOT NULL,
    IsUsed INTEGER NOT NULL,
    UNIQUE(PlayerId, BagType, Place)
);");
        }

        /// <summary>
        /// Inserts an account identity. PasswordHash must be produced by the account/authentication
        /// layer (for example ASP.NET Core Identity); this method never stores plaintext passwords.
        /// </summary>
        public long CreateAccount(string userName, string passwordHash, string? email = null)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new ArgumentException("An account name is required.", nameof(userName));
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException("A password hash is required.", nameof(passwordHash));

            using (var connection = _database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO Accounts(UserName, PasswordHash, Email, CreatedUtc, IsActive)
VALUES ($userName, $passwordHash, $email, $createdUtc, 1);";
                command.Parameters.AddWithValue("$userName", userName.Trim());
                command.Parameters.AddWithValue("$passwordHash", passwordHash);
                command.Parameters.AddWithValue("$email", (object?)email ?? DBNull.Value);
                command.Parameters.AddWithValue("$createdUtc", UtcNow());
                command.ExecuteNonQuery();

                command.Parameters.Clear();
                command.CommandText = "SELECT last_insert_rowid();";
                var accountId = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                transaction.Commit();
                return accountId;
            }
        }

        /// <summary>
        /// Creates the blank game-side player row on first game login, mirroring SP_Users_Active.
        /// The nickname remains empty until the Flash VisualizeRegister flow completes.
        /// </summary>
        public long CreatePendingPlayer(long accountId, bool sex, string? activeIp = null, string? site = null)
        {
            using (var connection = _database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                string userName;
                using (var account = connection.CreateCommand())
                {
                    account.Transaction = transaction;
                    account.CommandText = "SELECT UserName FROM Accounts WHERE AccountId = $accountId AND IsActive = 1;";
                    account.Parameters.AddWithValue("$accountId", accountId);
                    userName = Convert.ToString(account.ExecuteScalar(), CultureInfo.InvariantCulture)
                        ?? throw new InvalidOperationException("The account does not exist or is inactive.");
                }

                using (var existing = connection.CreateCommand())
                {
                    existing.Transaction = transaction;
                    existing.CommandText = "SELECT PlayerId FROM Players WHERE AccountId = $accountId;";
                    existing.Parameters.AddWithValue("$accountId", accountId);
                    if (existing.ExecuteScalar() != null)
                        throw new InvalidOperationException("A player record already exists for this account.");
                }

                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = @"
INSERT INTO Players(
    AccountId, UserName, NickName, Sex, GP, Grade, Gold, Money, State, IsFirst,
    Hide, Style, Colors, ActiveIP, Skin, Site, PvePermission, CreatedUtc, UpdatedUtc
)
VALUES (
    $accountId, $userName, '', $sex, 1, 1, 0, 0, 0, 0,
    1111111111, ',,,,,,', ',,,,,,', $activeIp, '', $site, $pvePermission, $createdUtc, $createdUtc
);";
                    insert.Parameters.AddWithValue("$accountId", accountId);
                    insert.Parameters.AddWithValue("$userName", userName);
                    insert.Parameters.AddWithValue("$sex", sex ? 1 : 0);
                    insert.Parameters.AddWithValue("$activeIp", activeIp ?? string.Empty);
                    insert.Parameters.AddWithValue("$site", site ?? string.Empty);
                    insert.Parameters.AddWithValue("$pvePermission", InitialPvePermission);
                    insert.Parameters.AddWithValue("$createdUtc", UtcNow());
                    insert.ExecuteNonQuery();
                }

                long playerId;
                using (var id = connection.CreateCommand())
                {
                    id.Transaction = transaction;
                    id.CommandText = "SELECT last_insert_rowid();";
                    playerId = Convert.ToInt64(id.ExecuteScalar(), CultureInfo.InvariantCulture);
                }

                using (var related = connection.CreateCommand())
                {
                    related.Transaction = transaction;
                    related.CommandText = @"
INSERT INTO PlayerFightStats(PlayerId) VALUES ($playerId);
INSERT INTO PlayerVip(PlayerId) VALUES ($playerId);
INSERT INTO PlayerTexp(PlayerId) VALUES ($playerId);
INSERT INTO DailyLogList(PlayerId) VALUES ($playerId);";
                    related.Parameters.AddWithValue("$playerId", playerId);
                    related.ExecuteNonQuery();
                }

                transaction.Commit();
                return playerId;
            }
        }

        /// <summary>
        /// Completes the Flash first-character flow. requestedSex=null preserves the pending
        /// player's existing sex, matching the legacy VisualizeRegister MustSex=false path.
        /// </summary>
        public CharacterCreationResult CompleteCharacterCreation(
            long accountId,
            string nickname,
            int? requestedSex,
            int styleDate,
            string? armColor = null,
            string? hairColor = null,
            string? faceColor = null,
            string? clothColor = null,
            string? hatColor = null)
        {
            if (string.IsNullOrWhiteSpace(nickname))
                throw new ArgumentException("A character nickname is required.", nameof(nickname));
            if (requestedSex.HasValue && requestedSex.Value != 0 && requestedSex.Value != 1)
                throw new ArgumentOutOfRangeException(nameof(requestedSex), "Sex must be 0, 1, or null.");

            var safeNickname = nickname.Trim().Replace(",", string.Empty);
            using (var connection = _database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                long playerId;
                int currentSex;
                int isFirst;
                using (var player = connection.CreateCommand())
                {
                    player.Transaction = transaction;
                    player.CommandText = "SELECT PlayerId, Sex, IsFirst FROM Players WHERE AccountId = $accountId;";
                    player.Parameters.AddWithValue("$accountId", accountId);
                    using (var reader = player.ExecuteReader())
                    {
                        if (!reader.Read())
                            return new CharacterCreationResult(3, 0);
                        playerId = reader.GetInt64(0);
                        currentSex = reader.GetInt32(1);
                        isFirst = reader.GetInt32(2);
                    }
                }

                if (isFirst > 0)
                    return new CharacterCreationResult(3, playerId);

                using (var duplicate = connection.CreateCommand())
                {
                    duplicate.Transaction = transaction;
                    duplicate.CommandText = @"
SELECT 1 FROM Players
WHERE NickName = $nickname COLLATE SQL_Latin1_General_CP1_CI_AS
  AND PlayerId <> $playerId
  AND NickName <> ''
LIMIT 1;";
                    duplicate.Parameters.AddWithValue("$nickname", safeNickname);
                    duplicate.Parameters.AddWithValue("$playerId", playerId);
                    if (duplicate.ExecuteScalar() != null)
                        return new CharacterCreationResult(2, playerId);
                }

                var sex = requestedSex ?? currentSex;
                var defaults = ReadCharacterDefaults(connection, transaction, sex);
                var arm = ReadShopGoods(connection, transaction, defaults.ArmTemplateId);
                var hair = ReadShopGoods(connection, transaction, defaults.HairTemplateId);
                var face = ReadShopGoods(connection, transaction, defaults.FaceTemplateId);
                var cloth = ReadShopGoods(connection, transaction, defaults.ClothTemplateId);
                var hat = ReadShopGoods(connection, transaction, defaults.HatTemplateId);

                if (arm.CategoryId == 0)
                    throw new InvalidOperationException("The configured starting arm has no ShopGoods category.");

                var style = ",," + hair.TemplateId + "|" + hair.Picture
                    + ",," + cloth.TemplateId + "|" + cloth.Picture
                    + "," + face.TemplateId + "|" + face.Picture
                    + "," + arm.TemplateId + "|" + arm.Picture + ",,,,";
                var colors = ",," + (hairColor ?? string.Empty)
                    + ",," + (clothColor ?? string.Empty)
                    + "," + (faceColor ?? string.Empty)
                    + "," + (armColor ?? string.Empty) + ",,,,";

                var now = UtcNow();
                InsertAppearanceItem(connection, transaction, playerId, arm.TemplateId, 6, armColor, styleDate, now);
                InsertAppearanceItem(connection, transaction, playerId, hair.TemplateId, 2, hairColor, styleDate, now);
                InsertAppearanceItem(connection, transaction, playerId, face.TemplateId, 5, faceColor, styleDate, now);
                InsertAppearanceItem(connection, transaction, playerId, cloth.TemplateId, 4, clothColor, styleDate, now);
                InsertAppearanceItem(connection, transaction, playerId, hat.TemplateId, 0, hatColor, styleDate, now);
                InsertBagInitItems(connection, transaction, playerId, sex, now);

                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = @"
UPDATE Players
SET NickName = $nickname, IsFirst = 17, Style = $style, Colors = $colors,
    Sex = $sex, UpdatedUtc = $updatedUtc
WHERE PlayerId = $playerId;";
                    update.Parameters.AddWithValue("$nickname", safeNickname);
                    update.Parameters.AddWithValue("$style", style);
                    update.Parameters.AddWithValue("$colors", colors);
                    update.Parameters.AddWithValue("$sex", sex);
                    update.Parameters.AddWithValue("$updatedUtc", now);
                    update.Parameters.AddWithValue("$playerId", playerId);
                    update.ExecuteNonQuery();
                }

                using (var stats = connection.CreateCommand())
                {
                    stats.Transaction = transaction;
                    stats.CommandText = @"
UPDATE PlayerFightStats
SET Attack = $attack, Defence = $defence, Agility = $agility, Luck = $luck
WHERE PlayerId = $playerId;";
                    stats.Parameters.AddWithValue("$attack", arm.Attack);
                    stats.Parameters.AddWithValue("$defence", arm.Defence);
                    stats.Parameters.AddWithValue("$agility", arm.Agility);
                    stats.Parameters.AddWithValue("$luck", arm.Luck);
                    stats.Parameters.AddWithValue("$playerId", playerId);
                    stats.ExecuteNonQuery();
                }

                transaction.Commit();
                return new CharacterCreationResult(0, playerId);
            }
        }

        private static CharacterDefaults ReadCharacterDefaults(SqliteConnection connection, SqliteTransaction transaction, int sex)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT ArmTemplateId, HairTemplateId, FaceTemplateId, ClothTemplateId, HatTemplateId
FROM CharacterDefaults WHERE Sex = $sex;";
                command.Parameters.AddWithValue("$sex", sex);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("No character defaults are configured for sex " + sex + ".");
                    return new CharacterDefaults(
                        reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
                        reader.GetInt32(3), reader.GetInt32(4));
                }
            }
        }

        private static ShopGoodsItem ReadShopGoods(SqliteConnection connection, SqliteTransaction transaction, int templateId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT TemplateId, Picture, CategoryId, MaxStack, Attack, Defence, Agility, Luck
FROM ShopGoods WHERE TemplateId = $templateId;";
                command.Parameters.AddWithValue("$templateId", templateId);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("Starting appearance item is missing from ShopGoods: " + templateId);
                    return new ShopGoodsItem(
                        reader.GetInt32(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4),
                        reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7));
                }
            }
        }

        private static void InsertAppearanceItem(
            SqliteConnection connection, SqliteTransaction transaction, long playerId,
            int templateId, int place, string? color, int validDate, string now)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO PlayerInventory(
    PlayerId, BagType, TemplateId, Place, ItemCount, IsJudge, Color, IsExist,
    StrengthenLevel, AttackCompose, DefendCompose, LuckCompose, AgilityCompose,
    IsBinds, BeginUtc, ValidDate, IsUsed
)
VALUES ($playerId, 0, $templateId, $place, 1, 1, $color, 1, 0, 0, 0, 0, 0, 1, $now, $validDate, 1);";
                command.Parameters.AddWithValue("$playerId", playerId);
                command.Parameters.AddWithValue("$templateId", templateId);
                command.Parameters.AddWithValue("$place", place);
                command.Parameters.AddWithValue("$color", color ?? string.Empty);
                command.Parameters.AddWithValue("$now", now);
                command.Parameters.AddWithValue("$validDate", validDate);
                command.ExecuteNonQuery();
            }
        }

        private static void InsertBagInitItems(
            SqliteConnection connection, SqliteTransaction transaction, long playerId, int sex, string now)
        {
            var items = new List<BagInitItem>();
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT b.TemplateId, b.ItemCount, b.ValidDate, b.StrengthenLevel,
       b.AttackCompose, b.DefendCompose, b.LuckCompose, b.AgilityCompose, b.IsBinds,
       CASE
           WHEN g.CategoryId BETWEEN 1 AND 9 THEN 0
           WHEN g.CategoryId IN (10, 11) THEN 1
           WHEN g.CategoryId = 12 THEN 2
           ELSE -1
       END AS BagType,
       g.MaxStack
FROM BagInit AS b
JOIN ShopGoods AS g ON g.TemplateId = b.TemplateId
WHERE b.Sex = 0 OR b.Sex = $bagSex
ORDER BY b.Id;";
                command.Parameters.AddWithValue("$bagSex", sex == 1 ? 1 : 2);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new BagInitItem(
                            reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
                            reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5),
                            reader.GetInt32(6), reader.GetInt32(7), reader.GetInt32(8) != 0,
                            reader.GetInt32(9), reader.GetInt32(10)));
                    }
                }
            }

            var nextPlace = new[] { 31, 0, 0 };
            foreach (var item in items)
            {
                if (item.MaxStack <= 0)
                    throw new InvalidOperationException("BagInit item has an invalid ShopGoods.MaxStack: " + item.TemplateId);

                var remaining = item.ItemCount;
                while (remaining > 0)
                {
                    var stackCount = Math.Min(remaining, item.MaxStack);
                    remaining -= stackCount;
                    var bagIndex = item.BagType == 0 ? 0 : item.BagType == 1 ? 1 : 2;
                    var place = nextPlace[bagIndex]++;
                    if (place > 49)
                        continue;

                    using (var insert = connection.CreateCommand())
                    {
                        insert.Transaction = transaction;
                        insert.CommandText = @"
INSERT INTO PlayerInventory(
    PlayerId, BagType, TemplateId, Place, ItemCount, IsJudge, Color, IsExist,
    StrengthenLevel, AttackCompose, DefendCompose, LuckCompose, AgilityCompose,
    IsBinds, BeginUtc, ValidDate, IsUsed
)
VALUES (
    $playerId, $bagType, $templateId, $place, $itemCount, 1, '', 1,
    $strengthen, $attack, $defence, $luck, $agility, $isBinds, $now, $validDate, 0
);";
                        insert.Parameters.AddWithValue("$playerId", playerId);
                        insert.Parameters.AddWithValue("$bagType", item.BagType);
                        insert.Parameters.AddWithValue("$templateId", item.TemplateId);
                        insert.Parameters.AddWithValue("$place", place);
                        insert.Parameters.AddWithValue("$itemCount", stackCount);
                        insert.Parameters.AddWithValue("$strengthen", item.StrengthenLevel);
                        insert.Parameters.AddWithValue("$attack", item.AttackCompose);
                        insert.Parameters.AddWithValue("$defence", item.DefendCompose);
                        insert.Parameters.AddWithValue("$luck", item.LuckCompose);
                        insert.Parameters.AddWithValue("$agility", item.AgilityCompose);
                        insert.Parameters.AddWithValue("$isBinds", item.IsBinds ? 1 : 0);
                        insert.Parameters.AddWithValue("$now", now);
                        insert.Parameters.AddWithValue("$validDate", item.ValidDate);
                        insert.ExecuteNonQuery();
                    }
                }
            }
        }

        private static string UtcNow()
        {
            return DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        }

        private sealed class CharacterDefaults
        {
            public CharacterDefaults(int arm, int hair, int face, int cloth, int hat)
            {
                ArmTemplateId = arm;
                HairTemplateId = hair;
                FaceTemplateId = face;
                ClothTemplateId = cloth;
                HatTemplateId = hat;
            }

            public int ArmTemplateId { get; }
            public int HairTemplateId { get; }
            public int FaceTemplateId { get; }
            public int ClothTemplateId { get; }
            public int HatTemplateId { get; }
        }

        private sealed class ShopGoodsItem
        {
            public ShopGoodsItem(int templateId, string picture, int categoryId, int maxStack,
                int attack, int defence, int agility, int luck)
            {
                TemplateId = templateId;
                Picture = picture;
                CategoryId = categoryId;
                MaxStack = maxStack;
                Attack = attack;
                Defence = defence;
                Agility = agility;
                Luck = luck;
            }

            public int TemplateId { get; }
            public string Picture { get; }
            public int CategoryId { get; }
            public int MaxStack { get; }
            public int Attack { get; }
            public int Defence { get; }
            public int Agility { get; }
            public int Luck { get; }
        }

        private sealed class BagInitItem
        {
            public BagInitItem(int templateId, int itemCount, int validDate, int strengthenLevel,
                int attackCompose, int defendCompose, int luckCompose, int agilityCompose,
                bool isBinds, int bagType, int maxStack)
            {
                TemplateId = templateId;
                ItemCount = itemCount;
                ValidDate = validDate;
                StrengthenLevel = strengthenLevel;
                AttackCompose = attackCompose;
                DefendCompose = defendCompose;
                LuckCompose = luckCompose;
                AgilityCompose = agilityCompose;
                IsBinds = isBinds;
                BagType = bagType;
                MaxStack = maxStack;
            }

            public int TemplateId { get; }
            public int ItemCount { get; }
            public int ValidDate { get; }
            public int StrengthenLevel { get; }
            public int AttackCompose { get; }
            public int DefendCompose { get; }
            public int LuckCompose { get; }
            public int AgilityCompose { get; }
            public bool IsBinds { get; }
            public int BagType { get; }
            public int MaxStack { get; }
        }
    }
}
