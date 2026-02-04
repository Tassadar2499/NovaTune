<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRoute } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { usePlayerStore, type Track } from '@/stores/player';

const route = useRoute();
const auth = useAuthStore();
const player = usePlayerStore();

const track = ref<Track | null>(null);
const isLoading = ref(true);
const error = ref<string | null>(null);

onMounted(async () => {
  try {
    const response = await fetch(
      `${import.meta.env.VITE_API_BASE_URL}/tracks/${route.params.id}`,
      {
        headers: {
          Authorization: `Bearer ${auth.accessToken}`,
        },
      }
    );

    if (!response.ok) {
      throw new Error('Failed to fetch track');
    }

    track.value = await response.json();
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'An error occurred';
  } finally {
    isLoading.value = false;
  }
});

function formatDuration(seconds: number): string {
  const mins = Math.floor(seconds / 60);
  const secs = Math.floor(seconds % 60);
  return `${mins}:${secs.toString().padStart(2, '0')}`;
}

function playTrack() {
  if (track.value) {
    player.clearQueue();
    player.addToQueue(track.value);
    player.play(track.value);
  }
}
</script>

<template>
  <div>
    <div v-if="isLoading" class="flex items-center justify-center py-12">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-primary-500"></div>
    </div>

    <div v-else-if="error" class="card text-center py-8">
      <p class="text-red-400">{{ error }}</p>
      <RouterLink to="/" class="btn-secondary inline-block mt-4">
        Back to Library
      </RouterLink>
    </div>

    <div v-else-if="track" class="max-w-2xl">
      <RouterLink to="/" class="text-slate-400 hover:text-white transition-colors mb-6 inline-flex items-center gap-2">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
        </svg>
        Back to Library
      </RouterLink>

      <div class="flex gap-6 mt-6">
        <div class="w-48 h-48 bg-slate-700 rounded-lg flex-shrink-0"></div>
        <div class="flex-1">
          <h1 class="text-3xl font-bold text-white">{{ track.title }}</h1>
          <p class="text-xl text-slate-400 mt-2">{{ track.artist }}</p>
          <p class="text-slate-500 mt-4">{{ formatDuration(track.duration) }}</p>

          <div class="flex gap-3 mt-6">
            <button @click="playTrack" class="btn-primary flex items-center gap-2">
              <svg class="w-5 h-5" fill="currentColor" viewBox="0 0 24 24">
                <path d="M8 5v14l11-7z" />
              </svg>
              Play
            </button>
            <button class="btn-secondary">
              Add to Playlist
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
