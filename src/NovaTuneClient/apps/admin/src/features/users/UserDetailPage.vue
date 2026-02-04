<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { format } from 'date-fns';

const route = useRoute();
const router = useRouter();
const auth = useAuthStore();

interface User {
  id: string;
  email: string;
  displayName: string;
  status: 'Active' | 'Suspended' | 'Disabled';
  roles: string[];
  createdAt: string;
  trackCount: number;
  playlistCount: number;
}

const user = ref<User | null>(null);
const isLoading = ref(true);
const showStatusModal = ref(false);
const newStatus = ref('');
const statusReason = ref('');
const isUpdating = ref(false);

onMounted(async () => {
  try {
    const response = await fetch(
      `${import.meta.env.VITE_API_BASE_URL}/admin/users/${route.params.id}`,
      {
        headers: {
          Authorization: `Bearer ${auth.accessToken}`,
        },
      }
    );

    if (response.ok) {
      user.value = await response.json();
    }
  } finally {
    isLoading.value = false;
  }
});

async function updateStatus() {
  if (!user.value) return;

  isUpdating.value = true;
  try {
    const response = await fetch(
      `${import.meta.env.VITE_API_BASE_URL}/admin/users/${user.value.id}/status`,
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
      user.value.status = newStatus.value as User['status'];
      showStatusModal.value = false;
    }
  } finally {
    isUpdating.value = false;
  }
}

function formatDate(date: string): string {
  return format(new Date(date), 'MMM d, yyyy HH:mm');
}

const statusColors = {
  Active: 'badge-success',
  Suspended: 'badge-warning',
  Disabled: 'badge-danger',
};
</script>

<template>
  <div>
    <button @click="router.back()" class="text-slate-400 hover:text-white mb-6 inline-flex items-center gap-2">
      <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
      </svg>
      Back to Users
    </button>

    <div v-if="isLoading" class="flex items-center justify-center py-12">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
    </div>

    <div v-else-if="user" class="space-y-6">
      <div class="card">
        <div class="flex items-start justify-between">
          <div class="flex items-center gap-4">
            <div class="w-16 h-16 rounded-full bg-blue-600 flex items-center justify-center text-white text-2xl font-medium">
              {{ user.displayName[0].toUpperCase() }}
            </div>
            <div>
              <h1 class="text-2xl font-bold text-white">{{ user.displayName }}</h1>
              <p class="text-slate-400">{{ user.email }}</p>
              <div class="flex items-center gap-2 mt-2">
                <span :class="['badge', statusColors[user.status]]">{{ user.status }}</span>
                <span v-for="role in user.roles" :key="role" class="badge badge-info">{{ role }}</span>
              </div>
            </div>
          </div>
          <button @click="showStatusModal = true; newStatus = user?.status || ''" class="btn-secondary">
            Change Status
          </button>
        </div>
      </div>

      <div class="grid grid-cols-3 gap-6">
        <div class="card">
          <p class="text-slate-400 text-sm">Tracks</p>
          <p class="text-2xl font-bold text-white">{{ user.trackCount }}</p>
        </div>
        <div class="card">
          <p class="text-slate-400 text-sm">Playlists</p>
          <p class="text-2xl font-bold text-white">{{ user.playlistCount }}</p>
        </div>
        <div class="card">
          <p class="text-slate-400 text-sm">Member Since</p>
          <p class="text-white font-medium">{{ formatDate(user.createdAt) }}</p>
        </div>
      </div>
    </div>

    <!-- Status modal -->
    <Teleport to="body">
      <div v-if="showStatusModal" class="fixed inset-0 bg-black/50 flex items-center justify-center z-50" @click.self="showStatusModal = false">
        <div class="card w-full max-w-md mx-4">
          <h2 class="text-xl font-semibold text-white mb-4">Change User Status</h2>
          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-slate-300 mb-1">New Status</label>
              <select v-model="newStatus" class="input">
                <option value="Active">Active</option>
                <option value="Suspended">Suspended</option>
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
