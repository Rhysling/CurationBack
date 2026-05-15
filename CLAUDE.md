# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run the API
dotnet run --project CurationBack/CurationBack.csproj

# Run the Runner utility
dotnet run --project CurationBack.Runner/CurationBack.Runner.csproj

# Publish (Release)
dotnet publish -c Release
```

There is no test project. `TestController` provides manual endpoint verification, and `CurationBack.Runner` provides operational utilities.

## Architecture

**CurationBack** is an ASP.NET Core 10.0 Web API for managing a curated picture collection with JWT authentication. It is one of two projects in the solution; `CurationBack.Runner` is a console utility app for batch/admin operations.

### Data Persistence

There is no database. All data is stored as JSON files in `CurationBack/Db/`:
- `PicturesDb.json` — array of `PictureItem` objects
- `UsersDb.json` — array of `UserClient` objects (BCrypt-hashed passwords)

`BaseDb<T>` (`Services/BaseDb.cs`) is the generic base class for all persistence. It handles: JSON serialization via Newtonsoft.Json, incremental ID generation, soft-delete via `IsDeleted` flag, and timestamped backup/restore. Both `PicturesDb` and `UsersDb` extend it.

### File Path Resolution

Picture files live at different paths depending on environment:
- **Development**: `../CurationFront/public/pics` (sibling repo folder)
- **Production**: `wwwroot/pics`

This is controlled by the `IsProduction` flag in `appsettings.json` and resolved in `PicFileOps.cs`.

### Authentication

JWT Bearer tokens are issued on login/register (10-day expiration). Claims include `UserId`, `Email`, `FullName`, and `IsAdmin`. Custom action filters in `Services/FiltersAttributes/` implement `[AdminAuthorize]` and `[UserAuthorize]` for endpoint protection.

### Controllers & Endpoints

| Controller | Route | Notes |
|---|---|---|
| `PicturesController` | `/api/pictures` | Picture CRUD, file upload, audit/cleanup |
| `UsersController` | `/api/users` | Login, register, user management |
| `DbController` | `/api/db` | Backup, restore, delete (Admin only) |
| `TestController` | `/api/test` | Public/user/admin test endpoints |

Key picture operations: `CleanPics` syncs DB records with filesystem; `GetAuditList` uses a full outer join (custom LINQ in `Utilities/ExtLinq.cs`) to find missing or orphaned files. Pictures use a `Seq` field (multiples of 10) for ordering, with resequencing on batch saves.

### Configuration

`appsettings.json` holds JWT key/issuer (`polson.com`), logging, and `IsProduction`. CORS is configured for `localhost:5050` in development.
