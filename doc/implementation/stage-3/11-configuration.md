# 11. Configuration

## appsettings.json

```json
{
  "AudioProcessor": {
    "MaxConcurrency": 4,
    "MaxRetries": 3,
    "RetryBackoffMs": [1000, 5000, 30000],
    "MaxTrackDurationMinutes": 120,
    "TempDirectory": "/tmp/novatune-processing",
    "MaxTempDiskSpaceMb": 2048,
    "FfprobeTimeoutSeconds": 30,
    "FfmpegTimeoutSeconds": 120,
    "TotalProcessingTimeoutMinutes": 10,
    "WaveformPeakCount": 1000,
    "WaveformCompressionEnabled": true
  },
  "Kafka": {
    "ConsumerGroup": "audio-processor-worker",
    "Topics": {
      "AudioEvents": "{env}-audio-events",
      "AudioEventsDlq": "{env}-audio-events-dlq"
    }
  }
}
```

## Configuration Options Reference

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `MaxConcurrency` | int | 4 | Maximum concurrent processing tasks |
| `MaxRetries` | int | 3 | Retry attempts before DLQ |
| `RetryBackoffMs` | int[] | [1000, 5000, 30000] | Backoff delays per retry |
| `MaxTrackDurationMinutes` | int | 120 | Maximum allowed track duration |
| `TempDirectory` | string | /tmp/novatune-processing | Temp file storage path |
| `MaxTempDiskSpaceMb` | int | 2048 | Max temp disk usage |
| `FfprobeTimeoutSeconds` | int | 30 | ffprobe execution timeout |
| `FfmpegTimeoutSeconds` | int | 120 | ffmpeg execution timeout |
| `TotalProcessingTimeoutMinutes` | int | 10 | Total processing time limit |
| `WaveformPeakCount` | int | 1000 | Number of peaks to generate |
| `WaveformCompressionEnabled` | bool | true | Enable gzip compression |
