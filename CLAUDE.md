# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run the API (http://localhost:5116, https://localhost:7212)
dotnet run --project CurationBack/CurationBack.csproj

# Run the Runner utility
dotnet run --project CurationBack.Runner/CurationBack.Runner.csproj

# Publish (Release)
dotnet publish -c Release

# Inspect the dev database
sqlite3 CurationBack/Db/curation.db .schema
```

There is no test project. `TestController` and `CurationBack/CurationBack.http` provide manual endpoint verification; `CurationBack.Runner` provides operational utilities.

## Architecture

**CurationBack** is an ASP.NET Core 10.0 Web API for managing a curated picture collection with JWT authentication. It also serves the built CurationFront SPA from `wwwroot` in production (non-`/api`, extensionless paths are rewritten to `/index.html` in `Program.cs`). `CurationBack.Runner` is a console app that references the API project and instantiates its services directly.

### Data Persistence (SQLite)

All data lives in a single SQLite file, `CurationBack/Db/curation.db`, accessed with raw ADO.NET via `Microsoft.Data.Sqlite` (no ORM, no migrations).

- `BaseSqliteDb` (`Services/BaseSqliteDb.cs`) — resolves the DB path, opens a new connection per call, provides `SetDeleted`/`Destroy` by Id, and backup/restore/download file operations.
- `PicturesSqliteDb` — `Pictures` table. `Keywords` is stored as a JSON string (Newtonsoft.Json). Also handles audit, sync-with-filesystem, resequence, and the RSS query.
- `UsersSqliteDb` — `Users` table. Passwords hashed with `BCrypt.EnhancedHashPassword` (work factor 13). `SaveItem` upserts but always preserves the existing `PwHash`; passwords change only via `SavePassword`.
- Both are registered as singletons in `Program.cs`.

- `TrafficSqliteDb` — cookieless traffic analytics (`Events`, `Salts` tables) in a separate `traffic.db` in the same Db folder (created automatically). `TrafficCollector` handles `POST /api/Traffic/Collect` (anonymous, rate-limited per IP, always 204; never stores/logs raw IP or UA; visitor = SHA-256 with a daily-rotating salt). `TrafficHousekeeping` (BackgroundService) deletes old salts and events past `Traffic:RetentionDays`. Contract: `CurationFront/docs/traffic-backend.md`.

**The Pictures/Users schema is not created by code.** `curation.db` must already contain `Pictures` and `Users` tables (see README for the schema). Only the `traffic.db` tables are created (`CREATE TABLE IF NOT EXISTS`) at startup and after a traffic restore. The `Db/` folder is gitignored, so the DB and its backups are never committed. Booleans are stored as `INTEGER` 0/1.

The legacy JSON persistence (`BaseDb.cs`, `PicturesDb.cs`, `UsersDb.cs`) has been deleted; old `*Db*.json` files may still sit in `Db/`.

Backups: `BaseSqliteDb` takes a base file name (`curation` or `traffic`). `BackupFile` runs `VACUUM INTO <name>_yyyy-MM-dd_HH-mm-ss.db` in the Db folder; `RestoreFile` writes a backup into the live file with the SQLite online backup API (safe with open pooled connections). `DbController` lists both DBs' files, backs up both on `Backup`, and routes `GetFile`/`Restore`/`Delete` by file-name prefix. Restore/Delete only accept backup names (`curation_*`, `traffic_*`) and are guarded against path traversal.

### Path Resolution

Controlled by `Polson:IsProductionString` (`"true"`/`"false"`) in `appsettings.json`, exposed as `AppSettings.Polson.IsProduction`. Development paths are derived by locating `CurationBack\CurationBack` in the current directory, so the app must be run from within that folder tree. Paths use Windows backslashes.

| | Pictures | Database |
|---|---|---|
| Development | `<root>\CurationFront\public\pics\` (sibling repo) | `<root>\CurationBack\CurationBack\Db\` |
| Production | `<cwd>\wwwroot\pics\` | `<cwd>\Db\` |

The pics path is resolved only in `PicFileOps.cs`; use `PicFileOps.GetImageFileNames()` to list the pics folder. Allowed image extensions: `.jpg`, `.jpeg`, `.png`, `.gif`.

### Authentication

JWT Bearer tokens (HMAC-SHA256, issuer = audience = `Jwt:Issuer`) are issued on login/register with a 10-day expiration, and the latest token is also stored in the `Users.Token` column. Claims: `UserId`, `Email`, `FullName`, `IsAdmin`. Custom filters in `Services/FiltersAttributes/`:
- `[AdminAuthorize]` — requires `IsAdmin` claim `true`
- `[UserAuthorize]` — requires a `UserId` claim

`[TypeScriptModel]` marks models shared with the frontend (`PictureItem`, `UserClientRemote`, `UserLogin`, `UserRegister`); it is a marker only.

### Controllers & Endpoints

All routes are `api/[controller]/[action]`.

| Controller | Notes |
|---|---|
| `PicturesController` | Public list/get, `Rss` feed (RSS 2.0, latest 50 `IsRss` pictures by `Ts`), admin CRUD, multipart upload (`SaveWithImg`), audit, `CleanPics`, `ResequencePics`, `RemoveMissing`, `Destroy` |
| `UsersController` | Login, register, admin user management |
| `DbController` | Admin only: backup list, download, backup, restore, delete |
| `TrafficController` | Anonymous `Collect` beacon; admin `Summary`, `TimeSeries`, `Breakdown`, `Realtime` reports |
| `TestController` | Public/user/admin test endpoints, `Throw` |
| `WeatherForecastController` | Template leftover; target of the launch profiles' `launchUrl` |

`GetAuditList` uses the custom full outer join in `Utilities/ExtLinq.cs` to compare DB records with files on disk (`Missing` vs `Orphans`). `CleanPics` resets `IsMissing`, flags missing records, and inserts orphan files as new records. `Seq` orders pictures; `ReSequence` renumbers everything from 100 in steps of 10. `Ts` is a Unix timestamp set on upload.

### Configuration

`appsettings.json`: `Polson:IsProductionString`, `Jwt:Key`, `Jwt:Issuer` (`polson.com`), `Traffic` (`SiteKey`, `OwnHosts`, `RetentionDays`, `CollectPermitsPerMinute`, `AllowedEventNames`), logging. `traffic.db` never uses WAL (production storage is a network share); startup forces `journal_mode=DELETE`. A `Jwt__Key` environment variable overrides `Jwt:Key` on the dev machine. In development, CORS allows `http://localhost:5050` and `http://127.0.0.1:5050` (CurationFront dev server); HTTPS redirection is enabled only outside development.

### Runner

`CurationBack.Runner/Program.cs` builds an `AppSettings` in code (development paths) and calls whichever run is uncommented: `UserRuns.RegisterUser`, `PictureRuns` (`LoadPicturesFromDir`, `MakeTimestamps`, `ListAll`), `DbRuns` (currently all commented out). Edit `Program.cs` to choose the run.
