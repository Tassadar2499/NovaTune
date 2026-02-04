<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { usePlaylistsStore } from '@/stores/playlists';

const playlists = usePlaylistsStore();

const showCreateModal = ref(false);
const newPlaylistName = ref('');
const newPlaylistDescription = ref('');
const isCreating = ref(false);

onMounted(() => {
  playlists.fetchPlaylists();
});

async function createPlaylist() {
  if (!newPlaylistName.value.trim()) return;

  isCreating.value = true;
  try {
    await playlists.createPlaylist(newPlaylistName.value, newPlaylistDescription.value || undefined);
    showCreateModal.value = false;
    newPlaylistName.value = '';
    newPlaylistDescription.value = '';
  } finally {
    isCreating.value = false;
  }
}
</script>

<template>
  <div>
    <div class="flex items-center justify-between mb-8">
      <h1 class="text-2xl font-bold text-white">Playlists</h1>
      <button @click="showCreateModal = true" class="btn-primary flex items-center gap-2">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        New Playlist
      </button>
    </div>

    <div v-if="playlists.isLoading && playlists.playlists.length === 0" class="flex items-center justify-center py-12">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-primary-500"></div>
    </div>

    <div v-else-if="playlists.playlists.length === 0" class="card text-center py-12">
      <svg class="w-16 h-16 text-slate-600 mx-auto mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
      </svg>
      <p class="text-slate-400 text-lg">No playlists yet</p>
      <p class="text-slate-500 mt-2">Create your first playlist to organize your music</p>
      <button @click="showCreateModal = true" class="btn-primary inline-block mt-4">
        Create Playlist
      </button>
    </div>

    <div v-else class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
      <RouterLink
        v-for="playlist in playlists.playlists"
        :key="playlist.id"
        :to="`/playlist/${playlist.id}`"
        class="card hover:bg-slate-700 transition-colors group"
      >
        <div class="w-full aspect-square bg-slate-700 rounded-lg mb-4 flex items-center justify-center">
          <svg class="w-12 h-12 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3" />
          </svg>
        </div>
        <h3 class="text-white font-medium truncate">{{ playlist.name }}</h3>
        <p class="text-slate-400 text-sm">{{ playlist.trackCount }} tracks</p>
      </RouterLink>
    </div>

    <!-- Create playlist modal -->
    <Teleport to="body">
      <div v-if="showCreateModal" class="fixed inset-0 bg-black/50 flex items-center justify-center z-50" @click.self="showCreateModal = false">
        <div class="card w-full max-w-md mx-4">
          <h2 class="text-xl font-semibold text-white mb-4">Create Playlist</h2>
          <form @submit.prevent="createPlaylist" class="space-y-4">
            <div>
              <label for="name" class="block text-sm font-medium text-slate-300 mb-1">Name</label>
              <input
                id="name"
                v-model="newPlaylistName"
                type="text"
                required
                class="input"
                placeholder="My Playlist"
              />
            </div>
            <div>
              <label for="description" class="block text-sm font-medium text-slate-300 mb-1">Description (optional)</label>
              <textarea
                id="description"
                v-model="newPlaylistDescription"
                class="input"
                rows="3"
                placeholder="Describe your playlist"
              ></textarea>
            </div>
            <div class="flex justify-end gap-3">
              <button type="button" @click="showCreateModal = false" class="btn-secondary">
                Cancel
              </button>
              <button type="submit" :disabled="isCreating" class="btn-primary">
                {{ isCreating ? 'Creating...' : 'Create' }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>
  </div>
</template>
