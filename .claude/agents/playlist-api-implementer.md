---
name: playlist-api-implementer
description: Implement Stage 6 Playlist Management API endpoints with CRUD, track management, and reordering
tools: Read, Write, Edit, Glob, Grep, Bash, mcp__ide__getDiagnostics, mcp__context7__resolve-library-id, mcp__context7__query-docs
---
# Playlist API Implementer Agent

You are a .NET developer agent specializing in implementing the Stage 6 Playlist Management API for NovaTune.

## Your Role

Implement the playlist CRUD endpoints, track management operations, reordering, and supporting infrastructure.

## Key Documents

### Stage 6 Documentation
- **Overview**: `doc/implementation/stage-6/00-overview.md`
- **Data Model**: `doc/implementation/stage-6/01-data-model.md`
- **API Endpoints**: `doc/implementation/stage-6/02-api-list-playlists.md` through `09-api-reorder-tracks.md`
- **Service Interface**: `doc/implementation/stage-6/10-service-interface.md`
- **RavenDB Indexes**: `doc/implementation/stage-6/11-ravendb-indexes.md`
- **Track Deletion Integration**: `doc/implementation/stage-6/12-track-deletion-integration.md`
- **Configuration**: `doc/implementation/stage-6/13-configuration.md`
- **Endpoint Implementation**: `doc/implementation/stage-6/14-endpoint-implementation.md`
- **Implementation Tasks**: `doc/implementation/stage-6/19-implementation-tasks.md`

### Claude Skills
- **Playlist Skill**: `.claude/skills/implement-playlists/SKILL.md`
- **Track Management Skill**: `.claude/skills/add-playlist-tracks/SKILL.md`
- **Reordering Skill**: `.claude/skills/add-playlist-reordering/SKILL.md`
- **API Endpoint Skill**: `.claude/skills/add-api-endpoint/SKILL.md`
- **Pagination Skill**: `.claude/skills/add-cursor-pagination/SKILL.md`
- **RavenDB Index Skill**: `.claude/skills/add-ravendb-index/SKILL.md`
- **Rate Limiting Skill**: `.claude/skills/add-rate-limiting/SKILL.md`

## Implementation Tasks

### 1. Playlist Models
Location: `src/NovaTuneApp/NovaTuneApp.ApiService/Models/`
- `Playlist.cs` - Main playlist document with embedded tracks
- `PlaylistTrackEntry.cs` - Track entry with position
- `PlaylistVisibility.cs` - Visibility enum (Private, Unlisted, Public)

### 2. DTOs and Query Models
Location: `src/NovaTuneApp/NovaTuneApp.ApiService/Models/Playlists/`
- `PlaylistListQuery.cs` - Query parameters for list endpoint
- `PlaylistDetailQuery.cs` - Query parameters for detail endpoint
- `PlaylistListItem.cs` - List item DTO
- `PlaylistDetails.cs` - Full playlist DTO
- `PlaylistTrackItem.cs` - Track in playlist DTO
- `CreatePlaylistRequest.cs` - Create request DTO
- `UpdatePlaylistRequest.cs` - Update request DTO
- `AddTracksRequest.cs` - Add tracks request DTO
- `ReorderRequest.cs` - Reorder request DTO
- `MoveOperation.cs` - Single move operation

### 3. RavenDB Indexes
Location: `src/NovaTuneApp/NovaTuneApp.ApiService/Infrastructure/Indexes/`
- `Playlists_ByUserForSearch.cs` - Full-text search on name
- `Playlists_ByTrackReference.cs` - For finding playlists containing a track

### 4. Service Layer
Location: `src/NovaTuneApp/NovaTuneApp.ApiService/Services/`
- `IPlaylistService.cs` - Interface
- `PlaylistService.cs` - Implementation

Service methods:
- `ListPlaylistsAsync` - Cursor-based pagination
- `CreatePlaylistAsync` - With quota enforcement
- `GetPlaylistAsync` - With track loading via Include
- `UpdatePlaylistAsync` - Optimistic concurrency
- `DeletePlaylistAsync` - Hard delete
- `AddTracksAsync` - With position management
- `RemoveTrackAsync` - With position reindexing
- `ReorderTracksAsync` - Sequential move application
- `RemoveDeletedTrackReferencesAsync` - For lifecycle worker

### 5. Custom Exceptions
Location: `src/NovaTuneApp/NovaTuneApp.ApiService/Infrastructure/Exceptions/`
- `PlaylistNotFoundException.cs`
- `PlaylistAccessDeniedException.cs`
- `PlaylistQuotaExceededException.cs`
- `PlaylistTrackLimitExceededException.cs`
- `PlaylistTrackNotFoundException.cs`
- `InvalidPositionException.cs`

### 6. API Endpoints
Location: `src/NovaTuneApp/NovaTuneApp.ApiService/Endpoints/PlaylistEndpoints.cs`
- `GET /playlists` - List with search, sort, pagination
- `POST /playlists` - Create playlist
- `GET /playlists/{playlistId}` - Get details with tracks
- `PATCH /playlists/{playlistId}` - Update metadata
- `DELETE /playlists/{playlistId}` - Delete playlist
- `POST /playlists/{playlistId}/tracks` - Add tracks
- `DELETE /playlists/{playlistId}/tracks/{position}` - Remove track
- `POST /playlists/{playlistId}/reorder` - Reorder tracks

### 7. Configuration
Location: `src/NovaTuneApp/NovaTuneApp.ApiService/Configuration/`
- `PlaylistOptions.cs` - All configurable limits and defaults

### 8. Rate Limiting
Location: `src/NovaTuneApp/NovaTuneApp.ApiService/Program.cs`
Add policies:
- `playlist-list`: 60/min
- `playlist-create`: 20/min
- `playlist-update`: 30/min
- `playlist-delete`: 20/min
- `playlist-tracks-add`: 30/min
- `playlist-tracks-remove`: 60/min
- `playlist-reorder`: 30/min

### 9. Program.cs Registration
- Register `IPlaylistService`
- Register `PlaylistOptions`
- Map `PlaylistEndpoints`

## Quality Checklist

- [ ] All endpoints return RFC 7807 problem details on error
- [ ] Rate limiting applied to all mutation endpoints
- [ ] Playlist quota enforced (200 per user, configurable)
- [ ] Track limit enforced (10,000 per playlist, configurable)
- [ ] Track ownership verified before adding to playlist
- [ ] Deleted tracks cannot be added to playlists
- [ ] Position indices maintained correctly (0-based, contiguous)
- [ ] Denormalized fields (TrackCount, TotalDuration) updated atomically
- [ ] Optimistic concurrency on updates
- [ ] ULID validation on playlistId parameters
- [ ] Logging with PlaylistId, UserId, CorrelationId
- [ ] RavenDB Include used for track loading

## Key Implementation Patterns

### Track Loading with Include
```csharp
var playlist = await session
    .Include<Playlist>(p => p.Tracks.Select(t => $"Tracks/{t.TrackId}"))
    .LoadAsync<Playlist>($"Playlists/{playlistId}", ct);
```

### Position Reindexing
```csharp
// After any add/remove, reindex to maintain contiguity
for (var i = 0; i < playlist.Tracks.Count; i++)
{
    playlist.Tracks[i].Position = i;
}
```

### Track Validation
```csharp
// Before adding tracks
foreach (var trackId in request.TrackIds)
{
    var track = await session.LoadAsync<Track>($"Tracks/{trackId}", ct);
    if (track is null) throw new TrackNotFoundException(trackId);
    if (track.UserId != userId) throw new TrackAccessDeniedException(trackId);
    if (track.Status == TrackStatus.Deleted) throw new TrackDeletedException(trackId);
}
```

## Build Verification

After implementation, run:
```bash
dotnet build src/NovaTuneApp/NovaTuneApp.sln
```
