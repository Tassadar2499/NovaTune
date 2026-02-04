<script setup lang="ts">
import { ref, onMounted, watch } from 'vue';
import { useLibraryStore, type TrackFilters } from '@/stores/library';
import { usePlayerStore } from '@/stores/player';
import { useDebounceFn } from '@vueuse/core';

const library = useLibraryStore();
const player = usePlayerStore();

const searchQuery = ref('');
const filters = ref<TrackFilters>({
  sortBy: 'createdAt',
  sortOrder: 'desc',
});

const debouncedSearch = useDebounceFn(() => {
  filters.value.search = searchQuery.value || undefined;
  library.reset();
  library.fetchTracks(filters.value);
}, 300);

watch(searchQuery, debouncedSearch);

onMounted(() => {
  library.fetchTracks(filters.value);
});

function formatDuration(seconds: number): string {
  const mins = Math.floor(seconds / 60);
  const secs = Math.floor(seconds % 60);
  return `${mins}:${secs.toString().padStart(2, '0')}`;
}

function playTrack(track: (typeof library.tracks)[0]) {
  player.clearQueue();
  player.addToQueue(track);
  player.play(track);
}
</script>

<template>
  <div>
    <div class="flex items-center justify-between mb-8">
      <h1 class="text-2xl font-bold text-white">My Library</h1>
      <div class="flex items-center gap-4">
        <div class="relative">
          <input
            v-model="searchQuery"
            type="search"
            placeholder="Search tracks..."
            class="input w-64 pl-10"
          />
          <svg class="w-5 h-5 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>
      </div>
    </div>

    <div v-if="library.isLoading && library.tracks.length === 0" class="flex items-center justify-center py-12">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-primary-500"></div>
    </div>

    <div v-else-if="library.error" class="card text-center py-8">
      <p class="text-red-400">{{ library.error }}</p>
      <button @click="library.fetchTracks(filters)" class="btn-secondary mt-4">
        Try Again
      </button>
    </div>

    <div v-else-if="library.tracks.length === 0" class="card text-center py-12">
      <svg class="w-16 h-16 text-slate-600 mx-auto mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3" />
      </svg>
      <p class="text-slate-400 text-lg">No tracks found</p>
      <p class="text-slate-500 mt-2">Upload some music to get started</p>
      <RouterLink to="/upload" class="btn-primary inline-block mt-4">
        Upload Track
      </RouterLink>
    </div>

    <div v-else class="space-y-2">
      <div
        v-for="track in library.tracks"
        :key="track.id"
        @click="playTrack(track)"
        class="flex items-center gap-4 p-3 rounded-lg hover:bg-slate-800 cursor-pointer group transition-colors"
        :class="{ 'bg-slate-800': player.currentTrack?.id === track.id }"
      >
        <div class="w-12 h-12 bg-slate-700 rounded flex-shrink-0 flex items-center justify-center">
          <svg v-if="player.currentTrack?.id === track.id && player.isPlaying" class="w-6 h-6 text-primary-500" fill="currentColor" viewBox="0 0 24 24">
            <path d="M6 4h4v16H6zM14 4h4v16h-4z" />
          </svg>
          <svg v-else class="w-6 h-6 text-slate-500 group-hover:text-white transition-colors" fill="currentColor" viewBox="0 0 24 24">
            <path d="M8 5v14l11-7z" />
          </svg>
        </div>
        <div class="flex-1 min-w-0">
          <p class="text-white font-medium truncate">{{ track.title }}</p>
          <p class="text-slate-400 text-sm truncate">{{ track.artist }}</p>
        </div>
        <div class="text-slate-400 text-sm">
          {{ formatDuration(track.duration) }}
        </div>
      </div>

      <div v-if="library.hasMore" class="text-center pt-4">
        <button
          @click="library.loadMore(filters)"
          :disabled="library.isLoading"
          class="btn-secondary"
        >
          {{ library.isLoading ? 'Loading...' : 'Load More' }}
        </button>
      </div>
    </div>
  </div>
</template>
