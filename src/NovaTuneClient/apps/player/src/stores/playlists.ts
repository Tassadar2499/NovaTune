import { defineStore } from 'pinia';
import { ref } from 'vue';
import { useAuthStore } from './auth';
import type { Track } from './player';

export interface Playlist {
  id: string;
  name: string;
  description?: string;
  trackCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface PlaylistWithTracks extends Playlist {
  tracks: Track[];
}

export const usePlaylistsStore = defineStore('playlists', () => {
  const playlists = ref<Playlist[]>([]);
  const currentPlaylist = ref<PlaylistWithTracks | null>(null);
  const isLoading = ref(false);
  const error = ref<string | null>(null);

  async function fetchPlaylists() {
    const auth = useAuthStore();
    isLoading.value = true;
    error.value = null;

    try {
      const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/playlists`, {
        headers: {
          Authorization: `Bearer ${auth.accessToken}`,
        },
      });

      if (!response.ok) {
        throw new Error('Failed to fetch playlists');
      }

      playlists.value = await response.json();
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'An error occurred';
    } finally {
      isLoading.value = false;
    }
  }

  async function fetchPlaylist(id: string) {
    const auth = useAuthStore();
    isLoading.value = true;
    error.value = null;

    try {
      const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/playlists/${id}`, {
        headers: {
          Authorization: `Bearer ${auth.accessToken}`,
        },
      });

      if (!response.ok) {
        throw new Error('Failed to fetch playlist');
      }

      currentPlaylist.value = await response.json();
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'An error occurred';
    } finally {
      isLoading.value = false;
    }
  }

  async function createPlaylist(name: string, description?: string) {
    const auth = useAuthStore();

    const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/playlists`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${auth.accessToken}`,
      },
      body: JSON.stringify({ name, description }),
    });

    if (!response.ok) {
      throw new Error('Failed to create playlist');
    }

    const newPlaylist = await response.json();
    playlists.value.push(newPlaylist);
    return newPlaylist;
  }

  async function updatePlaylist(id: string, updates: { name?: string; description?: string }) {
    const auth = useAuthStore();

    const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/playlists/${id}`, {
      method: 'PATCH',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${auth.accessToken}`,
      },
      body: JSON.stringify(updates),
    });

    if (!response.ok) {
      throw new Error('Failed to update playlist');
    }

    const updatedPlaylist = await response.json();
    const index = playlists.value.findIndex((p) => p.id === id);
    if (index !== -1) {
      playlists.value[index] = updatedPlaylist;
    }
    return updatedPlaylist;
  }

  async function deletePlaylist(id: string) {
    const auth = useAuthStore();

    const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/playlists/${id}`, {
      method: 'DELETE',
      headers: {
        Authorization: `Bearer ${auth.accessToken}`,
      },
    });

    if (!response.ok) {
      throw new Error('Failed to delete playlist');
    }

    playlists.value = playlists.value.filter((p) => p.id !== id);
  }

  async function addTrackToPlaylist(playlistId: string, trackId: string) {
    const auth = useAuthStore();

    const response = await fetch(
      `${import.meta.env.VITE_API_BASE_URL}/playlists/${playlistId}/tracks`,
      {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${auth.accessToken}`,
        },
        body: JSON.stringify({ trackId }),
      }
    );

    if (!response.ok) {
      throw new Error('Failed to add track to playlist');
    }

    // Refresh playlist if it's the current one
    if (currentPlaylist.value?.id === playlistId) {
      await fetchPlaylist(playlistId);
    }
  }

  async function removeTrackFromPlaylist(playlistId: string, trackId: string) {
    const auth = useAuthStore();

    const response = await fetch(
      `${import.meta.env.VITE_API_BASE_URL}/playlists/${playlistId}/tracks/${trackId}`,
      {
        method: 'DELETE',
        headers: {
          Authorization: `Bearer ${auth.accessToken}`,
        },
      }
    );

    if (!response.ok) {
      throw new Error('Failed to remove track from playlist');
    }

    // Refresh playlist if it's the current one
    if (currentPlaylist.value?.id === playlistId) {
      await fetchPlaylist(playlistId);
    }
  }

  async function reorderPlaylistTracks(playlistId: string, trackId: string, newPosition: number) {
    const auth = useAuthStore();

    const response = await fetch(
      `${import.meta.env.VITE_API_BASE_URL}/playlists/${playlistId}/reorder`,
      {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${auth.accessToken}`,
        },
        body: JSON.stringify({ trackId, newPosition }),
      }
    );

    if (!response.ok) {
      throw new Error('Failed to reorder playlist');
    }

    // Refresh playlist
    if (currentPlaylist.value?.id === playlistId) {
      await fetchPlaylist(playlistId);
    }
  }

  return {
    playlists,
    currentPlaylist,
    isLoading,
    error,
    fetchPlaylists,
    fetchPlaylist,
    createPlaylist,
    updatePlaylist,
    deletePlaylist,
    addTrackToPlaylist,
    removeTrackFromPlaylist,
    reorderPlaylistTracks,
  };
});
