using System;
using System.Data;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace DDTank.LocalData
{
    /// <summary>
    /// Owns an embedded SQLite database stored in the current user's data directory.
    /// This is the portable edition's new persistence foundation; it does not adapt
    /// legacy SQL Server stored procedures automatically.
    /// </summary>
    public sealed class LocalSqliteDatabase
    {
        private readonly string _databasePath;

        public LocalSqliteDatabase(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("A database path is required.", nameof(databasePath));

            _databasePath = Path.GetFullPath(databasePath);
        }

        public string DatabasePath => _databasePath;

        public static string GetDefaultDatabasePath()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
                throw new InvalidOperationException("The Windows LocalApplicationData directory is unavailable.");

            return Path.Combine(localAppData, "DDTank", "data", "ddtank.sqlite3");
        }

        /// <summary>
        /// Opens a configured connection. Foreign-key enforcement is enabled per connection.
        /// </summary>
        public SqliteConnection OpenConnection()
        {
            var directory = Path.GetDirectoryName(_databasePath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("The database path must have a parent directory.");

            Directory.CreateDirectory(directory);

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                Pooling = true,
                DefaultTimeout = 30,
                ForeignKeys = true
            };

            var connection = new SqliteConnection(builder.ToString());
            try
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
                    command.ExecuteNonQuery();
                }

                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Creates the database and migration ledger on first run. Safe to call on every launch.
        /// </summary>
        public void Initialize()
        {
            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
                command.ExecuteNonQuery();

                command.CommandText = @"
CREATE TABLE IF NOT EXISTS ""__SchemaMigrations"" (
    ""Version"" INTEGER NOT NULL PRIMARY KEY,
    ""AppliedUtc"" TEXT NOT NULL
);";
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Applies one versioned SQL migration exactly once, in a transaction.
        /// Returns false when that version has already been applied.
        /// </summary>
        public bool ApplyMigration(int version, string sql)
        {
            if (version <= 0)
                throw new ArgumentOutOfRangeException(nameof(version), "Migration versions must be positive.");
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("Migration SQL is required.", nameof(sql));

            using (var connection = OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                using (var check = connection.CreateCommand())
                {
                    check.Transaction = transaction;
                    check.CommandText = "SELECT 1 FROM ""__SchemaMigrations"" WHERE ""Version"" = $version LIMIT 1;";
                    check.Parameters.AddWithValue("$version", version);
                    if (check.ExecuteScalar() != null)
                    {
                        transaction.Commit();
                        return false;
                    }
                }

                using (var migration = connection.CreateCommand())
                {
                    migration.Transaction = transaction;
                    migration.CommandText = sql;
                    migration.ExecuteNonQuery();
                }

                using (var record = connection.CreateCommand())
                {
                    record.Transaction = transaction;
                    record.CommandText = @"
INSERT INTO ""__SchemaMigrations"" (""Version"", ""AppliedUtc"")
VALUES ($version, $appliedUtc);";
                    record.Parameters.AddWithValue("$version", version);
                    record.Parameters.AddWithValue("$appliedUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                    record.ExecuteNonQuery();
                }

                transaction.Commit();
                return true;
            }
        }

        /// <summary>
        /// Uses SQLite's online backup API to create a consistent standalone backup.
        /// Existing destination files are never overwritten.
        /// </summary>
        public void CreateBackup(string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(destinationPath))
                throw new ArgumentException("A backup path is required.", nameof(destinationPath));

            var fullDestination = Path.GetFullPath(destinationPath);
            if (string.Equals(fullDestination, _databasePath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The backup path must differ from the live database path.", nameof(destinationPath));
            if (!File.Exists(_databasePath))
                throw new FileNotFoundException("The local database has not been initialized.", _databasePath);
            if (File.Exists(fullDestination))
                throw new IOException("The backup destination already exists; choose a new backup file name.");

            var directory = Path.GetDirectoryName(fullDestination);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("The backup path must have a parent directory.");
            Directory.CreateDirectory(directory);

            var temporaryPath = fullDestination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var destinationBuilder = new SqliteConnectionStringBuilder
                {
                    DataSource = temporaryPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Pooling = false
                };

                using (var source = OpenConnection())
                using (var destination = new SqliteConnection(destinationBuilder.ToString()))
                {
                    destination.Open();
                    source.BackupDatabase(destination);
                }

                File.Move(temporaryPath, fullDestination);
            }
            catch
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
                throw;
            }
        }

        /// <summary>
        /// Returns true only when SQLite reports a clean database.
        /// </summary>
        public bool VerifyIntegrity()
        {
            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA integrity_check;";
                var result = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
