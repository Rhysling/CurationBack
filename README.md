# CurationBack

ASP.NET Core 10.0 Web API for managing a curated picture collection. Provides JWT-authenticated endpoints for picture CRUD, file uploads, and user management. Paired with a Svelte frontend (CurationFront) in the sibling directory.

## Projects

| Project | Type | Purpose |
|---|---|---|
| `CurationBack` | Web API | The main API server |
| `CurationBack.Runner` | Console app | Batch/admin utilities (backup, user registration, data migration) |

## Commands

```bash
# Build
dotnet build

# Run the API (development)
dotnet run --project CurationBack/CurationBack.csproj

# Run the Runner utility
dotnet run --project CurationBack.Runner/CurationBack.Runner.csproj

# Publish (release)
dotnet publish -c Release
```

## Configuration

`appsettings.json`:

```json
{
  "Polson": {
    "IsProductionString": "false"
  },
  "Jwt": {
    "Key": "<secret key>",
    "Issuer": "polson.com"
  }
}
```

`IsProductionString` controls where picture files and DB files are resolved (see [File Path Resolution](#file-path-resolution) below).

## Data Persistence

There is no database. All data is stored as JSON files in `CurationBack/Db/`:

| File | Contents |
|---|---|
| `PicturesDb.json` | Array of `PictureItem` objects |
| `UsersDb.json` | Array of `UserClient` objects (BCrypt-hashed passwords) |

`BaseDb<T>` (`Services/BaseDb.cs`) is the generic persistence base class. It handles JSON serialization (Newtonsoft.Json), incremental integer ID generation, soft-delete via `IsDeleted`, and timestamped backup/restore. `PicturesDb` and `UsersDb` both extend it.

### File Path Resolution

Picture files and DB files resolve differently by environment:

| Environment | Pictures path | DB path |
|---|---|---|
| Development | `../CurationFront/public/pics` | `CurationBack/CurationBack/Db/` |
| Production | `wwwroot/pics` | `Db/` (relative to working dir) |

## Authentication

JWT Bearer tokens are issued on login or register with a 10-day expiration. Token claims:

| Claim | Value |
|---|---|
| `UserId` | Integer user ID |
| `Email` | User email |
| `FullName` | Display name |
| `IsAdmin` | Boolean |

Custom action filters in `Services/FiltersAttributes/` enforce access:
- `[AdminAuthorize]` — requires a valid token with `IsAdmin = true`
- `[UserAuthorize]` — requires any valid token

## API Reference

### Pictures — `/api/pictures`

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `GetPublicList` | None | All non-deleted, non-missing pictures |
| GET | `GetBySlug` | None | Single picture by slug |
| GET | `GetById` | None | Single picture by ID |
| GET | `GetAll` | Admin | All pictures including deleted and missing |
| GET | `GetAuditList` | Admin | Full outer join of DB records vs. filesystem — returns `{ Missing, Orphans }` |
| POST | `Save` | Admin | Create or update a picture; renames the file if `FileName` changes |
| POST | `SaveWithImg` | Admin | Multipart upload — saves both the file and the DB record |
| POST | `CleanPics` | Admin | Syncs DB records with the current filesystem state |
| POST | `RemoveMissing` | Admin | Hard-removes DB records flagged as missing |
| POST | `Destroy` | Admin | Hard-deletes a picture record and its file (if no other record references the file) |

### Users — `/api/users`

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `Login` | None | Returns `UserClientRemote` with JWT token |
| POST | `Register` | None | Creates a user account, returns JWT token |
| POST | `Save` | Admin | Create or update a user (cannot disable yourself) |
| POST | `Destroy` | Admin | Hard-deletes a user (cannot destroy yourself) |
| POST | `UpdatePassword` | Admin | Updates a user's BCrypt password hash |
| GET | `GetAll` | Admin | All users (no password hashes) |

### DB — `/api/db`

All endpoints require Admin.

| Method | Endpoint | Description |
|---|---|---|
| GET | `GetBackupList?dbName=` | Lists timestamped backup files for `UsersDb` or `PicturesDb` |
| GET | `GetFile?fileName=` | Downloads a DB JSON file by name |
| POST | `Backup?dbName=` | Creates a timestamped backup of `UsersDb` or `PicturesDb` |
| POST | `Restore?fileName=` | Restores a DB from a backup file |
| POST | `Delete?fileName=` | Deletes a backup file |

### Test — `/api/test`

Manual endpoint verification. Public, user, and admin variants.

## Models

### `PictureItem`

| Field | Type | Notes |
|---|---|---|
| `Id` | int | Auto-incremented |
| `FileName` | string | Filename only, no path |
| `Seq` | int | Sort order (multiples of 10); resequenced on batch save |
| `Ts` | int | Unix timestamp |
| `Keywords` | `List<string>` | |
| `Description` | string? | |
| `Link` | string? | |
| `IsMissing` | bool | Set by `CleanPics` when file not found on disk |
| `IsDeleted` | bool | Soft-delete flag |

### `UserClientRemote` (API surface)

| Field | Type | Notes |
|---|---|---|
| `Id` | int | |
| `Email` | string | |
| `FullName` | string | |
| `Token` | string? | JWT, populated on login/register |
| `IsAdmin` | bool | |
| `HasPw` | bool | |
| `IsDisabled` | bool | |
| `IsDeleted` | bool | Soft-delete flag |

`UserClient` (internal, stored in `UsersDb.json`) extends `UserClientRemote` and adds `PwHash`.

## Runner Utility

`CurationBack.Runner` references the main project and directly instantiates service classes. It is used for operations that require direct DB access without going through the API — for example, bootstrapping the first admin user, bulk data migrations, or manual backups. Edit `Program.cs` to activate the desired run by uncommenting the relevant call.

## Key Utilities

- **`ExtLinq.cs`** (`Utilities/`) — custom LINQ full outer join used by `GetAuditList` to match DB records against filesystem files
- **`PicFileOps.cs`** — handles file save, rename, and delete in the pics directory, resolving the correct path by environment
- **`BaseDb<T>`** — generic persistence with backup/restore; all state is held in memory and flushed to JSON on every write

## Dependencies

| Package | Purpose |
|---|---|
| `BCrypt.Net-Next` | Password hashing |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT middleware |
| `Newtonsoft.Json` | JSON serialization for DB files |

## Development Notes

- CORS is configured for `localhost:5050` in development (the CurationFront dev server port).
- There is no test project. `TestController` provides manual endpoint smoke tests; `CurationBack.Runner` provides operational scripting.
- The `Db/` folder is excluded from the build via the `.csproj` `<Compile Remove>` directive — JSON data files are never compiled.
- `Seq` values are stored as multiples of 10 to allow manual reordering without a full resequence.
