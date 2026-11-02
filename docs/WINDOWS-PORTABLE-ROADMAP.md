# Windows Portable Edition Roadmap

## Decisions
- Distribution: portable Windows package; extract and run.
- Migration: gradual; preserve existing C# game rules and wire protocol while replacing bounded components.
- Client: hybrid; retain the SWF game client and add a native Windows launcher.

## Repository baseline
- Legacy solution: `Source Server/DDTank 4.1.sln`.
- The README mentions .NET Framework 4.5.2, while inspected server projects target .NET Framework 4.7.2. The project files and cloud build will establish the actual baseline.
- The solution contains multiple libraries and services; it is not yet a single launcher executable.
- SQL Server backup files exist in the repository and must be excluded from release packages pending data and licensing review.

## Milestones
1. **Cloud build baseline:** compile the unchanged legacy solution on a Windows GitHub Actions runner and capture failures.
2. **Portable launcher:** add a self-contained win-x64 Windows launcher; store saves, logs, settings, and backups under `%LOCALAPPDATA%/DDTank`.
3. **Runtime supervision:** identify actual startup projects and service dependencies from source/configuration; add health checks and graceful shutdown.
4. **Hybrid client:** trace SWF loading, endpoint configuration, socket policy, and required Flash APIs; test Ruffle compatibility before selecting a runtime.
5. **Incremental migration:** introduce seams around persistence, networking, timers, and game services; add protocol and game-rule tests before replacing implementations.
6. **Portable release:** package versioned win-x64 files with checksums; test clean-machine startup, save persistence, backup/restore, and shutdown.

## Important constraints
- The first workflow is a build-validation baseline, not a finished game EXE.
- Do not replace SQL Server with SQLite until stored procedures, schema, transactions, and provider dependencies have been inventoried.
- Do not assume a SWF will run in a modern browser or Ruffle without testing.
- Keep user data outside the application folder. Do not include database backups, secrets, or developer-specific paths in releases.

## Acceptance criteria
- Extract and launch on a clean Windows machine without installing the .NET SDK or SQL Server manually.
- Launcher reports startup errors with useful logs and only reports ready after local services pass health checks.
- Saves survive restart; shutdown closes child processes and flushes state.
- Original SWF works against the actual local endpoints in the selected runtime.
