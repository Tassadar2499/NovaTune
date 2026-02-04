export type PlaybackEventType = 'play_start' | 'play_stop' | 'play_progress' | 'play_complete' | 'seek';

export interface PlaybackEvent {
  eventType: PlaybackEventType;
  trackId: string;
  clientTimestamp: string;
  positionSeconds: number;
  sessionId: string;
  deviceId: string;
  clientVersion: string;
}

export interface TelemetryConfig {
  endpoint: string;
  getAccessToken: () => string | null;
  batchSize?: number;
  flushIntervalMs?: number;
}
