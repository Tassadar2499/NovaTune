# 7. Idempotency (Req 3.5)

## Guarantees

1. **Status check**: Skip processing if `Status != Processing`
2. **Overwrite safe**: Re-extracting metadata and regenerating waveform overwrites previous values safely
3. **Optimistic concurrency**: Prevents lost updates from concurrent processing

## Replay Scenarios

| Scenario | Behavior |
|----------|----------|
| Event replayed, track `Ready` | Skip (no-op) |
| Event replayed, track `Failed` | Skip (no-op, requires manual intervention) |
| Event replayed, track `Processing` | Reprocess (safe) |
| Event replayed, track `Deleted` | Skip (no-op) |
