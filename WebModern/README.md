# DDTank ASP.NET Core Web Host

This is the replacement web host being introduced alongside the legacy PHP site.

## Current scope

- ASP.NET Core Razor Pages host targeting .NET 10.
- Loopback-only default binding at `http://127.0.0.1:5080`.
- Liveness/readiness endpoints and a minimal local landing page.
- No PHP runtime, SQL credentials, payment integration, account store, or game-server coupling.

## Run locally

```powershell
dotnet run --project WebModern/DDTank.Web.csproj
```

Open `http://127.0.0.1:5080`.

## Deliberately not implemented yet

Account registration/login, password storage, player persistence, game tickets, and payment workflows are not stubbed with fake success responses. They require explicit domain and database contracts. Since this is a fresh rebuild with no player data, new accounts can use a clean password-hashing design when persistence is introduced.

## Next integration steps

1. Inventory the legacy SQL stored procedures used by account creation and game login.
2. Decide whether the rebuilt single-player edition needs web accounts at all; local profile creation may be sufficient.
3. Implement persistence behind application interfaces.
4. Add a short-lived, single-use game-launch ticket and validate it at the C# game login boundary.
5. Reuse approved static assets from `Web/Assets` without copying the legacy PHP runtime.
