import { defineStore } from 'pinia';
import { ref } from 'vue';
import { useAuthStore } from './auth';
import type { Track } from './player';

export interface TrackFilters {
  search?: string;
  genre?: string;
  sortBy?: 'createdAt' | 'title' | 'artist';
  sortOrder?: 'asc' | 'desc';
}

export interface PaginatedTracks {
  items: Track[];
  nextCursor?: string;
  total: number;
}

export const useLibraryStore = defineStore('library', () => {
  const tracks = ref<Track[]>([]);
  const isLoading = ref(false);
  const error = ref<string | null>(null);
  const nextCursor = ref<string | undefined>();
  const hasMore = ref(true);

  async function fetchTracks(filters: TrackFilters = {}, cursor?: string) {
    const auth = useAuthStore();
    isLoading.value = true;
    error.value = null;

    try {
      const params = new URLSearchParams();
      if (filters.search) params.set('search', filters.search);
      if (filters.genre) params.set('genre', filters.genre);
      if (filters.sortBy) params.set('sortBy', filters.sortBy);
      if (filters.sortOrder) params.set('sortOrder', filters.sortOrder);
      if (cursor) params.set('cursor', cursor);

      const response = await fetch(
        `${import.meta.env.VITE_API_BASE_URL}/tracks?${params.toString()}`,
        {
          headers: {
            Authorization: `Bearer ${auth.accessToken}`,
          },
        }
      );

      if (!response.ok) {
        throw new Error('Failed to fetch tracks');
      }

      const data: PaginatedTracks = await response.json();

      if (cursor) {
        tracks.value = [...tracks.value, ...data.items];
      } else {
        tracks.value = data.items;
      }

      nextCursor.value = data.nextCursor;
      hasMore.value = !!data.nextCursor;
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'An error occurred';
    } finally {
      isLoading.value = false;
    }
  }

  async function loadMore(filters: TrackFilters = {}) {
    if (hasMore.value && nextCursor.value) {
      await fetchTracks(filters, nextCursor.value);
    }
  }

  function reset() {
    tracks.value = [];
    nextCursor.value = undefined;
    hasMore.value = true;
    error.value = null;
  }

  return {
    tracks,
    isLoading,
    error,
    hasMore,
    fetchTracks,
    loadMore,
    reset,
  };
});
