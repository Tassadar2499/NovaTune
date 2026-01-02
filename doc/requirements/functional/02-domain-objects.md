# 2. Domain Objects (current models)

- **User**
  - Fields: `Email`, `DisplayName`, `PasswordHash`, timestamps, `Status`.
  - Validation: email format, display name length constraints.
  - Status: `Active`, `Disabled`, `PendingDeletion`.
  - Authorization: Admins are represented as a separate user type and carry an `admin` role claim.
- **Track**
  - Fields: `UserId`, `Title`, optional `Artist`, `Duration`, `ObjectKey`, optional `Checksum`, optional `AudioMetadata`, timestamps, `Status`.
  - Validation: `Title` length constraints; `UserId` and `ObjectKey` required.
  - Status: `Processing`, `Ready`, `Failed`, `Deleted`.
- **AudioMetadata**
  - `Format`, `Bitrate`, `SampleRate`, `Channels`, `FileSizeBytes`, `MimeType`.

## 2.1 Identifiers

- API and event identifiers (`UserId`, `TrackId`, `PlaylistId`, etc.) shall be ULID values serialized as strings.
- RavenDB document IDs may remain an internal persistence detail, but must not leak as the external/API identifier.
