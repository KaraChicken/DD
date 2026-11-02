# DDTank ASP.NET Core Web Host

This is the replacement local web host being introduced alongside the legacy PHP site.

## Current scope

- ASP.NET Core Razor Pages host targeting .NET 10, published self-contained for win-x64.
- Loopback-only default binding at `http://127.0.0.1:5080`.
- Embedded SQLite database initialized by the host; no SQL Server service or database installation is required by this component.
- Bundled character-reference seed is copied into the per-user database on first run.
- Database files are stored outside the application folder at `%LOCALAPPDATA%\DDTank\data\ddtank.sqlite3`.
- `/health/live` reports process liveness; `/health/ready` checks SQLite integrity and required character-reference data.
- `POST /api/v1/accounts/register` creates only an account identity and stores an ASP.NET Core password hash; it does not create a player or character.
- `POST /api/v1/accounts/login` verifies the password, creates a pending player if needed, and issues a five-minute random game ticket stored only as a hash.
- `/Request/VisualizeRegister.ashx` preserves the Flash first-character request fields and XML response while completing the character in SQLite. It enforces the legacy nickname blacklist/14-byte rule and sex selection.
- The SWF login handshake and legacy Game.Server ticket validation are not yet connected to these endpoints; the current web host is not yet a playable game server.

## Run locally

```powershell
dotnet run --project WebModern/DDTank.Web.csproj
```

Open `http://127.0.0.1:5080`. The database is created under the current Windows user's LocalAppData directory. When the reference seed is present under `WebModern/Seeds`, it is imported automatically.

## Current limitations

This is not yet the complete game launcher or game server. The legacy Game.Server, Center, and Fighting services still use SQL Server through SqlClient. The new SQLite repository covers account-row creation, pending player creation, and the first-character transaction only; other gameplay persistence must be migrated before the complete game can run without SQL Server.

The host does not yet connect the new login ticket to the legacy Game.Server socket protocol or update the SWF login handshake to request the new ticket. The current web host is therefore not a playable game server. Payment workflows remain out of scope.
