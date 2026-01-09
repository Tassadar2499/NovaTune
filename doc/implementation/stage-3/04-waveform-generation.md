# 4. Waveform Generation (ffmpeg)

## Waveform Data Format

Generate peak data for visualization (not audio playback):

```bash
ffmpeg -i "{input_file}" -ac 1 -filter:a "aresample=8000,asetnsamples=n=1000" \
  -f wav -acodec pcm_s16le "{output_file}"
```

**Alternative: JSON peaks format** (recommended for smaller size):

```bash
ffmpeg -i "{input_file}" -ac 1 -filter:a "aresample=8000" \
  -f lavfi -i "sine=frequency=1:duration=0.001" \
  -filter_complex "[0:a]astats=metadata=1:reset=1,ametadata=print:key=lavfi.astats.Overall.Peak_level:file=-" \
  -f null -
```

## Waveform Storage

- **Object Key**: `waveforms/{userId}/{trackId}/peaks.json`
- **Content-Type**: `application/json`
- **Compression**: gzip (optional, configurable)
- **Max Size**: 100 KB (truncate if larger)

## Waveform JSON Schema

```json
{
  "version": 1,
  "sampleRate": 8000,
  "samplesPerPeak": 441,
  "peaks": [0.12, 0.45, 0.78, 0.32, ...]  // Normalized 0-1 values
}
```
