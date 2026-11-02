# Database migration plan: zero-install Windows edition

## Product requirement

The Windows Portable edition must start without the player installing SQL Server, SQL Server Express, LocalDB, or running a manual database restore. The launcher will own first-run database initialization, service startup, backup, and graceful shutdown. Mutable files belong under `%LOCALAPPDATA%/DDTank`, never beside the executable.

## What source inspection has confirmed

- The legacy data layer is not a thin connection-string wrapper. `SqlDataProvider/DAL/SqlHelper.cs` directly uses `System.Data.SqlClient`, `SqlConnection`, `SqlCommand`, `SqlTransaction`, `SqlDataAdapter`, and stored-procedure execution.
- `Bussiness/BaseBussiness.cs` creates `Sql_DbObject("AppConfig", "conString")`, so the old business layer expects a SQL Server connection string from application settings.
- `SqlDataProvider/BaseClass/Sql_DbObject.cs` executes both raw T-SQL and stored procedures through `SqlCommand`; it also relies on SQL Server parameter metadata and `SqlDataAdapter`.
- `PlayerBussiness.ActivePlayer` invokes `SP_Users_Active` with output and return-value parameters. This is part of the in-game character creation/persistence path, not a website-only operation.
- `Center.Service/App.config` references `Db_Tank41`, `Db_Tank`, and `Db_Count`. The exact runtime necessity of each database still needs tracing before any service or database is removed.
- Therefore, replacing `SqlClient` with `Microsoft.Data.Sqlite` or changing connection strings alone cannot work. Stored procedures and T-SQL behavior must be migrated deliberately.

## Backup inspection

The repository currently contains three MSSQL backup files in `Db/`. Their canonical role and schema overlap are not yet established.

The `Database backup inspection` GitHub Actions workflow restores each backup into a disposable SQL Server 2022 Developer container and exports metadata only:
- logical backup file names and file types;
- user table/view/procedure/trigger inventory;
- table column names and SQL types;
- a static scan of C# files that reference SqlClient and stored procedure calls.

It must not export table rows, user credentials, or restored database files. The SQL Server container and its data are deleted at the end of the job. The inventory artifact is short-lived and for migration planning only.

## Target architecture

Preferred target: SQLite, using a bundled managed provider, with one or more local database files under `%LOCALAPPDATA%/DDTank/data`. SQLite is embedded in the application process and does not require a database server or Windows service.

Do not assume a single SQLite file is automatically sufficient. First use the metadata inventory and runtime dependency tracing to classify:
1. immutable game configuration/reference data;
2. account and player save data;
3. runtime/session coordination data;
4. logs, audit and statistics;
5. cross-zone or multi-server features that may be unnecessary in single-player mode.

The single-player edition should migrate only data required by the retained game flows. External payment, PicPay, remote cross-zone services, and multi-server statistics should be excluded only after their call paths are confirmed unused by the portable runtime.

## Migration sequence

1. Restore and inventory all three backups in isolated CI; determine which backup contains the canonical game schema and which objects are required by the running services.
2. Trace data calls from launcher startup, account login, Flash character creation, game entry, battle completion, inventory, mail, quest, and save/shutdown.
3. Build a schema migration tool that copies only required schema and seed/reference rows from the selected backup. Do not migrate unknown user/player rows as bundled seed data.
4. Add a new SQLite persistence implementation behind a bounded interface. Keep the existing SQL Server implementation available as a temporary test oracle while migrating one domain at a time.
5. Replace stored procedures with parameterized SQLite commands and explicit transactions. Preserve output values, identity generation, uniqueness constraints, ordering, null semantics, and transaction boundaries.
6. Add integration tests for account creation, in-game character creation, item/inventory changes, battle rewards, reconnect, restart persistence, and backup/restore.
7. Switch the portable launcher to initialize SQLite on first run, validate schema version, run migrations transactionally, and keep user saves/backups under the user data directory.
8. Remove SQL Server dependencies from the portable package only after the full retained gameplay path passes clean-machine tests.

## Release and data safety

- Never include `.BAK`, `.mdf`, `.ldf`, `.ndf`, credentials, or restored user data in release artifacts.
- The original backups remain migration inputs only until the migration is verified. Do not delete or rewrite them as part of this work.
- Existing connection strings contain SQL Server host/user/password literals in legacy service configuration. Treat these as exposed legacy configuration, not valid production secrets; rotate any real credentials and replace them with local generated configuration before distributing any build.
- A release is not SQL-free until a clean Windows runner can start the complete portable game, create a character through the original Flash flow, persist game state, restart, and restore a user backup without any SQL Server installation.
