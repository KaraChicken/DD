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

## Account and character persistence call-path clues

Source inspection found multiple legacy paths that must not be conflated:
- `MemberShipBussiness.CreateUsername` calls `Mem_Users_CreateUser` using the separate `membershipDb` setting; `CheckUsername` calls `Check_User`.
- `PlayerBussiness.ActivePlayer` calls `SP_Users_Active` and passes username, password, and sex while initializing a blank player record.
- `PlayerBussiness.RegisterPlayer` calls `SP_Users_RegisterNotValidate` with username, password, nickname, sex, appearance item IDs, and colors.
- `PlayerBussiness.RegisterUser` also calls `SP_Account_Register` with username, password, nickname, sex, and starting currencies.

These are source-level call sites, not proof of which path the current Flash SWF invokes. The Flash packet/handler trace must identify the active path before implementing SQLite equivalents. The new website must not take over the in-game nickname/gender UI just because an older SQL procedure accepts those fields.

## Backup inspection

The repository currently contains three MSSQL backup files in `Db/`. Their canonical role and schema overlap are not yet established.

The `Database backup inspection` GitHub Actions workflow restores each backup into a disposable SQL Server 2022 Developer container and exports metadata only:
- logical backup file names and file types;
- user table/view/procedure/trigger inventory;
- table column names and SQL types;
- a static scan of C# files that reference SqlClient and stored procedure calls.

It must not export table rows, user credentials, or restored database files. The SQL Server container and its data are deleted at the end of the job. The inventory artifact is short-lived and for migration planning only.

## First successful backup inventory (2026-11-02)

The isolated SQL Server 2022 restore workflow completed successfully for all three backups. The uploaded artifact contains metadata only and expires after seven days. It did not upload database files or table rows.

| Backup | Restored logical database | User tables | Views | Stored procedures | User table columns |
|---|---|---:|---:|---:|---:|
| `Db_Membership_20200702.BAK` | `Db_Membership_Data` | 34 | 7 | 59 | 285 |
| `Db_Tank41.BAK` | `DDTMixTankAc` | 71 | 15 | 300 | 902 |
| `Db_Tank_20200702.BAK` | `Db_Tank` | 80 | 0 | 162 | 820 |

Additional findings:

The restored databases also have different SQL Server compatibility/collation settings: membership is compatibility level 100 with `Chinese_PRC_CI_AS`, game state is level 100 with `SQL_Latin1_General_CP1_CI_AS`, and reference data is level 130 with `Latin1_General_CI_AS`. SQLite defaults do not reproduce these comparison rules automatically. Nickname/account uniqueness and text ordering must be explicitly defined and tested (including case/accent behavior) during the port.
- The three backups are not duplicates. They have different schemas and logical database names.
- The membership backup contains account/website-oriented tables such as `Mem_Users`, `Mem_UserInfo`, password reset and webshop tables. Do not bundle its historical user rows into a fresh single-player release.
- `Db_Tank41` contains live player/game state tables such as `Sys_User_Info`, `Sys_Users_Detail`, `Sys_Users_Goods`, `Sys_Users_Pet`, `Sys_Users_Friends`, `QuestData`, `Consortia`, and `User_Messages`.
- `Db_Tank` contains game reference/configuration tables such as `LevelInfo`, `Game_Map`, `Quest`, `NPC_Info`, `Pet_Template_Info`, `Item_Strengthen`, `Shop_Goods`, and `Achievement`.
- The static source scan examined 4,171 C# files, found 43 files directly referencing SqlClient, and identified 153 distinct stored-procedure names by pattern scan. The scan is not exhaustive: procedure names built dynamically or hidden behind helper methods may not be detected.
- Legacy `Center.Service/App.config` also references a `Db_Count` database, but no separate `Db_Count` backup is present in the current `Db/` directory listing. Its role must be traced; it must not silently be mapped to another database.

These counts describe database objects, not required portable features. The next step is to trace the actual startup and gameplay call graph and classify the procedures/tables into required, optional, and obsolete domains. The current data inventory is too large to justify a wholesale automatic SQLite conversion.

The second metadata pass also recorded per-table row counts (counts only, no values):
- `Db_Membership_20200702.BAK` contains 24 rows in `Mem_Users`, 24 in `Mem_UserInfo`, and 24 in `Webshop_Account`.
- `Db_Tank41.BAK` contains 24 rows in each of `Sys_Users_Detail`, `Sys_Users_Fight`, `Sys_Users_Texp`, and `Sys_VIP_Info`; it also contains 1,481 `Sys_Users_Goods` rows, 28 `Sys_Users_Pet` rows, 13 `Sys_Users_Friends` rows, and 384 `User_Messages` rows.
- The `Db_Tank` backup is largely reference/configuration data (for example, 390 `Game_Map`, 467 `Quest`, 962 `NPC_Info`, and 10,427 `Shop_Goods` rows), but it still needs a table-by-table seed-data allowlist.

This confirms that at least two backups contain historical account/player-associated records. They are explicitly excluded from the portable seed database: the product is a fresh single-player rebuild and does not need those identities or saves. Only reviewed schema and allowlisted static game configuration/reference rows may be migrated. The three backups must remain private migration inputs and must not be included in player downloads.

## First-character creation flow: server endpoint traced

The checked-in server code now identifies the exact HTTP endpoint used by the Flash-side first-character flow:

1. `Login.ashx` decrypts the Flash login payload and calls `BaseInterface.CreateLogin`.
2. `BaseInterface.CreateLogin` calls `PlayerBussiness.LoginGame`. If no game-side player row exists, it calls `PlayerBussiness.ActivePlayer`, which invokes `SP_Users_Active` to create a pending player with an empty nickname and related fight/VIP/experience/daily-log rows.
3. `LoginSelectList.ashx` returns only player rows with a non-empty nickname. The Flash `SelectListManager.setup` marks the account as a newbie when this role list is empty.
4. The server-side first-character creation endpoint is `VisualizeRegister.ashx` (`Tank.Request.VisualizeRegister`). It reads `Name`, `Pass`, `NickName`, and `Sex`; validates nickname byte length and illegal characters; selects gender-specific default appearance IDs from `BoyVisualizeItem` / `GrilVisualizeItem`; then calls `PlayerBussiness.RegisterPlayer`.
5. `PlayerBussiness.RegisterPlayer` invokes `SP_Users_RegisterNotValidate`, which performs the nickname uniqueness check, loads appearance and starter-item definitions, creates initial equipment/inventory, and updates nickname, sex, style, colors, and fight attributes in one transaction.
6. The regular `Login.ashx` response then returns the populated `PlayerInfo` to Flash.

The compiled `VisualizeRegister` source reads `MustSex`, `BoyVisualizeItem`, and `GrilVisualizeItem` from the root `Request/Web.config`. `MustSex=true`; for the selected sex it takes the first semicolon-delimited group of five template IDs and uses those as the default arm, hair, face, cloth, and hat. All ten configured IDs were verified to exist in the restored `Db_Tank.dbo.Shop_Goods` table. The filtered SQLite seed now includes a `CharacterDefaults` table with the two sex-specific five-item sets (seed schema version 2). A separate nested copy under `Request/Tank.Request` contains an older, different four-slot handler/config variant; it must not be mistaken for the compiled source flow. The currently configured defaults are boy: `7008,3158,6103,5160,1142`; girl: `7008,3244,6204,5276,1214`.

The checked-in ActionScript confirms the empty-role/newbie decision and existing-role selector. The exact ActionScript caller for `VisualizeRegister.ashx` has not yet been found in the source tree, but the endpoint contract and C# persistence path are now verified. The handler returns an XML `<Result value="..." message="..."/>` response, strips commas from the nickname, enforces a 14-byte nickname limit, applies the configured illegal-character list, and uses the sex-specific default item IDs. Preserve this flow: website registration creates the account identity; Flash owns the gender/nickname screen; `VisualizeRegister.ashx` / the game-side business layer owns first-character creation. Do not move that UI into ASP.NET Core.

The source also references `Check_User`, `SP_CheckAccount`, and `SP_Account_Register`, but none of those procedure names exists in any of the three supplied backups. They appear to be stale/alternate paths or dependencies from an absent database/version. The migration must not recreate them from guesses; trace whether they are actually called in the active login/creation flow. Conversely, `Mem_Code_Sto` is present and is called by `Mem_Users_CreateUser` to generate account IDs, so its definition is included in the expanded inspection.

## Character-creation transaction dependencies found in the backup

The selected procedure source reveals the concrete database contract behind the first-time player flow:

- `Mem_Users_CreateUser` creates the website/account identity in `Mem_Users` and `Mem_UserInfo`. It depends on `Mem_Application`, `Mem_Application_User`, and `Mem_Code_Sto` for application lookup, duplicate detection, and ID generation.
- `SP_Users_Active` creates the initial game-side player row in `Sys_Users_Detail`, then creates related rows in `Sys_Users_Fight`, `Sys_VIP_Info`, `Sys_Users_Texp`, and `DailyLogList`, in one SQL Server transaction. The nickname is initially empty.
- `SP_Users_RegisterNotValidate` is the actual character-profile operation in this backup: it checks nickname uniqueness and first-creation state; reads gender-specific appearance item data from `Db_Tank.dbo.Shop_Goods`; reads starting inventory from `Db_Tank41.dbo.Bag_Init`; creates inventory rows in `Sys_Users_Goods`; then updates `Sys_Users_Detail` (nickname, sex, style, colors, first-login state) and `Sys_Users_Fight` (combat attributes), transactionally.
- This is a cross-database SQL Server operation involving both the game database and the reference database. A SQLite implementation must place the required reference tables in the local database (or attach a second SQLite file deliberately) and preserve the entire operation as one transaction. It must not split character creation into partially committed writes.
- The procedure's nickname check is a database-level uniqueness check; the SQLite port must enforce uniqueness under the intended collation, not only in UI validation.

A useful migration caveat from the row-count inventory: `Db_Tank41.dbo.Bag_Init` exists but contains **zero rows** in the supplied backup. The character-creation procedure reads it to populate the starter bag, so a direct port would currently produce no additional Bag_Init-derived items (the procedure still inserts the selected appearance equipment directly). This needs a gameplay decision/test; do not invent starter items or silently substitute another table.

This is a much narrower and more actionable first migration slice than attempting to translate all 300 game stored procedures at once. The next implementation target is the account/first-character path, after confirming the exact Flash packet handler and the live service call sequence.

## CI inspection artifact

The initial schema inventory passed in [workflow run 41420505591](https://github.com/KaraChicken/DD/actions/runs/41420505591). The expanded inspection, including selected account/character stored-procedure definitions, per-table row counts, and a filtered SQLite seed, passed in [workflow run 41423542854](https://github.com/KaraChicken/DD/actions/runs/41423542854). Artifacts (seven-day retention): [schema/procedure inventory](https://github.com/KaraChicken/DD/actions/runs/41423542854/artifacts/13130568443) and [character-reference SQLite seed](https://github.com/KaraChicken/DD/actions/runs/41423542854/artifacts/13130368921). The seed contains 10,427 `ShopGoods` rows and zero `BagInit` rows; its SQLite integrity check passed. No account/player rows, credentials, or restored SQL Server database files are included. Re-run the workflow to regenerate these artifacts.

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

The legacy solution's Windows Release build also passed after correcting stale service project references in `Road.Service.csproj` and removing invalid Debug-output analyzer entries from `Fighting.Service.csproj`. The successful baseline is [run 41422074766](https://github.com/KaraChicken/DD/actions/runs/41422074766). This verifies compilation only; it does not imply that the old server can start without SQL Server.

## Embedded SQLite foundation implementation

A new `LocalData/DDTank.LocalData.csproj` library now targets .NET Standard 2.0 and uses `Microsoft.Data.Sqlite` with an explicitly pinned SQLitePCLRaw native bundle. It provides:
- default per-user path `%LOCALAPPDATA%/DDTank/data/ddtank.sqlite3`;
- first-run database creation, WAL mode, foreign-key enforcement, and a versioned migration ledger;
- transactional, one-time schema migrations;
- SQLite integrity checks;
- consistent standalone backup creation without overwriting an existing backup.

The `LocalData.SmokeTest` project exercises initialization, repeat initialization, migration idempotency, insert/read persistence, integrity verification, and standalone backup verification. This passed on a Windows GitHub Actions runner in [run 41421724645](https://github.com/KaraChicken/DD/actions/runs/41421724645) with zero build warnings and a successful smoke-test result.

Important boundary: this library is a tested foundation, **not yet wired into the legacy Game.Server/Bussiness code**. The legacy services still use SqlClient and SQL Server stored procedures. The next integration work depends on the account/character procedure definitions and a reviewed reference-data allowlist; do not treat the current server as SQL-free yet.

## Release and data safety

- Never include `.BAK`, `.mdf`, `.ldf`, `.ndf`, credentials, or restored user data in release artifacts.
- The original backups remain migration inputs only until the migration is verified. Do not delete or rewrite them as part of this work.
- Existing connection strings contain SQL Server host/user/password literals in legacy service configuration. Treat these as exposed legacy configuration, not valid production secrets; rotate any real credentials and replace them with local generated configuration before distributing any build.
- The legacy Request Web.config files also contain an external payment URL with a hard-coded token/signature. Payments are out of scope for the single-player edition; do not copy this configuration into the portable package, and rotate the token if it is still valid.
- A release is not SQL-free until a clean Windows runner can start the complete portable game, create a character through the original Flash flow, persist game state, restart, and restore a user backup without any SQL Server installation.
