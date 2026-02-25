/# Integration Tests Implementation Plan

Read the implementation plan. Analyze all stages and identify which ones have no dependencies on each other and can be implemented in parallel. For each independent stage, use the Task tool to spawn a sub-agent with these instructions: (1) create a git branch named stage-N, (2) implement all files for that stage, (3) run 'dotnet build' and fix any errors, (4) run all relevant unit tests and fix failures, (5) report back with files changed and test results. After all parallel tasks complete, integrate the branches sequentially, running the full test suite after each merge. Present a final summary of all changes, test results, and any conflicts resolved.

## Coverage Gap Analysis

### Already Covered (4 test files, 91 tests total)

| File | Total | Active | Skipped | Coverage |
|------|-------|--------|---------|----------|
| `AuthIntegrationTests.cs` | 17 | 17 | 0 | register (3), login (4), refresh (5), logout (1), RFC 7807 (2), lifecycle (2) |
| `TrackEndpointTests.cs` | 31 | 24 | 7 | CRUD solid; all 7 `ListTracks_*` skipped (RavenDB index timing) |
| `StreamingIntegrationTests.cs` | 11 | 4 | 7 | auth+validation (3), 1 active placeholder; 7 require upload pipeline; **2 have empty test bodies** |
| `AdminEndpointTests.cs` | 31 | 31 | 0 | users (8), tracks (6), analytics (3), audit (5), auth (2), moderation lifecycle (7) |
| `WebTests.cs` | 1 | 1 | 0 | Empty stub |

### NOT Covered (critical gaps)

| Feature | Routes | Tests | Priority |
|---------|--------|-------|----------|
| **Playlists** (CRUD + tracks + reorder) | 8 routes | 0 | **P0 - Critical** |
| **Telemetry** (single + batch) | 2 routes | 0 | **P1 - High** |
| **Upload** (initiate) | 1 route | 0 | **P2 - Medium** (MinIO disabled) |

### Needs Fix / Expansion

| Area | Issue | Count | Priority |
|------|-------|-------|----------|
| Track listing tests | 7 skipped — RavenDB index timing between test/API stores | 7 | P1 |
| Streaming test stubs | 2 tests with empty bodies (403 + 409) need implementation | 2 | P1 |

---

## Example Audio Files

Three real MP3 files at `tasks/add_integration_tests/examples/`:

| Short Name | File | Size (bytes) | Format |
|------------|------|-------------|--------|
| `BurnTheTowers` | `🎙️ Kerry Eurodyne (Zero Tool)  — Burn the Towers (Cyberpunk 2077) [2rR6TxokG9E].mp3` | 4,413,068 | audio/mpeg, ID3v2.4, 64 kbps, 48 kHz, Stereo |
| `EncoreInHell` | `🎙️ Kerry Eurodyne (Zero Tool)  — Encore in Hell (Cyberpunk 2077) [ULPhOuewHh4].mp3` | 4,674,692 | audio/mpeg, ID3v2.4, 64 kbps, 48 kHz, Stereo |
| `GlitchInTheSystem` | `🎙️ Kerry Eurodyne (Zero Tool vs Han Oomori)  — Glitch in the System (Cyberpunk 2077) [yQy9VbgATuw].mp3` | 4,500,044 | audio/mpeg, ID3v2.4, 64 kbps, 48 kHz, Stereo |

Useful because: real ID3v2.4 metadata; filenames with Unicode emoji (`🎙️`), em dash (`—`), parentheses; ~4MB (within 100MB upload limit); all `audio/mpeg` (primary allowed MIME type).

---

## Implementation Plan

### Phase 1: Test Factory Enhancements

**Agent**: `test-factory-builder`
**File**: `src/integration_tests/NovaTuneApp.IntegrationTests/IntegrationTestsApiFactory.cs`

#### 1.1 — New Using Directives

```csharp
using NovaTuneApp.ApiService.Models.Playlists;  // Playlist, PlaylistTrackEntry, PlaylistVisibility
using NovaTuneApp.ApiService.Models.Upload;      // UploadSession
```

#### 1.2 — Playlist Helpers

```csharp
Task<string> SeedPlaylistAsync(string name, string userId, string? description = null, List<PlaylistTrackEntry>? tracks = null)
Task<(string PlaylistId, List<string> TrackIds)> SeedPlaylistWithTracksAsync(string name, string userId, int trackCount)
Task<Playlist?> GetPlaylistByIdAsync(string playlistId)    // Load by "Playlists/{playlistId}"
Task<int> GetPlaylistCountAsync(string userId)
```

Pattern: follow `SeedTrackAsync` — ULID IDs, `WaitForIndexesAfterSaveChanges()`, set all required fields including `TotalDuration`, `Visibility = PlaylistVisibility.Private`.

#### 1.3 — Audio File Helpers

```csharp
public record TestAudioFile(string FileName, string FilePath, string MimeType, long FileSizeBytes);

static TestAudioFile BurnTheTowers => new(..., "audio/mpeg", 4_413_068);
static TestAudioFile EncoreInHell  => new(..., "audio/mpeg", 4_674_692);
static TestAudioFile GlitchInTheSystem => new(..., "audio/mpeg", 4_500_044);
static TestAudioFile[] AllTestAudioFiles => [BurnTheTowers, EncoreInHell, GlitchInTheSystem];
```

Path resolution: `Path.Combine(GetSolutionRoot(), "tasks", "add_integration_tests", "examples", FileName)`.
Need a `GetSolutionRoot()` helper that walks up from `AppContext.BaseDirectory` to find `NovaTuneApp.sln`.

#### 1.4 — Extend ClearDataAsync

Add deletion of: `Playlist`, `UploadSession` (same pattern as existing types — query all, delete each, wait for indexes).

---

### Phase 2: Playlist Endpoint Tests (P0)

**Agent**: `integration-tester`
**File**: `PlaylistEndpointTests.cs`

#### API Surface Reference

| Method | Path | Response | Rate Limit |
|--------|------|----------|------------|
| GET | `/playlists` | `PagedResult<PlaylistListItem>` 200 | `playlist-list` |
| POST | `/playlists` | `PlaylistDetails` 201 | `playlist-create` |
| GET | `/playlists/{playlistId}` | `PlaylistDetails` 200 | — |
| PATCH | `/playlists/{playlistId}` | `PlaylistDetails` 200 | `playlist-update` |
| DELETE | `/playlists/{playlistId}` | 204 | `playlist-delete` |
| POST | `/playlists/{playlistId}/tracks` | `PlaylistDetails` 200 | `playlist-tracks-add` |
| DELETE | `/playlists/{playlistId}/tracks/{position:int}` | 204 | `playlist-tracks-remove` |
| POST | `/playlists/{playlistId}/reorder` | `PlaylistDetails` 200 | `playlist-reorder` |

**Auth**: All require `ActiveUser` policy.

**Key Models**:
- `CreatePlaylistRequest(Name, Description?)` — record
- `UpdatePlaylistRequest { Name?, Description?, HasDescription }` — distinguishes "not sent" vs "clear to null"
- `AddTracksRequest(TrackIds, Position?)` — TrackIds: 1-100 items, Position: null = append
- `ReorderRequest(Moves)` — Moves: `IReadOnlyList<MoveOperation(From, To)>`, max 50 moves
- `PlaylistDetails(PlaylistId, Name, Description?, TrackCount, TotalDuration, Visibility, CreatedAt, UpdatedAt, Tracks?)`
- `PlaylistListItem(PlaylistId, Name, Description?, TrackCount, TotalDuration, Visibility, CreatedAt, UpdatedAt)`
- `PlaylistTrackItem(Position, TrackId, Title, Artist?, Duration, Status, AddedAt)`
- Query params: `?search=&sortBy=&sortOrder=&cursor=&limit=` (list), `?includeTracks=&trackCursor=&trackLimit=` (detail)
- Valid sort fields (case-insensitive): `createdAt`, `updatedAt`, `name`, `trackCount`
- Valid sort orders: `asc`, `desc`

**Validation Constants**:
- MaxNameLength: 100, MaxDescriptionLength: 500
- MaxTracksPerPlaylist: 10,000; MaxPlaylistsPerUser: 200
- MaxTracksPerAddRequest: 100; MaxMovesPerReorderRequest: 50

**Error Types**:
- `PlaylistNotFoundException` → 404 (`playlist-not-found`)
- `PlaylistAccessDeniedException` → 403 (`forbidden`)
- `PlaylistQuotaExceededException` → 403 (`playlist-quota-exceeded`)
- `PlaylistTrackLimitExceededException` → 403 (`playlist-track-limit-exceeded`)
- `PlaylistTrackNotFoundException` → 404 (`track-not-in-playlist`)
- `InvalidPositionException` → 400 (`invalid-position`)
- `TrackNotFoundException` → 404 (`track-not-found`)
- `TrackDeletedException` → 409 (`track-deleted`)
- Inline validation → 400 (`validation-error`, `invalid-playlist-id`, `invalid-track-id`, `invalid-query-parameter`)

#### 2.1 — CRUD Operations (24 tests)

**Authentication:**
1. `Playlists_Should_return_401_for_unauthenticated_requests` — GET/POST without token

**Create:**
2. `CreatePlaylist_Should_return_201_with_playlist_details` — name + description, verify PlaylistId, TrackCount=0, TotalDuration=00:00:00, Visibility=Private
3. `CreatePlaylist_Should_return_201_without_description` — name only, Description should be null
4. `CreatePlaylist_Should_return_400_for_empty_name` — `{ "name": "" }`
5. `CreatePlaylist_Should_return_400_for_missing_name` — `{ }`
6. `CreatePlaylist_Should_return_400_for_name_too_long` — name > 100 chars
7. `CreatePlaylist_Should_return_400_for_description_too_long` — description > 500 chars
8. `CreatePlaylist_Should_return_403_when_quota_exceeded` — seed 200 playlists (MaxPlaylistsPerUser), attempt 201st → 403 `playlist-quota-exceeded`

**Get:**
9. `GetPlaylist_Should_return_playlist_with_tracks` — `?includeTracks=true`, verify Tracks is not null, contains `PlaylistTrackItem` with Position, Title, Artist, Duration, Status
10. `GetPlaylist_Should_return_playlist_without_tracks` — `?includeTracks=false`, verify Tracks is null
11. `GetPlaylist_Should_support_track_pagination` — seed playlist with 5 tracks, request `?includeTracks=true&trackLimit=2`, verify HasMore, NextCursor
12. `GetPlaylist_Should_return_400_for_invalid_ulid` — `/playlists/not-a-ulid`
13. `GetPlaylist_Should_return_404_for_nonexistent_playlist`
14. `GetPlaylist_Should_return_403_for_other_users_playlist`

**Update:**
15. `UpdatePlaylist_Should_update_name` — verify response has new name, UpdatedAt changed
16. `UpdatePlaylist_Should_update_description`
17. `UpdatePlaylist_Should_clear_description` — send `{ "description": null, "hasDescription": true }` → Description becomes null
18. `UpdatePlaylist_Should_return_400_for_empty_name`
19. `UpdatePlaylist_Should_return_404_for_nonexistent_playlist`
20. `UpdatePlaylist_Should_return_403_for_other_users_playlist`

**Delete:**
21. `DeletePlaylist_Should_return_204_and_remove_playlist` — verify GET returns 404 after
22. `DeletePlaylist_Should_return_404_for_nonexistent_playlist`
23. `DeletePlaylist_Should_return_403_for_other_users_playlist`

**List:**
24. `ListPlaylists_Should_return_users_playlists` — create 2 playlists, verify both returned
25. `ListPlaylists_Should_reject_invalid_sort_field` — `?sortBy=invalid` → 400
26. `ListPlaylists_Should_reject_invalid_sort_order` — `?sortOrder=invalid` → 400

#### 2.2 — Track Management (16 tests)

**Add Tracks (POST /playlists/{id}/tracks):**
27. `AddTracks_Should_add_single_track` — verify TrackCount=1, TotalDuration updated, track in Tracks list
28. `AddTracks_Should_add_multiple_tracks` — 3 tracks at once
29. `AddTracks_Should_append_to_end_by_default` — Position null → appended after existing
30. `AddTracks_Should_insert_at_position` — Position=0 inserts at start, existing tracks shift
31. `AddTracks_Should_return_400_for_empty_track_ids` — `{ "trackIds": [] }`
32. `AddTracks_Should_return_400_for_invalid_track_ulid` — malformed track ID in array
33. `AddTracks_Should_return_400_for_negative_position` — position=-1
34. `AddTracks_Should_return_400_for_too_many_tracks` — >100 track IDs (MaxTracksPerAddRequest)
35. `AddTracks_Should_return_404_for_nonexistent_track` — track ID doesn't exist in DB
36. `AddTracks_Should_return_409_for_deleted_track` — soft-deleted track → `track-deleted`
37. `AddTracks_Should_return_403_for_other_users_playlist`
38. `AddTracks_Should_return_404_for_nonexistent_playlist`

**Remove Track (DELETE /playlists/{id}/tracks/{position}):**
39. `RemoveTrack_Should_return_204_and_decrement_count` — verify TrackCount decremented, TotalDuration updated
40. `RemoveTrack_Should_return_400_for_negative_position`
41. `RemoveTrack_Should_return_404_for_position_out_of_range` — position >= track count → `track-not-in-playlist`
42. `RemoveTrack_Should_return_403_for_other_users_playlist`

#### 2.3 — Reorder Tracks (7 tests)

**Reorder (POST /playlists/{id}/reorder):**
43. `ReorderTracks_Should_move_track_to_new_position` — move(0, 2), verify positions are correct
44. `ReorderTracks_Should_apply_multiple_moves` — 2 sequential moves, verify final order
45. `ReorderTracks_Should_return_400_for_empty_moves` — `{ "moves": [] }`
46. `ReorderTracks_Should_return_400_for_negative_from_position`
47. `ReorderTracks_Should_return_400_for_negative_to_position`
48. `ReorderTracks_Should_return_400_for_empty_playlist` — reorder playlist with 0 tracks → `empty-playlist`
49. `ReorderTracks_Should_return_403_for_other_users_playlist`

#### 2.4 — Lifecycle (2 tests)

50. `PlaylistLifecycle_Create_AddTracks_Reorder_Remove_Delete` — full CRUD + track ops flow
51. `PlaylistTrack_Should_reflect_deleted_track_status` — add track, soft-delete track via `/tracks/{id}`, get playlist, verify track Status in PlaylistTrackItem

**Total Phase 2: 51 tests**

---

### Phase 3: Telemetry Endpoint Tests (P1)

**Agent**: `integration-tester`
**File**: `TelemetryEndpointTests.cs`

#### API Surface Reference

| Method | Path | Request | Response | Rate Limit |
|--------|------|---------|----------|------------|
| POST | `/telemetry/playback` | `PlaybackEventRequest` | `TelemetryAcceptedResponse` 202 | `telemetry-ingest` |
| POST | `/telemetry/playback/batch` | `PlaybackEventBatchRequest` | `TelemetryBatchResponse` 202 | `telemetry-ingest-batch` |

**Auth**: All require `ActiveUser` policy.

**Key Models**:
- `PlaybackEventRequest { EventType, TrackId, ClientTimestamp, PositionSeconds?, DurationPlayedSeconds?, SessionId?, DeviceId?, ClientVersion? }`
- `PlaybackEventBatchRequest { Events: IReadOnlyList<PlaybackEventRequest> }`
- `TelemetryAcceptedResponse(Accepted, CorrelationId)` — single event
- `TelemetryBatchResponse(Accepted, Rejected, CorrelationId)` — batch
- `PlaybackEventTypes.Valid`: `play_start`, `play_stop`, `play_progress`, `play_complete`, `seek` (case-insensitive)

**Validation**:
- Event type: must be in `PlaybackEventTypes.Valid`
- Track ID: must be valid ULID (26 chars)
- PositionSeconds: if present, >= 0
- DurationPlayedSeconds: if present, >= 0
- ClientTimestamp: within [now - 24h, now + 5min] (service-level, returns 400 `invalid-timestamp`)
- Batch: 1-50 events (MaxBatchSize); empty → 400 `validation-error`; >50 → 400 `batch-too-large`
- Track access: track must exist, not deleted, Ready status, owned by user → 403 `track-access-denied`

**Error Types**:
- 400 `invalid-event-type` — bad event type
- 400 `invalid-track-id` — malformed ULID
- 400 `validation-error` — negative position/duration, empty batch
- 400 `batch-too-large` — >50 events
- 400 `invalid-timestamp` — timestamp out of range
- 403 `track-access-denied` — track access denied
- 503 `service-unavailable` — Kafka publish failure

#### 3.1 — Single Event Ingestion (13 tests)

**Authentication:**
1. `Telemetry_Should_return_401_for_unauthenticated_requests`

**Happy Path:**
2. `IngestPlayback_Should_return_202_for_play_start_event` — verify `Accepted: true`, `CorrelationId` not empty
3. `IngestPlayback_Should_return_202_for_play_complete_event` — with `DurationPlayedSeconds`
4. `IngestPlayback_Should_return_202_with_all_optional_fields` — SessionId, DeviceId, ClientVersion, PositionSeconds

**Validation:**
5. `IngestPlayback_Should_return_400_for_invalid_event_type` — "invalid_type" → `invalid-event-type`
6. `IngestPlayback_Should_return_400_for_invalid_track_id` — "not-a-ulid" → `invalid-track-id`
7. `IngestPlayback_Should_return_400_for_negative_position` — `PositionSeconds: -1` → `validation-error`
8. `IngestPlayback_Should_return_400_for_negative_duration` — `DurationPlayedSeconds: -1` → `validation-error`
9. `IngestPlayback_Should_return_400_for_timestamp_too_old` — `ClientTimestamp: now - 25h` → `invalid-timestamp`
10. `IngestPlayback_Should_return_400_for_timestamp_in_future` — `ClientTimestamp: now + 10min` → `invalid-timestamp`

**Access Control:**
11. `IngestPlayback_Should_return_403_for_nonexistent_track` — seed no track, use random ULID → `track-access-denied`
12. `IngestPlayback_Should_return_403_for_other_users_track` — seed track for user A, submit as user B

**Event Types:**
13. `IngestPlayback_Should_accept_all_valid_event_types` — loop: play_start, play_stop, play_progress, play_complete, seek → all 202

#### 3.2 — Batch Ingestion (8 tests)

14. `IngestBatch_Should_return_202_with_accepted_count` — 3 valid events → `Accepted: 3, Rejected: 0`
15. `IngestBatch_Should_return_400_for_empty_batch` — `{ "events": [] }` → `validation-error`
16. `IngestBatch_Should_return_400_for_batch_too_large` — 51 events → `batch-too-large`
17. `IngestBatch_Should_return_400_for_invalid_event_type_in_batch` — one bad event type → 400 (endpoint validates all before calling service)
18. `IngestBatch_Should_return_400_for_invalid_track_id_in_batch` — one bad ULID → 400
19. `IngestBatch_Should_return_202_for_multiple_event_types` — mixed play_start + play_progress + play_complete
20. `IngestBatch_Should_return_401_for_unauthenticated_requests`
21. `IngestBatch_Should_handle_partial_rejection` — batch with valid events + one with access denied → 202 with Rejected > 0

**Total Phase 3: 21 tests**

---

### Phase 4: Upload Endpoint Tests (P2)

**Agent**: `integration-tester`
**File**: `UploadEndpointTests.cs`

#### API Surface Reference

| Method | Path | Request | Response | Rate Limit |
|--------|------|---------|----------|------------|
| POST | `/tracks/upload/initiate` | `InitiateUploadRequest` (body) | `InitiateUploadResponse` 200 | `upload-initiate` |

**Auth**: Requires `ActiveUser` policy.

**Key Models**:
- `InitiateUploadRequest(FileName [1-255], MimeType [required], FileSizeBytes [1-max], Title? [max 255], Artist? [max 255])`
- `InitiateUploadResponse(UploadId, TrackId, PresignedUrl, ExpiresAt, ObjectKey)`
- AllowedMimeTypes: `audio/mpeg`, `audio/mp4`, `audio/flac`, `audio/wav`, `audio/x-wav`, `audio/ogg`

**Validation (via UploadExceptionFilter → UploadProblemDetailsFactory → RFC 7807)**:
- `UnsupportedMimeType` → 400 `unsupported-mime-type`
- `FileTooLarge` → 400 `file-too-large` (>100MB)
- `InvalidFileName` → 400 `invalid-file-name` (null/whitespace, >255 chars, invalid path chars)
- `QuotaExceeded` → 400 `quota-exceeded` (storage or track count)
- `ServiceUnavailable` → 503 `service-unavailable` (MinIO down)

> **Note**: Storage (MinIO) is disabled in testing (`Features:StorageEnabled=false`). The presigned URL generation will fail with 503 `service-unavailable`. Tests that pass validation but hit storage should assert on the expected error (not skip silently). Only skip tests that truly require a presigned URL to proceed.

#### 4.1 — Authentication & Validation (8 tests)

1. `Upload_Should_return_401_for_unauthenticated_requests`
2. `InitiateUpload_Should_return_400_for_missing_filename` — empty string → model validation or `invalid-file-name`
3. `InitiateUpload_Should_return_400_for_missing_mimetype` — empty string → model validation
4. `InitiateUpload_Should_return_400_for_zero_filesize` — `FileSizeBytes: 0` → `[Range(1, ...)]` validation
5. `InitiateUpload_Should_return_400_for_unsupported_mimetype` — "application/pdf" → `unsupported-mime-type`
6. `InitiateUpload_Should_return_400_for_filename_too_long` — 256 chars → `invalid-file-name`
7. `InitiateUpload_Should_return_400_for_file_too_large` — `FileSizeBytes: 200_000_000` (>100MB) → `file-too-large`
8. `InitiateUpload_Should_accept_all_allowed_mime_types` — loop: audio/mpeg, audio/mp4, audio/flac, audio/wav, audio/x-wav, audio/ogg → all pass validation (may get 503 from storage)

#### 4.2 — Real Audio File Tests (4 tests)

Uses `TestAudioFile` helpers with actual file metadata:

9. `InitiateUpload_Should_pass_validation_with_real_mp3_metadata` — `BurnTheTowers` (4,413,068 bytes, audio/mpeg) → passes validation, hits storage → expect 200 or 503
10. `InitiateUpload_Should_handle_unicode_filename` — `GlitchInTheSystem` (🎙️ emoji, em dash) → passes filename validation
11. `InitiateUpload_Should_accept_title_and_artist` — `EncoreInHell` with Title="Encore in Hell", Artist="Kerry Eurodyne"
12. `InitiateUpload_Should_pass_validation_for_all_example_files` — loop `AllTestAudioFiles`, verify none get 400

#### 4.3 — Storage-Dependent (skipped)

13. `[Skip("Requires MinIO")] InitiateUpload_Should_return_presigned_url` — use `BurnTheTowers`, verify UploadId, TrackId, PresignedUrl, ExpiresAt, ObjectKey format
14. `[Skip("Requires MinIO")] FullUploadFlow_Should_upload_and_create_track` — initiate + PUT file bytes to presigned URL + verify track created

**Total Phase 4: 14 tests (12 active + 2 skipped)**

---

### Phase 5: Existing Test Improvements (P1)

**Agent**: `test-fixer`

#### 5.1 — Fix 7 Skipped Track Listing Tests

**File**: `src/integration_tests/NovaTuneApp.IntegrationTests/TrackEndpointTests.cs`

**Root cause**: RavenDB index `Tracks_ByUserForSearch` timing. The factory's `IDocumentStore` and the API's `IDocumentStore` are separate instances. After seeding via the factory, the API's session may hit a stale index.

**Fix strategy**: Add retry-with-delay in the test itself:

```csharp
HttpResponseMessage response = null!;
for (int attempt = 0; attempt < 5; attempt++)
{
    response = await client.GetAsync("/tracks");
    var body = await response.Content.ReadFromJsonAsync<PagedResult<TrackListItem>>(JsonOptions);
    if (body?.Items.Count > 0 || attempt == 4) break;
    await Task.Delay(500);
}
```

**Skip reason to look for**: `"ListTracks uses Tracks_ByUserForSearch index - timing issues with test/API store consistency"`

Tests to un-skip (7):
- `ListTracks_Should_return_empty_list_when_no_tracks`
- `ListTracks_Should_return_only_users_own_tracks`
- `ListTracks_Should_return_paged_results`
- `ListTracks_Should_continue_with_cursor`
- `ListTracks_Should_filter_by_search`
- `ListTracks_Should_exclude_deleted_by_default`
- `ListTracks_Should_include_deleted_when_requested`

#### 5.2 — Implement 2 Streaming Test Stubs

**File**: `src/integration_tests/NovaTuneApp.IntegrationTests/StreamingIntegrationTests.cs`

These tests already exist with empty bodies — implement them:

1. `Stream_endpoint_Should_return_403_for_other_users_track` — seed Ready track for user A, POST `/tracks/{id}/stream` as user B → 403
2. `Stream_endpoint_Should_return_409_for_processing_track` — seed track with `TrackStatus.Processing`, attempt stream → 409

**Total Phase 5: 7 un-skipped + 2 implemented = 9 tests fixed**

---

## Test File Structure

```
src/integration_tests/NovaTuneApp.IntegrationTests/
├── IntegrationTestsApiFactory.cs     # Phase 1: playlist, audio, ClearData enhancements
├── TestCollections.cs                 # Unchanged
├── AuthIntegrationTests.cs            # Existing — 17 tests (no changes)
├── TrackEndpointTests.cs              # Phase 5.1: un-skip 7 ListTracks tests
├── StreamingIntegrationTests.cs       # Phase 5.2: implement 2 empty test bodies
├── AdminEndpointTests.cs              # Existing — 31 tests (no changes)
├── PlaylistEndpointTests.cs           # NEW — Phase 2 (51 tests)
├── TelemetryEndpointTests.cs          # NEW — Phase 3 (21 tests)
├── UploadEndpointTests.cs             # NEW — Phase 4 (12 active + 2 skipped)
└── WebTests.cs                        # Existing placeholder
```

## Implementation Conventions

All new test files must follow established patterns:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Playlists;
using Shouldly;

namespace NovaTuneApp.Tests;

[Collection("Integration Tests")]
[Trait("Category", "Aspire")]
public class PlaylistEndpointTests(IntegrationTestsApiFactory factory) : IAsyncLifetime
{
    private readonly IntegrationTestsApiFactory _factory = factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task InitializeAsync() => await _factory.ClearDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreatePlaylist_Should_return_201_with_playlist_details()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        var request = new { Name = "My Playlist", Description = "Test description" };

        // Act
        var response = await client.PostAsJsonAsync("/playlists", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var playlist = await response.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
        playlist.ShouldNotBeNull();
        playlist.Name.ShouldBe("My Playlist");
        playlist.Description.ShouldBe("Test description");
        playlist.TrackCount.ShouldBe(0);
        playlist.TotalDuration.ShouldBe(TimeSpan.Zero);
        playlist.Visibility.ShouldBe(PlaylistVisibility.Private);
    }
}
```

**Key conventions:**
- Primary constructor `(IntegrationTestsApiFactory factory)` for DI
- `ClearDataAsync()` in `InitializeAsync()` for isolation
- `ShouldBe` / `ShouldNotBeNull` (Shouldly) for assertions
- `ReadFromJsonAsync<T>` with `JsonSerializerOptions { PropertyNameCaseInsensitive = true }`
- Unique emails per test: `$"user-{Guid.NewGuid():N}@test.com"`
- Dispose additional `HttpClient` instances created in tests
- RFC 7807 verification: `ReadFromJsonAsync<ProblemDetails>`, check `.Type`, `.Status`

## Estimated Test Count Summary

| File | New | Active | Skipped | Notes |
|------|-----|--------|---------|-------|
| PlaylistEndpointTests.cs | 51 | 51 | 0 | New file |
| TelemetryEndpointTests.cs | 21 | 21 | 0 | New file |
| UploadEndpointTests.cs | 14 | 12 | 2 | New file, 2 require MinIO |
| TrackEndpointTests.cs | 0 | +7 | -7 | Un-skip ListTracks tests |
| StreamingIntegrationTests.cs | 0 | +2 | -2 | Implement empty test bodies |
| **Total** | **86 new** | **+93 active** | **-9 fixed** | |

**After all phases**: 91 existing + 86 new = **177 total tests**, 5 remaining skipped (streaming pipeline).

---

## Agents & Skills

### Agent Roster

| Agent | Location | Owns | What it Does |
|-------|----------|------|-------------|
| **`test-factory-builder`** | `.claude/agents/test-factory-builder.md` | Phase 1 | Modifies `IntegrationTestsApiFactory.cs` only. Adds playlist helpers, audio file helpers, extends ClearDataAsync. Must finish before Phases 2-4. |
| **`integration-tester`** | `.claude/agents/integration-tester.md` | Phases 2, 3, 4 | Creates new test files. Has full API surface reference, model names, validation constants. Spawn 1 instance per phase. |
| **`test-fixer`** | `.claude/agents/test-fixer.md` | Phase 5 | Modifies existing test files only. Fixes 7 skipped tests (retry logic), implements 2 empty stubs. No dependencies on Phases 2-4. |

### Supporting Agents (domain reference)

| Agent | Use When |
|-------|----------|
| `playlist-tester` | Need playlist model details, reorder edge cases, position semantics |
| `track-tester` | Need soft-delete semantics, pagination cursor format, lifecycle rules |
| `admin-tester` | Need audit log format, moderation status transitions |

### Skills

| Skill | Invocation | Use For |
|-------|------------|---------|
| `add-integration-tests` | `/add-integration-tests playlists` | Interactive scaffolding for a feature area |
| `build-and-run` | `/build-and-run` | Build + run full test suite |

---

## Dependency Graph

```
Phase 1 (test-factory-builder) ─────────── PREREQUISITE
    │
    ├──► Phase 2 (integration-tester) ──── PlaylistEndpointTests.cs   (51 tests)
    ├──► Phase 3 (integration-tester) ──── TelemetryEndpointTests.cs  (21 tests)
    ├──► Phase 4 (integration-tester) ──── UploadEndpointTests.cs     (14 tests)
    │
Phase 5 (test-fixer) ──────────────────── TrackEndpointTests.cs + StreamingIntegrationTests.cs (9 fixes)
```

- **Phase 1** blocks Phases 2, 3, 4 (they use new factory helpers)
- **Phases 2, 3, 4** are independent of each other
- **Phase 5** is independent of everything (modifies different files, no new helpers needed)

---

## Orchestration

### Step 1: Parallel — Phase 1 + Phase 5

```
Task(subagent_type="test-factory-builder",
     description="Phase 1: factory helpers",
     prompt="Implement Phase 1 from tasks/add_integration_tests/main.md.

Read these files first:
- src/integration_tests/NovaTuneApp.IntegrationTests/IntegrationTestsApiFactory.cs (existing factory)
- src/NovaTuneApp/NovaTuneApp.ApiService/Models/Playlists/ (Playlist, PlaylistTrackEntry, PlaylistVisibility)
- src/NovaTuneApp/NovaTuneApp.ApiService/Models/Upload/UploadSession.cs

Then add to IntegrationTestsApiFactory.cs:
1. Using directives for NovaTuneApp.ApiService.Models.Playlists and Models.Upload
2. SeedPlaylistAsync(name, userId, description?, tracks?) — follow SeedTrackAsync pattern
3. SeedPlaylistWithTracksAsync(name, userId, trackCount) — seed real tracks + playlist
4. GetPlaylistByIdAsync(playlistId) — Load 'Playlists/{playlistId}'
5. GetPlaylistCountAsync(userId) — query with WaitForNonStaleResults
6. TestAudioFile record + BurnTheTowers/EncoreInHell/GlitchInTheSystem statics (see main.md for exact filenames and sizes)
7. Extend ClearDataAsync to also delete Playlist and UploadSession documents

Run: dotnet build src/integration_tests/NovaTuneApp.IntegrationTests/NovaTuneApp.IntegrationTests.csproj
Then: dotnet test src/integration_tests/NovaTuneApp.IntegrationTests/NovaTuneApp.IntegrationTests.csproj --filter 'FullyQualifiedName~AuthIntegrationTests' -v minimal (verify no regressions)",
     run_in_background=true)

Task(subagent_type="test-fixer",
     description="Phase 5: fix skipped tests",
     prompt="Implement Phase 5 from tasks/add_integration_tests/main.md.

Phase 5.1 — TrackEndpointTests.cs:
Read src/integration_tests/NovaTuneApp.IntegrationTests/TrackEndpointTests.cs.
Find all 7 tests with Skip reason containing 'Tracks_ByUserForSearch index'.
Remove the Skip attribute. Add retry-with-delay logic (5 attempts, 500ms between) when calling GET /tracks to handle RavenDB index timing.

Phase 5.2 — StreamingIntegrationTests.cs:
Read src/integration_tests/NovaTuneApp.IntegrationTests/StreamingIntegrationTests.cs.
Find the 2 tests with empty bodies:
- Stream_endpoint_Should_return_403_for_other_users_track
- Stream_endpoint_Should_return_409_for_processing_track
Implement them using the factory helpers (CreateAuthenticatedClientWithUserAsync, SeedTrackAsync with TrackStatus.Processing).

Run: dotnet build + dotnet test --filter 'FullyQualifiedName~TrackEndpointTests' -v detailed
Then: dotnet test --filter 'FullyQualifiedName~StreamingIntegrationTests' -v detailed",
     run_in_background=true)
```

### Step 2: Parallel — Phases 2, 3, 4 (after Phase 1 finishes)

```
Task(subagent_type="integration-tester",
     description="Phase 2: playlist tests",
     prompt="Implement Phase 2 from tasks/add_integration_tests/main.md.

Read these files first:
- src/NovaTuneApp/NovaTuneApp.ApiService/Endpoints/PlaylistEndpoints.cs (8 routes, all handlers)
- src/NovaTuneApp/NovaTuneApp.ApiService/Models/Playlists/ (all DTOs)
- src/NovaTuneApp/NovaTuneApp.ApiService/Exceptions/PlaylistExceptions.cs (all exception types)
- src/NovaTuneApp/NovaTuneApp.ApiService/Infrastructure/Configuration/PlaylistOptions.cs (validation constants)
- src/integration_tests/NovaTuneApp.IntegrationTests/IntegrationTestsApiFactory.cs (factory helpers)

Create PlaylistEndpointTests.cs with all 51 tests from Phase 2 sections 2.1-2.4.

Key details:
- Routes: GET/POST /playlists, GET/PATCH/DELETE /playlists/{id}, POST /playlists/{id}/tracks, DELETE /playlists/{id}/tracks/{position:int}, POST /playlists/{id}/reorder
- Response types: PlaylistDetails (create/get/update/addTracks/reorder), PlaylistListItem (list), 204 (delete/removeTrack)
- UpdatePlaylistRequest has HasDescription flag to distinguish 'not sent' from 'clear to null'
- MaxNameLength=100, MaxDescriptionLength=500, MaxPlaylistsPerUser=200, MaxTracksPerAddRequest=100, MaxMovesPerReorderRequest=50
- Valid sort: createdAt, updatedAt, name, trackCount (case-insensitive)

Run: dotnet build + dotnet test --filter 'FullyQualifiedName~PlaylistEndpointTests' -v detailed",
     run_in_background=true)

Task(subagent_type="integration-tester",
     description="Phase 3: telemetry tests",
     prompt="Implement Phase 3 from tasks/add_integration_tests/main.md.

Read these files first:
- src/NovaTuneApp/NovaTuneApp.ApiService/Endpoints/TelemetryEndpoints.cs (2 routes)
- src/NovaTuneApp/NovaTuneApp.ApiService/Models/Telemetry/ (all DTOs, PlaybackEventTypes)
- src/NovaTuneApp/NovaTuneApp.ApiService/Services/TelemetryIngestionService.cs (validation logic)
- src/NovaTuneApp/NovaTuneApp.ApiService/Infrastructure/Configuration/TelemetryOptions.cs
- src/integration_tests/NovaTuneApp.IntegrationTests/IntegrationTestsApiFactory.cs (factory helpers)

Create TelemetryEndpointTests.cs with all 21 tests from Phase 3 sections 3.1-3.2.

Key details:
- POST /telemetry/playback → TelemetryAcceptedResponse(Accepted, CorrelationId)
- POST /telemetry/playback/batch → TelemetryBatchResponse(Accepted, Rejected, CorrelationId)
- Valid event types: play_start, play_stop, play_progress, play_complete, seek
- Endpoint validates event type + track ID format for ALL events before calling service
- Service validates timestamp bounds (24h old, 5min future) and track access
- MaxBatchSize=50
- Need to seed tracks for valid requests (use SeedTrackAsync)

Run: dotnet build + dotnet test --filter 'FullyQualifiedName~TelemetryEndpointTests' -v detailed",
     run_in_background=true)

Task(subagent_type="integration-tester",
     description="Phase 4: upload tests",
     prompt="Implement Phase 4 from tasks/add_integration_tests/main.md.

Read these files first:
- src/NovaTuneApp/NovaTuneApp.ApiService/Endpoints/UploadEndpoints.cs (1 route)
- src/NovaTuneApp/NovaTuneApp.ApiService/Models/Upload/ (InitiateUploadRequest, InitiateUploadResponse, UploadSession)
- src/NovaTuneApp/NovaTuneApp.ApiService/Services/UploadService.cs (AllowedMimeTypes, validation flow)
- src/NovaTuneApp/NovaTuneApp.ApiService/Exceptions/UploadException.cs + UploadErrorType.cs
- src/NovaTuneApp/NovaTuneApp.ApiService/Infrastructure/UploadProblemDetailsFactory.cs (error type URLs)
- src/integration_tests/NovaTuneApp.IntegrationTests/IntegrationTestsApiFactory.cs (TestAudioFile helpers)

Create UploadEndpointTests.cs with all 14 tests from Phase 4 sections 4.1-4.3.

Key details:
- POST /tracks/upload/initiate → InitiateUploadResponse or error
- AllowedMimeTypes: audio/mpeg, audio/mp4, audio/flac, audio/wav, audio/x-wav, audio/ogg
- MaxUploadSizeBytes: 100MB (104,857,600)
- FileName: 1-255 chars, no invalid path chars
- Storage is DISABLED in testing — validation passes but storage call returns 503
- Tests 9-12 use TestAudioFile helpers: BurnTheTowers(4413068), EncoreInHell(4674692), GlitchInTheSystem(4500044)
- For tests that pass validation but hit storage, assert either 200 or 503 (not 400)
- Tests 13-14 are [Fact(Skip='Requires MinIO')]

Run: dotnet build + dotnet test --filter 'FullyQualifiedName~UploadEndpointTests' -v detailed",
     run_in_background=true)
```

### Step 3: Verify

```bash
dotnet test src/integration_tests/NovaTuneApp.IntegrationTests/NovaTuneApp.IntegrationTests.csproj -v minimal
```

Or: `/build-and-run`

### Git Branch Strategy

| Branch | Phase | Agent | Merges After |
|--------|-------|-------|--------------|
| `integ/phase-1-factory` | 1 | `test-factory-builder` | — (first) |
| `integ/phase-5-fixes` | 5 | `test-fixer` | Phase 1 |
| `integ/phase-2-playlists` | 2 | `integration-tester` | Phase 1 |
| `integ/phase-3-telemetry` | 3 | `integration-tester` | Phase 1 |
| `integ/phase-4-uploads` | 4 | `integration-tester` | Phase 1 |

Merge order: Phase 1 → Phase 5 → Phase 2 → Phase 3 → Phase 4.
Run full test suite after each merge. Conflicts are unlikely (each phase touches different files).
