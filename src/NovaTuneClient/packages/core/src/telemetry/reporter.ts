import type { PlaybackEvent, TelemetryConfig } from './types';

/**
 * Telemetry reporter with batching support.
 */
export class TelemetryReporter {
  private queue: PlaybackEvent[] = [];
  private flushTimer: number | null = null;
  private readonly batchSize: number;
  private readonly flushIntervalMs: number;

  constructor(private readonly config: TelemetryConfig) {
    this.batchSize = config.batchSize ?? 10;
    this.flushIntervalMs = config.flushIntervalMs ?? 30_000;
  }

  /**
   * Queues a playback event for reporting.
   */
  report(event: PlaybackEvent): void {
    this.queue.push(event);

    if (this.queue.length >= this.batchSize) {
      this.flush();
    } else if (!this.flushTimer) {
      this.flushTimer = window.setTimeout(() => this.flush(), this.flushIntervalMs);
    }
  }

  /**
   * Immediately sends all queued events.
   */
  async flush(): Promise<void> {
    if (this.flushTimer) {
      clearTimeout(this.flushTimer);
      this.flushTimer = null;
    }

    if (this.queue.length === 0) {
      return;
    }

    const events = [...this.queue];
    this.queue = [];

    try {
      const token = this.config.getAccessToken();
      const headers: Record<string, string> = {
        'Content-Type': 'application/json',
      };

      if (token) {
        headers.Authorization = `Bearer ${token}`;
      }

      await fetch(this.config.endpoint, {
        method: 'POST',
        headers,
        body: JSON.stringify({ events }),
      });
    } catch {
      // Re-queue events on failure (with limit to prevent memory issues)
      if (this.queue.length < 100) {
        this.queue = [...events, ...this.queue];
      }
    }
  }

  /**
   * Cleans up the reporter.
   */
  destroy(): void {
    if (this.flushTimer) {
      clearTimeout(this.flushTimer);
    }
    this.flush();
  }
}
