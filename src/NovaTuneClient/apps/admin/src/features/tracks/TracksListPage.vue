<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { format } from 'date-fns';

const auth = useAuthStore();

interface Track {
  id: string;
  title: string;
  artist: string;
  status: 'Active' | 'PendingReview' | 'Disabled' | 'Deleted';
  uploadedBy: { id: string; displayName: string };
  duration: number;
  createdAt: string;
}

const tracks = ref<Track[]>([]);
const isLoading = ref(true);
const searchQuery = ref('');
const statusFilter = ref('');

onMounted(async () => {
  await fetchTracks();
});

async function fetchTracks() {
  isLoading.value = true;
  try {
    const params = new URLSearchParams();
    if (searchQuery.value) params.set('search', searchQuery.value);
    if (statusFilter.value) params.set('status', statusFilter.value);

    const response = await fetch(
      `${import.meta.env.VITE_API_BASE_URL}/admin/tracks?${params.toString()}`,
      {
        headers: {
          Authorization: `Bearer ${auth.accessToken}`,
        },
      }
    );

    if (response.ok) {
      const data = await response.json();
      tracks.value = data.items || data;
    }
  } finally {
    isLoading.value = false;
  }
}

const statusColors = {
  Active: 'badge-success',
  PendingReview: 'badge-warning',
  Disabled: 'badge-danger',
  Deleted: 'badge-danger',
};

function formatDate(date: string): string {
  return format(new Date(date), 'MMM d, yyyy');
}

function formatDuration(seconds: number): string {
  const mins = Math.floor(seconds / 60);
  const secs = Math.floor(seconds % 60);
  return `${mins}:${secs.toString().padStart(2, '0')}`;
}
</script>

<template>
  <div>
    <div class="flex items-center justify-between mb-8">
      <h1 class="text-2xl font-bold text-white">Tracks</h1>
    </div>

    <div class="card mb-6">
      <div class="flex gap-4">
        <div class="flex-1">
          <input
            v-model="searchQuery"
            type="search"
            placeholder="Search tracks..."
            class="input"
            @keyup.enter="fetchTracks"
          />
        </div>
        <select v-model="statusFilter" class="input w-48" @change="fetchTracks">
          <option value="">All statuses</option>
          <option value="Active">Active</option>
          <option value="PendingReview">Pending Review</option>
          <option value="Disabled">Disabled</option>
        </select>
        <button @click="fetchTracks" class="btn-primary">Search</button>
      </div>
    </div>

    <div v-if="isLoading" class="flex items-center justify-center py-12">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
    </div>

    <div v-else class="card overflow-hidden">
      <table class="w-full">
        <thead class="bg-slate-900">
          <tr>
            <th class="table-header">Track</th>
            <th class="table-header">Status</th>
            <th class="table-header">Uploaded By</th>
            <th class="table-header">Duration</th>
            <th class="table-header">Created</th>
            <th class="table-header">Actions</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-700">
          <tr v-for="track in tracks" :key="track.id" class="hover:bg-slate-700/50">
            <td class="table-cell">
              <div>
                <p class="text-white font-medium">{{ track.title }}</p>
                <p class="text-slate-400 text-xs">{{ track.artist }}</p>
              </div>
            </td>
            <td class="table-cell">
              <span :class="['badge', statusColors[track.status]]">
                {{ track.status }}
              </span>
            </td>
            <td class="table-cell">
              <RouterLink :to="`/users/${track.uploadedBy.id}`" class="text-blue-400 hover:text-blue-300">
                {{ track.uploadedBy.displayName }}
              </RouterLink>
            </td>
            <td class="table-cell">{{ formatDuration(track.duration) }}</td>
            <td class="table-cell">{{ formatDate(track.createdAt) }}</td>
            <td class="table-cell">
              <RouterLink :to="`/tracks/${track.id}`" class="text-blue-400 hover:text-blue-300">
                View
              </RouterLink>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
