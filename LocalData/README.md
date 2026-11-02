# DDTank.LocalData

The embedded persistence layer for the Windows Portable edition.

- Targets .NET Standard 2.0 so it can be referenced by modern .NET and compatible .NET Framework projects.
- Uses bundled SQLite through Microsoft.Data.Sqlite; no SQL Server service is required by this library.
- Stores the default database at `%LOCALAPPDATA%/DDTank/data/ddtank.sqlite3`.
- Initializes a schema migration ledger, applies versioned migrations, checks database integrity, and creates consistent standalone backups.
- Imports the filtered, static character-reference seed (`ShopGoods`, `BagInit`, and sex-specific `CharacterDefaults`). The seed contains no accounts, credentials, or player rows.

## Implemented account and first-character slice

- `CreateAccount` stores a password hash supplied by the authentication layer; plaintext passwords are never persisted.
- `CreatePendingPlayer` creates the blank-nickname player and its initial fight, VIP, Texp, and daily-log records.
- A 256-bit random game ticket is stored only as a SHA-256 hash with a five-minute expiry. `UpdateGameTicket` rotates it, and `VerifyGameTicket` checks it.
- `CompleteCharacterCreation` verifies the ticket and mirrors the legacy first-character stored procedure: it applies the male/female default appearance set, inserts the five starting appearance items, copies starting weapon stats, processes any `BagInit` rows using the legacy bag placement/stacking rules, and updates the nickname/style in one transaction.
- Legacy result codes are preserved: 0 success, 2 duplicate nickname, 3 no pending character / already completed / invalid ticket.
- Nickname uniqueness uses a case-insensitive, accent-sensitive .NET collation approximation of `SQL_Latin1_General_CP1_CI_AS`; exact SQL Server Unicode equivalence still needs broader compatibility tests.

## Validation

`LocalData.SmokeTest` runs against both a synthetic seed and the actual filtered seed generated from the MSSQL backup. It tests account creation, pending players, ticket validation/rotation, male and female character creation, duplicate nickname rejection, persistence, integrity, and backup/restore.

```powershell
dotnet run --project LocalData.SmokeTest/LocalData.SmokeTest.csproj
dotnet run --project LocalData.SmokeTest/LocalData.SmokeTest.csproj -- path/to/character-reference-seed.sqlite3
```

## Remaining migration work

This is a bounded first migration slice, not a replacement for the legacy `SqlDataProvider`, `Bussiness`, or all stored procedures. The legacy Game.Server, Center, and Fighting services still use SqlClient/SQL Server and do not yet validate the new SQLite game tickets. The Flash-compatible web endpoint is implemented in `WebModern`, but the SWF login handshake and game-server socket authentication still need to be connected to the ticket flow before the complete game can run without SQL Server.
