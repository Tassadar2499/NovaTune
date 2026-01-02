# Req 11.x — Admin / Moderation

- **Req 11.1** The system shall allow Admins to list/search users and update user status (enable/disable/pending deletion).
- **Req 11.2** The system shall allow Admins to list/search tracks across users and delete/moderate tracks, and shall require audit logs and reason codes for Admin actions.
- **Req 11.3** The system shall allow Admins to view analytics dashboards (per-track play counts and recent activity at minimum).

## Clarifications

- Moderation semantics:
  - Delete: removes the track from all playlists and prevents further streaming.
  - Moderate: marks the track for review.
  - Disable: prevents streaming but keeps the track accessible for review.
