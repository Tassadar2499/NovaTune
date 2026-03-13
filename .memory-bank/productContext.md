# Product Context

## Problem Space
NovaTune targets creators or operators who want a modern, self-hosted audio service without relying on a monolithic upload-and-stream stack. The product combines direct object storage uploads, asynchronous processing, secure playback URL issuance, and admin oversight.

## Primary Users
- `Listener`: registers, uploads tracks, monitors processing, manages a personal library, streams tracks, and organizes playlists
- `Admin`: manages users, moderates tracks, reviews audit logs, and monitors analytics trends

## Core User Journeys
1. `Authentication`: register or log in, receive access and refresh tokens, continue via token rotation
2. `Upload`: request a presigned upload URL, send the file to MinIO, let workers create and process the track
3. `Library`: browse personal tracks, inspect details, update metadata, soft-delete or restore as needed
4. `Playback`: request a presigned stream URL, reuse cache when possible, emit playback telemetry
5. `Playlists`: create playlists, add or remove tracks, and reorder items
6. `Administration`: inspect users and tracks, apply moderation, review analytics, and audit sensitive actions

## UX / Product Priorities
- Keep the API as the control plane, not the media transport path
- Preserve responsive playback by caching presigned stream URLs
- Avoid creating track records until storage confirms upload completion
- Separate listener and admin concerns into distinct frontend apps
- Preserve auditability for admin actions

## Current Product Shape In Repo
- Backend functionality is present for all major domains listed above
- The `player` app already has routes and feature pages for auth, library, playlists, track detail, and upload
- The `admin` app already has routes and feature pages for dashboard, analytics, users, tracks, and audit logs
- Shared frontend packages exist for API generation, auth/http helpers, telemetry utilities, and shared UI components

## Product Gaps Still Visible
- Frontend automation is not yet on par with the backend; no frontend test files were found during this refresh
- Production web hosting depends on building both Vue apps before `NovaTuneApp.Web` Release builds
- Some earlier planning docs still describe futures or alternatives that the live repo has already moved past
