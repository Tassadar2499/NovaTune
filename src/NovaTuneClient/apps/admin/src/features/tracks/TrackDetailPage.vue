<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { format } from 'date-fns';

const route = useRoute();
const router = useRouter();
const auth = useAuthStore();

interface Track {
  id: string;
  title: string;
  artist: string;
  status: 'Active' | 'PendingReview' | 'Disabled' | 'Deleted';
  uploadedBy: { id: string; displayName: string; email: string };
  duration: number;
  playCount: number;
  createdAt: string;
}

const track = ref<Track | null>(null);
const isLoading = ref(true);
const showStatusModal = ref(false);
const newStatus = ref('');
const statusReason = ref('');
const isUpdating = ref(false);

onMounted(async () => {
  try {
    const response = await fetch(
      `${import.meta.env.VITE_API_BASE_URL || '/api'}/admin/tracks/${route.params.id}`,
      {
        headers: {
          Authorization: `Bearer ${auth.accessToken}`,
        },
      }
    );

    if (response.ok) {
      track.value = await response.json();
    }
  } finally {
    isLoading.value = false;
  }
});

async function updateStatus() {
  if (!track.value) return;

  isUpdating.value = true;
  try {
    const response = await fetch(
      `${import.meta.env.VITE_API_BASE_URL || '/api'}/admin/tracks/${track.value.id}/status`,
      {
        method: 'PATCH',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${auth.accessToken}`,
        },
        body: JSON.stringify({ status: newStatus.value, reason: statusReason.value }),
      }
    );

    if (response.ok) {
      track.value.status = newStatus.value as Track['status'];
      showStatusModal.value = false;
    }
  } finally {
    isUpdating.value = false;
  }
}

async function deleteTrack() {
  if (!track.value || !confirm('Are you sure you want to delete this track?')) return;

  try {
    const response = await fetch(
      `${import.meta.env.VITE_API_BASE_URL || '/api'}/admin/tracks/${track.value.id}`,
      {
        method: 'DELETE',
        headers: {
          Authorization: `Bearer ${auth.accessToken}`,
        },
      }
    );

    if (response.ok) {
      router.push('/tracks');
    }
  } catch (e) {
    console.error('Failed to delete track:', e);
  }
}

function formatDate(date: string): string {
  return format(new Date(date), 'MMM d, yyyy HH:mm');
}

function formatDuration(seconds: number): string {
  const mins = Math.floor(seconds / 60);
  const secs = Math.floor(seconds % 60);
  return `${mins}:${secs.toString().padStart(2, '0')}`;
}

const statusColors = {
  Active: 'badge-success',
  PendingReview: 'badge-warning',
  Disabled: 'badge-danger',
  Deleted: 'badge-danger',
};
</script>

<template>
  <div>
    <button @click="router.back()" class="text-slate-400 hover:text-white mb-6 inline-flex items-center gap-2">
      <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
      </svg>
      Back to Tracks
    </button>

    <div v-if="isLoading" class="flex items-center justify-center py-12">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
    </div>

    <div v-else-if="track" class="space-y-6">
      <div class="card">
        <div class="flex items-start justify-between">
          <div class="flex gap-6">
            <div class="w-32 h-32 bg-slate-700 rounded-lg flex items-center justify-center">
              <svg class="w-12 h-12 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3" />
              </svg>
            </div>
            <div>
              <h1 class="text-2xl font-bold text-white">{{ track.title }}</h1>
              <p class="text-slate-400 text-lg">{{ track.artist }}</p>
              <div class="flex items-center gap-2 mt-2">
                <span :class="['badge', statusColors[track.status]]">{{ track.status }}</span>
              </div>
            </div>
          </div>
          <div class="flex gap-2">
            <button @click="showStatusModal = true; newStatus = track?.status || ''" class="btn-secondary">
              Change Status
            </button>
            <button @click="deleteTrack" class="btn-danger">Delete</button>
          </div>
        </div>
      </div>

      <div class="grid grid-cols-4 gap-6">
        <div class="card">
          <p class="text-slate-400 text-sm">Duration</p>
          <p class="text-xl font-bold text-white">{{ formatDuration(track.duration) }}</p>
        </div>
        <div class="card">
          <p class="text-slate-400 text-sm">Play Count</p>
          <p class="text-xl font-bold text-white">{{ track.playCount.toLocaleString() }}</p>
        </div>
        <div class="card">
          <p class="text-slate-400 text-sm">Uploaded</p>
          <p class="text-white font-medium">{{ formatDate(track.createdAt) }}</p>
        </div>
        <div class="card">
          <p class="text-slate-400 text-sm">Uploaded By</p>
          <RouterLink :to="`/users/${track.uploadedBy.id}`" class="text-blue-400 hover:text-blue-300">
            {{ track.uploadedBy.displayName }}
          </RouterLink>
        </div>
      </div>
    </div>

    <!-- Status modal -->
    <Teleport to="body">
      <div v-if="showStatusModal" class="fixed inset-0 bg-black/50 flex items-center justify-center z-50" @click.self="showStatusModal = false">
        <div class="card w-full max-w-md mx-4">
          <h2 class="text-xl font-semibold text-white mb-4">Change Track Status</h2>
          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-slate-300 mb-1">New Status</label>
              <select v-model="newStatus" class="input">
                <option value="Active">Active</option>
                <option value="PendingReview">Pending Review</option>
                <option value="Disabled">Disabled</option>
              </select>
            </div>
            <div>
              <label class="block text-sm font-medium text-slate-300 mb-1">Reason</label>
              <textarea v-model="statusReason" class="input" rows="3" placeholder="Reason for status change"></textarea>
            </div>
            <div class="flex justify-end gap-3">
              <button type="button" @click="showStatusModal = false" class="btn-secondary">Cancel</button>
              <button @click="updateStatus" :disabled="isUpdating" class="btn-primary">
                {{ isUpdating ? 'Updating...' : 'Update Status' }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
