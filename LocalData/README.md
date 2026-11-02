# DDTank.LocalData

This is the first embedded-database foundation for the Windows Portable edition.

- Targets .NET Standard 2.0 so it can be referenced by modern .NET and compatible .NET Framework projects.
- Uses bundled SQLite through Microsoft.Data.Sqlite; no SQL Server service is required.
- Stores the default database at `%LOCALAPPDATA%/DDTank/data/ddtank.sqlite3`.
- Initializes a schema migration ledger, applies versioned migrations transactionally, checks database integrity, and creates consistent standalone backups.
- Does **not** yet replace the legacy `SqlDataProvider` or migrate the old stored procedures. That work depends on the metadata inventory and domain-by-domain compatibility tests.

Run the smoke test with:

```powershell
dotnet run --project LocalData.SmokeTest/LocalData.SmokeTest.csproj
```
