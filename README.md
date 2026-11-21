# CurationBack

ASP.NET Core 10.0 Web API for managing a curated picture collection. It provides JWT-authenticated endpoints for picture CRUD, file uploads, user management, database backups and a public RSS feed. Data is stored in SQLite. It works with a Svelte frontend (CurationFront) in the sibling directory and serves that frontend's built files from `wwwroot` in production.

## Projects

| Project | Type | Purpose |
|---|---|---|
| `CurationBack` | Web API | The main API server |
| `CurationBack.Runner` | Console app | Batch/admin utilities (user registration, bulk picture import, timestamp backfill) |

## Commands

```bash
# Build
dotnet build

# Run the API (development: http://localhost:5116, https://localhost:7212)
dotnet run --project CurationBack/CurationBack.csproj

# Run the Runner utility
dotnet run --project CurationBack.Runner/CurationBack.Runner.csproj

# Publish (release)
dotnet publish -c Release
```

`CurationBack/CurationBack.http` holds ad-hoc requests for manual testing.

## Configuration

`appsettings.json`:

```json
{
  "Polson": {
    "IsProductionString": "false"
  },
  "Jwt": {
    "Key": "<secret key, 32+ chars>",
    "Issuer": "polson.com"
  }
}
```

`IsProductionString` (`"true"` / `"false"`) controls where picture files and the database are found (see [Path Resolution](#path-resolution)).

## Data Persistence

All data lives in one SQLite database, `curation.db`, accessed through `Microsoft.Data.Sqlite` with hand-written SQL (no ORM).

| Class | Table | Notes |
|---|---|---|
| `BaseSqliteDb` | — | Resolves the DB path; opens a connection per call; `SetDeleted`, `Destroy`; backup/restore/download |
| `PicturesSqliteDb` | `Pictures` | `Keywords` stored as a JSON string; audit, filesystem sync, resequence, RSS query |
| `UsersSqliteDb` | `Users` | BCrypt password hashes (enhanced, work factor 13); stores each user's latest token |

The application does **not** create the schema. The tables must already exist:

```sql
CREATE TABLE "Pictures" (
  "Id"          INTEGER PRIMARY KEY,
  "FileName"    TEXT NOT NULL DEFAULT '',
  "Seq"         INTEGER NOT NULL DEFAULT 0,
  "Ts"          NUMERIC NOT NULL DEFAULT 0,
  "Title"       TEXT,
  "Description" TEXT,
  "Keywords"    TEXT NOT NULL DEFAULT '[]',
  "Link"        TEXT,
  "IsMissing"   INTEGER NOT NULL DEFAULT 0,
  "IsDeleted"   INTEGER NOT NULL DEFAULT 0,
  "IsRss"       INTEGER DEFAULT 0
);

CREATE TABLE "Users" (
  "Id"         INTEGER PRIMARY KEY,
  "Email"      TEXT NOT NULL DEFAULT '',
  "FullName"   TEXT NOT NULL DEFAULT '',
  "PwHash"     TEXT NOT NULL DEFAULT '',
  "Token"      TEXT NOT NULL DEFAULT '',
  "IsAdmin"    INTEGER NOT NULL DEFAULT 0,
  "HasPw"      INTEGER NOT NULL DEFAULT 0,
  "IsDisabled" INTEGER NOT NULL DEFAULT 0,
  "IsDeleted"  INTEGER NOT NULL DEFAULT 0
);
```

Booleans are stored as `INTEGER` 0/1. The `Db/` folder is gitignored, so the database and its backups are never committed.

### Backups

- **Backup**: `VACUUM INTO` a new `curation_yyyy-MM-dd_HH-mm-ss.db` in the Db folder.
- **Restore**: copies a `curation_*.db` backup over `curation.db`.
- **Download**: `curation.db` is first vacuumed into `temp.db` so the download is a consistent snapshot; backup files are returned as they are.
- **Delete**: only files whose names start with `curation_`.

Restore and delete are checked so they cannot reach files outside the Db folder.

### Path Resolution

| Environment | Pictures path | DB path |
|---|---|---|
| Development | `../CurationFront/public/pics/` (sibling repo) | `CurationBack/CurationBack/Db/` |
| Production | `wwwroot/pics/` | `Db/` (relative to the working directory) |

In development the paths are worked out by finding `CurationBack\CurationBack` in the current directory, so run the API from inside the repo. Paths are built with Windows separators.

## Authentication

JWT Bearer tokens (HMAC-SHA256; issuer and audience both `Jwt:Issuer`) are issued on login or register and expire after 10 days. Token claims:

| Claim | Value |
|---|---|
| `UserId` | Integer user ID |
| `Email` | User email |
| `FullName` | Display name |
| `IsAdmin` | `True` / `False` |

Custom filters in `Services/FiltersAttributes/` control access:
- `[AdminAuthorize]`: requires a valid token with `IsAdmin = true`
- `[UserAuthorize]`: requires a valid token with a `UserId` claim

## API Reference

All routes have the form `/api/{controller}/{action}`.

### Pictures: `/api/pictures`

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `GetPublicList` | None | All non-deleted, non-missing pictures |
| GET | `Rss` | None | RSS 2.0 feed of the 50 newest non-deleted pictures with `IsRss = true` |
| GET | `GetBySlug?slug=` | None | First picture whose `FileName` starts with the slug |
| GET | `GetById?id=` | None | Single non-deleted picture by ID |
| GET | `GetAll` | Admin | All pictures, including deleted and missing ones |
| GET | `GetAuditList` | Admin | Compares DB records with files on disk and returns `{ Missing, Orphans }` |
| POST | `Save` | Admin | Creates or updates a picture; renames the file if `FileName` changes (name and extension are validated) |
| POST | `SaveWithImg` | Admin | Multipart upload (`file` + `picItemJSON`): saves the file and the record, sets `Ts` to now |
| POST | `CleanPics` | Admin | Syncs records with the filesystem: flags missing files, adds orphan files as new records |
| POST | `ResequencePics` | Admin | Renumbers `Seq` for all pictures, starting at 100 in steps of 10 |
| POST | `RemoveMissing` | Admin | Hard-deletes records flagged `IsMissing`; returns the count |
| POST | `Destroy` | Admin | Hard-deletes a record, and its file if no other record uses it |

### Users: `/api/users`

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `Login` | None | Returns `UserClientRemote` with a JWT; disabled and deleted users are rejected |
| POST | `Register` | None | Creates an account and returns a JWT (valid email, unique, password ≥ 6 chars, no leading/trailing spaces) |
| POST | `Save` | Admin | Creates or updates a user without changing the password hash; you cannot disable yourself |
| POST | `Destroy` | Admin | Hard-deletes a user; you cannot delete yourself |
| POST | `UpdatePassword` | Admin | Sets a user's password (`{ Email, Pw }`) |
| GET | `GetAll` | Admin | All users, without password hashes |

### DB: `/api/db`

All endpoints require Admin.

| Method | Endpoint | Description |
|---|---|---|
| GET | `GetBackupList` | `curation.db` followed by `curation_*.db` backups, newest first |
| GET | `GetFile?fileName=` | Downloads the live DB (as a snapshot) or a backup file |
| POST | `Backup` | Creates a timestamped backup; returns its file name |
| POST | `Restore?fileName=` | Replaces `curation.db` with a `curation_*` backup |
| POST | `Delete?fileName=` | Deletes a `curation_*` backup file |

### Test: `/api/test`

`GetUnsecuredValue` (public), `GetSecuredValue` (user), `GetAdminValue` (admin), `Throw` (raises an exception).

### WeatherForecast: `/api/weatherforecast`

Leftover from the project template; the launch profiles open it on startup.

## Models

### `PictureItem`

| Field | Type | Notes |
|---|---|---|
| `Id` | int | SQLite rowid |
| `FileName` | string | File name only, no path |
| `Seq` | int | Sort order |
| `Ts` | int | Unix timestamp (seconds); used for RSS ordering and `pubDate` |
| `Title` | string? | RSS item title (falls back to the file name) |
| `Description` | string? | |
| `Keywords` | `List<string>` | Stored as JSON; RSS `<category>` elements |
| `Link` | string? | Source link |
| `IsMissing` | bool | Set by `CleanPics` when the file is not on disk |
| `IsDeleted` | bool | Soft-delete flag |
| `IsRss` | bool | Include in the RSS feed |

### `UserClientRemote` (API surface)

| Field | Type | Notes |
|---|---|---|
| `Id` | int | |
| `Email` | string | |
| `FullName` | string | |
| `Token` | string? | JWT, filled in on login/register |
| `IsAdmin` | bool | |
| `HasPw` | bool | Worked out from whether a password hash exists |
| `IsDisabled` | bool | |
| `IsDeleted` | bool | Soft-delete flag |

`UserClient` extends this with `PwHash`. Request models: `UserLogin { Email, Pw }`, and `UserRegister`, which adds `FullName`. Classes marked `[TypeScriptModel]` are shared with the frontend.

## Runner Utility

`CurationBack.Runner` references the API project and creates its services directly against the development database. `Program.cs` builds an `AppSettings` in code; uncomment the run you want:

- `UserRuns.RegisterUser()`: registers a user through `UsersController.Register`
- `PictureRuns.LoadPicturesFromDir()`: bulk-imports images from a source folder (hard-coded paths)
- `PictureRuns.MakeTimestamps()`: fills in `Ts` for pictures that have none
- `PictureRuns.ListAll()`: prints `Id-Seq--FileName` for every picture

## Key Utilities

- **`ExtLinq.cs`**: custom LINQ full outer join, used by the audit to match DB records with files
- **`ExtDateTime.cs`**: Unix/JS time conversions and short time-zone display strings
- **`PicFileOps.cs`**: the one place the pics folder path is worked out; lists, saves, renames, deletes and sizes image files there

## Dependencies

| Package | Purpose |
|---|---|
| `Microsoft.Data.Sqlite` | SQLite data access |
| `BCrypt.Net-Next` | Password hashing |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT middleware |
| `Newtonsoft.Json` | `Keywords` column serialization; parsing `picItemJSON` on upload |

## Development Notes

- In development, CORS allows `http://localhost:5050` and `http://127.0.0.1:5050` (the CurationFront dev server).
- Outside `/api`, requests for paths with no file extension are rewritten to `/index.html` so the SPA can handle routing.
- There is no test project.
