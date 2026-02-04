<script setup lang="ts">
import { onMounted } from 'vue';
import { useRoute } from 'vue-router';
import { usePlaylistsStore } from '@/stores/playlists';
import { usePlayerStore, type Track } from '@/stores/player';

const route = useRoute();
const playlists = usePlaylistsStore();
const player = usePlayerStore();

onMounted(() => {
  playlists.fetchPlaylist(route.params.id as string);
});

function formatDuration(seconds: number): string {
  const mins = Math.floor(seconds / 60);
  const secs = Math.floor(seconds % 60);
  return `${mins}:${secs.toString().padStart(2, '0')}`;
}

function playAll() {
  if (playlists.currentPlaylist?.tracks.length) {
    player.clearQueue();
    playlists.currentPlaylist.tracks.forEach((track) => player.addToQueue(track));
    player.play(playlists.currentPlaylist.tracks[0]);
  }
}

function playTrack(track: Track) {
  if (playlists.currentPlaylist) {
    player.clearQueue();
    playlists.currentPlaylist.tracks.forEach((t) => player.addToQueue(t));
    player.play(track);
  }
}

async function removeTrack(trackId: string) {
  if (playlists.currentPlaylist) {
    await playlists.removeTrackFromPlaylist(playlists.currentPlaylist.id, trackId);
  }
}
</script>

<template>
  <div>
    <RouterLink to="/playlists" class="text-slate-400 hover:text-white transition-colors mb-6 inline-flex items-center gap-2">
      <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
      </svg>
      Back to Playlists
    </RouterLink>

    <div v-if="playlists.isLoading" class="flex items-center justify-center py-12">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-primary-500"></div>
    </div>

    <div v-else-if="playlists.currentPlaylist" class="mt-6">
      <div class="flex gap-6 mb-8">
        <div class="w-48 h-48 bg-slate-700 rounded-lg flex-shrink-0 flex items-center justify-center">
          <svg class="w-16 h-16 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3" />
          </svg>
        </div>
        <div class="flex-1">
          <h1 class="text-3xl font-bold text-white">{{ playlists.currentPlaylist.name }}</h1>
          <p class="text-slate-400 mt-2" v-if="playlists.currentPlaylist.description">
            {{ playlists.currentPlaylist.description }}
          </p>
          <p class="text-slate-500 mt-4">{{ playlists.currentPlaylist.trackCount }} tracks</p>

          <div class="flex gap-3 mt-6">
            <button @click="playAll" class="btn-primary flex items-center gap-2" :disabled="!playlists.currentPlaylist.tracks.length">
              <svg class="w-5 h-5" fill="currentColor" viewBox="0 0 24 24">
                <path d="M8 5v14l11-7z" />
              </svg>
              Play All
            </button>
          </div>
        </div>
      </div>

      <div v-if="playlists.currentPlaylist.tracks.length === 0" class="card text-center py-8">
        <p class="text-slate-400">This playlist is empty</p>
        <p class="text-slate-500 mt-2">Add tracks from your library</p>
      </div>

      <div v-else class="space-y-2">
        <div
          v-for="(track, index) in playlists.currentPlaylist.tracks"
          :key="track.id"
          class="flex items-center gap-4 p-3 rounded-lg hover:bg-slate-800 group transition-colors"
          :class="{ 'bg-slate-800': player.currentTrack?.id === track.id }"
        >
          <span class="w-6 text-slate-500 text-sm text-center">{{ index + 1 }}</span>
          <div
            @click="playTrack(track)"
            class="w-10 h-10 bg-slate-700 rounded flex-shrink-0 flex items-center justify-center cursor-pointer"
          >
            <svg v-if="player.currentTrack?.id === track.id && player.isPlaying" class="w-5 h-5 text-primary-500" fill="currentColor" viewBox="0 0 24 24">
              <path d="M6 4h4v16H6zM14 4h4v16h-4z" />
            </svg>
            <svg v-else class="w-5 h-5 text-slate-500 group-hover:text-white transition-colors" fill="currentColor" viewBox="0 0 24 24">
              <path d="M8 5v14l11-7z" />
            </svg>
          </div>
          <div class="flex-1 min-w-0 cursor-pointer" @click="playTrack(track)">
            <p class="text-white font-medium truncate">{{ track.title }}</p>
            <p class="text-slate-400 text-sm truncate">{{ track.artist }}</p>
          </div>
          <div class="text-slate-400 text-sm">
            {{ formatDuration(track.duration) }}
          </div>
          <button
            @click="removeTrack(track.id)"
            class="text-slate-500 hover:text-red-400 transition-colors opacity-0 group-hover:opacity-100"
            title="Remove from playlist"
          >
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
            </svg>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
