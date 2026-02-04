import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { hashDeviceId, getOrCreateDeviceId } from '@novatune/core/device';
import { useAuthStore } from './auth';

export interface Track {
  id: string;
  title: string;
  artist: string;
  duration: number;
  coverUrl?: string;
}

export const usePlayerStore = defineStore('player', () => {
  const audio = ref<HTMLAudioElement | null>(null);
  const currentTrack = ref<Track | null>(null);
  const isPlaying = ref(false);
  const currentTime = ref(0);
  const duration = ref(0);
  const volume = ref(1);
  const queue = ref<Track[]>([]);

  const sessionId = ref(crypto.randomUUID());
  const hashedDeviceId = hashDeviceId(getOrCreateDeviceId());

  const progress = computed(() => (duration.value > 0 ? currentTime.value / duration.value : 0));

  async function play(track: Track) {
    const auth = useAuthStore();

    if (currentTrack.value?.id !== track.id) {
      currentTrack.value = track;

      // Get presigned streaming URL
      const response = await fetch(
        `${import.meta.env.VITE_API_BASE_URL}/tracks/${track.id}/stream`,
        {
          method: 'POST',
          headers: {
            Authorization: `Bearer ${auth.accessToken}`,
          },
        }
      );

      if (!response.ok) {
        throw new Error('Failed to get stream URL');
      }

      const { streamUrl } = await response.json();

      if (!audio.value) {
        audio.value = new Audio();
        setupAudioListeners();
      }

      audio.value.src = streamUrl;
    }

    await audio.value?.play();
    isPlaying.value = true;

    await reportTelemetry('play_start');
  }

  async function pause() {
    audio.value?.pause();
    isPlaying.value = false;
    await reportTelemetry('play_stop');
  }

  function togglePlay() {
    if (isPlaying.value) {
      pause();
    } else if (currentTrack.value) {
      play(currentTrack.value);
    }
  }

  function seek(position: number) {
    if (audio.value) {
      audio.value.currentTime = position;
    }
  }

  function setVolume(newVolume: number) {
    volume.value = Math.max(0, Math.min(1, newVolume));
    if (audio.value) {
      audio.value.volume = volume.value;
    }
  }

  async function reportTelemetry(eventType: string) {
    if (!currentTrack.value) return;

    const auth = useAuthStore();

    try {
      await fetch(`${import.meta.env.VITE_API_BASE_URL}/telemetry/playback`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${auth.accessToken}`,
        },
        body: JSON.stringify({
          eventType,
          trackId: currentTrack.value.id,
          clientTimestamp: new Date().toISOString(),
          positionSeconds: currentTime.value,
          sessionId: sessionId.value,
          deviceId: hashedDeviceId,
          clientVersion: import.meta.env.VITE_APP_VERSION,
        }),
      });
    } catch {
      // Telemetry failures should not disrupt playback
    }
  }

  function setupAudioListeners() {
    if (!audio.value) return;

    audio.value.addEventListener('timeupdate', () => {
      currentTime.value = audio.value?.currentTime ?? 0;
    });

    audio.value.addEventListener('loadedmetadata', () => {
      duration.value = audio.value?.duration ?? 0;
    });

    audio.value.addEventListener('ended', async () => {
      await reportTelemetry('play_complete');
      playNext();
    });
  }

  function playNext() {
    const currentIndex = queue.value.findIndex((t) => t.id === currentTrack.value?.id);
    if (currentIndex >= 0 && currentIndex < queue.value.length - 1) {
      play(queue.value[currentIndex + 1]);
    }
  }

  function playPrevious() {
    const currentIndex = queue.value.findIndex((t) => t.id === currentTrack.value?.id);
    if (currentIndex > 0) {
      play(queue.value[currentIndex - 1]);
    }
  }

  function addToQueue(track: Track) {
    queue.value.push(track);
  }

  function clearQueue() {
    queue.value = [];
  }

  return {
    currentTrack,
    isPlaying,
    currentTime,
    duration,
    volume,
    queue,
    progress,
    play,
    pause,
    togglePlay,
    seek,
    setVolume,
    playNext,
    playPrevious,
    addToQueue,
    clearQueue,
  };
});
