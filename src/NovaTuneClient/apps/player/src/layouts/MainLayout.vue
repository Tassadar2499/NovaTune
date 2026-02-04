<script setup lang="ts">
import { RouterView, RouterLink } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { usePlayerStore } from '@/stores/player';

const auth = useAuthStore();
const player = usePlayerStore();

function formatTime(seconds: number): string {
  const mins = Math.floor(seconds / 60);
  const secs = Math.floor(seconds % 60);
  return `${mins}:${secs.toString().padStart(2, '0')}`;
}
</script>

<template>
  <div class="min-h-screen bg-slate-900 pb-24">
    <!-- Sidebar -->
    <aside class="fixed left-0 top-0 bottom-20 w-64 bg-slate-800 border-r border-slate-700 p-4">
      <div class="mb-8">
        <h1 class="text-2xl font-bold text-white">NovaTune</h1>
      </div>

      <nav class="space-y-2">
        <RouterLink
          to="/"
          class="block px-4 py-2 rounded-lg text-slate-300 hover:bg-slate-700 hover:text-white transition-colors"
          active-class="bg-slate-700 text-white"
        >
          Library
        </RouterLink>
        <RouterLink
          to="/playlists"
          class="block px-4 py-2 rounded-lg text-slate-300 hover:bg-slate-700 hover:text-white transition-colors"
          active-class="bg-slate-700 text-white"
        >
          Playlists
        </RouterLink>
        <RouterLink
          to="/upload"
          class="block px-4 py-2 rounded-lg text-slate-300 hover:bg-slate-700 hover:text-white transition-colors"
          active-class="bg-slate-700 text-white"
        >
          Upload
        </RouterLink>
      </nav>

      <div class="absolute bottom-4 left-4 right-4">
        <div class="flex items-center gap-3 px-4 py-2 rounded-lg bg-slate-700">
          <div class="w-8 h-8 rounded-full bg-primary-600 flex items-center justify-center text-white text-sm font-medium">
            {{ auth.user?.displayName?.[0]?.toUpperCase() || 'U' }}
          </div>
          <div class="flex-1 min-w-0">
            <p class="text-sm text-white truncate">{{ auth.user?.displayName || 'User' }}</p>
          </div>
          <button
            @click="auth.logout()"
            class="text-slate-400 hover:text-white transition-colors"
            title="Logout"
          >
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
            </svg>
          </button>
        </div>
      </div>
    </aside>

    <!-- Main content -->
    <main class="ml-64 p-8">
      <RouterView />
    </main>

    <!-- Player bar -->
    <div class="player-bar flex items-center px-4 gap-4" v-if="player.currentTrack">
      <!-- Track info -->
      <div class="flex items-center gap-3 w-64">
        <div class="w-14 h-14 bg-slate-700 rounded flex-shrink-0"></div>
        <div class="min-w-0">
          <p class="text-white font-medium truncate">{{ player.currentTrack.title }}</p>
          <p class="text-slate-400 text-sm truncate">{{ player.currentTrack.artist }}</p>
        </div>
      </div>

      <!-- Controls -->
      <div class="flex-1 flex flex-col items-center">
        <div class="flex items-center gap-4 mb-2">
          <button @click="player.playPrevious()" class="text-slate-400 hover:text-white transition-colors">
            <svg class="w-6 h-6" fill="currentColor" viewBox="0 0 24 24">
              <path d="M6 6h2v12H6zm3.5 6l8.5 6V6z" />
            </svg>
          </button>
          <button
            @click="player.togglePlay()"
            class="w-10 h-10 rounded-full bg-white flex items-center justify-center hover:scale-105 transition-transform"
          >
            <svg v-if="player.isPlaying" class="w-5 h-5 text-slate-900" fill="currentColor" viewBox="0 0 24 24">
              <path d="M6 4h4v16H6zM14 4h4v16h-4z" />
            </svg>
            <svg v-else class="w-5 h-5 text-slate-900 ml-0.5" fill="currentColor" viewBox="0 0 24 24">
              <path d="M8 5v14l11-7z" />
            </svg>
          </button>
          <button @click="player.playNext()" class="text-slate-400 hover:text-white transition-colors">
            <svg class="w-6 h-6" fill="currentColor" viewBox="0 0 24 24">
              <path d="M6 18l8.5-6L6 6v12zM16 6v12h2V6h-2z" />
            </svg>
          </button>
        </div>
        <div class="w-full max-w-md flex items-center gap-2">
          <span class="text-xs text-slate-400">{{ formatTime(player.currentTime) }}</span>
          <input
            type="range"
            :value="player.currentTime"
            :max="player.duration"
            @input="(e) => player.seek(Number((e.target as HTMLInputElement).value))"
            class="flex-1 h-1 bg-slate-600 rounded-full appearance-none cursor-pointer"
          />
          <span class="text-xs text-slate-400">{{ formatTime(player.duration) }}</span>
        </div>
      </div>

      <!-- Volume -->
      <div class="flex items-center gap-2 w-32">
        <svg class="w-5 h-5 text-slate-400" fill="currentColor" viewBox="0 0 24 24">
          <path d="M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02z" />
        </svg>
        <input
          type="range"
          :value="player.volume"
          min="0"
          max="1"
          step="0.01"
          @input="(e) => player.setVolume(Number((e.target as HTMLInputElement).value))"
          class="flex-1 h-1 bg-slate-600 rounded-full appearance-none cursor-pointer"
        />
      </div>
    </div>
  </div>
</template>
