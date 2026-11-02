using System;
using System.Globalization;
using System.IO;
using DDTank.LocalData;

namespace DDTank.Web.Infrastructure
{
    /// <summary>Initializes and reports readiness for the per-user embedded SQLite database.</summary>
    public sealed class LocalDatabaseRuntime
    {
        public LocalDatabaseRuntime(string dataDirectory, string referenceSeedPath)
        {
            if (string.IsNullOrWhiteSpace(dataDirectory))
                throw new ArgumentException("A local data directory is required.", nameof(dataDirectory));

            DataDirectory = Path.GetFullPath(dataDirectory);
            DatabasePath = Path.Combine(DataDirectory, "ddtank.sqlite3");
            Directory.CreateDirectory(DataDirectory);

            Database = new LocalSqliteDatabase(DatabasePath);
            Database.Initialize();

            ReferenceSeedPath = Path.GetFullPath(referenceSeedPath);
            if (File.Exists(ReferenceSeedPath))
                Database.ImportCharacterReferenceSeed(ReferenceSeedPath);

            Characters = new CharacterCreationRepository(Database);
            Characters.InitializeSchema();
        }

        /// <summary>Gets the user-writable directory containing the local database.</summary>
        public string DataDirectory { get; }

        /// <summary>Gets the path of the local SQLite database.</summary>
        public string DatabasePath { get; }

        /// <summary>Gets the bundled reference seed path.</summary>
        public string ReferenceSeedPath { get; }

        /// <summary>Gets the initialized local database.</summary>
        public LocalSqliteDatabase Database { get; }

        /// <summary>Gets the first-character persistence repository.</summary>
        public CharacterCreationRepository Characters { get; }

        /// <summary>Checks database integrity and the presence of required character reference data.</summary>
        public bool IsReady(out string description)
        {
            try
            {
                if (!Database.VerifyIntegrity())
                {
                    description = "SQLite integrity_check failed.";
                    return false;
                }

                using (var connection = Database.OpenConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM ShopGoods;";
                    var shopGoodsCount = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);

                    command.CommandText = "SELECT COUNT(*) FROM CharacterDefaults;";
                    var defaultsCount = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);

                    if (shopGoodsCount <= 0 || defaultsCount != 2)
                    {
                        description = "SQLite is initialized, but the character reference seed is missing or incomplete.";
                        return false;
                    }

                    description = "SQLite integrity and character reference data are ready.";
                    return true;
                }
            }
            catch (Exception exception)
            {
                description = "SQLite readiness check failed: " + exception.Message;
                return false;
            }
        }
    }
}
