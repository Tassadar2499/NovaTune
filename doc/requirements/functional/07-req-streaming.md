# Req 5.x — Streaming

- **Req 5.1** The system shall allow a Listener to request playback for a track they own (or are otherwise permitted to access).
- **Req 5.2** The system shall return a short-lived streaming URL (presigned GET), reusing cached URLs where valid.
- **Req 5.3** The streaming solution shall support range requests (byte-range playback).
- **Req 5.4** The system shall emit playback telemetry suitable for analytics (at minimum: play start/stop, duration/position summaries).

## Clarifications

- Streaming is always direct-from-MinIO via presigned GET (the API does not proxy bytes).
- Telemetry mechanism: client-reported events.
- Analytics retention period: 30 days.
