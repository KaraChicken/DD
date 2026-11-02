# DDTank ASP.NET Core Web Host

This is the replacement local web host being introduced alongside the legacy PHP site.

## Current scope

- ASP.NET Core Razor Pages host targeting .NET 10, published self-contained for win-x64.
- Loopback-only default binding at `http://127.0.0.1:5080`.
- Embedded SQLite database initialized by the host; no SQL Server service or database installation is required by this component.
- Bundled character-reference seed is copied into the per-user database on first run.
- Database files are stored outside the application folder at `%LOCALAPPDATA%\DDTank\data\ddtank.sqlite3`.
- `/health/live` reports process liveness; `/health/ready` checks SQLite integrity and required character-reference data.
- The host wires the first-character SQLite repository into dependency injection, but account endpoints and the Flash-compatible `VisualizeRegister.ashx` endpoint are not yet connected.

## Run locally

```powershell
dotnet run --project WebModern/DDTank.Web.csproj
```

Open `http://127.0.0.1:5080`. The database is created under the current Windows user's LocalAppData directory. When the reference seed is present under `WebModern/Seeds`, it is imported automatically.

## Current limitations

This is not yet the complete game launcher or game server. The legacy Game.Server, Center, and Fighting services still use SQL Server through SqlClient. The new SQLite repository covers account-row creation, pending player creation, and the first-character transaction only; other gameplay persistence must be migrated before the complete game can run without SQL Server.

The host does not yet expose account registration/login, password verification, game tickets, or the Flash-compatible first-character HTTP endpoint. These will be implemented against the traced request/response contracts; no fake-success endpoints are included. Payment workflows remain out of scope.
