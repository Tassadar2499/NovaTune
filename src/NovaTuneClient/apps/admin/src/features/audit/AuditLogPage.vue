<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { format } from 'date-fns';

const auth = useAuthStore();

interface AuditLog {
  id: string;
  timestamp: string;
  actorId: string;
  actorEmail: string;
  action: string;
  resourceType: string;
  resourceId: string;
  details: Record<string, unknown>;
  previousHash?: string;
  currentHash: string;
}

const logs = ref<AuditLog[]>([]);
const isLoading = ref(true);
const isVerifying = ref(false);
const verificationResult = ref<{ valid: boolean; message: string } | null>(null);
const actionFilter = ref('');
const resourceTypeFilter = ref('');

onMounted(async () => {
  await fetchLogs();
});

async function fetchLogs() {
  isLoading.value = true;
  try {
    const params = new URLSearchParams();
    if (actionFilter.value) params.set('action', actionFilter.value);
    if (resourceTypeFilter.value) params.set('resourceType', resourceTypeFilter.value);

    const response = await fetch(
      `${import.meta.env.VITE_API_BASE_URL}/admin/audit-logs?${params.toString()}`,
      {
        headers: {
          Authorization: `Bearer ${auth.accessToken}`,
        },
      }
    );

    if (response.ok) {
      const data = await response.json();
      logs.value = data.items || data;
    }
  } finally {
    isLoading.value = false;
  }
}

async function verifyIntegrity() {
  isVerifying.value = true;
  verificationResult.value = null;

  try {
    const response = await fetch(
      `${import.meta.env.VITE_API_BASE_URL}/admin/audit-logs/verify`,
      {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${auth.accessToken}`,
        },
      }
    );

    if (response.ok) {
      const data = await response.json();
      verificationResult.value = {
        valid: data.isValid,
        message: data.isValid
          ? 'Hash chain integrity verified successfully'
          : `Integrity check failed at entry ${data.failedAtId}`,
      };
    }
  } finally {
    isVerifying.value = false;
  }
}

function formatDate(date: string): string {
  return format(new Date(date), 'MMM d, yyyy HH:mm:ss');
}

const actionColors: Record<string, string> = {
  UserStatusChanged: 'badge-warning',
  TrackStatusChanged: 'badge-warning',
  TrackDeleted: 'badge-danger',
  UserCreated: 'badge-success',
  Login: 'badge-info',
};
</script>

<template>
  <div>
    <div class="flex items-center justify-between mb-8">
      <h1 class="text-2xl font-bold text-white">Audit Logs</h1>
      <button @click="verifyIntegrity" :disabled="isVerifying" class="btn-primary">
        {{ isVerifying ? 'Verifying...' : 'Verify Integrity' }}
      </button>
    </div>

    <div v-if="verificationResult" :class="['card mb-6', verificationResult.valid ? 'border-green-700 bg-green-900/20' : 'border-red-700 bg-red-900/20']">
      <div class="flex items-center gap-3">
        <svg v-if="verificationResult.valid" class="w-6 h-6 text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
        </svg>
        <svg v-else class="w-6 h-6 text-red-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
        </svg>
        <p :class="verificationResult.valid ? 'text-green-200' : 'text-red-200'">
          {{ verificationResult.message }}
        </p>
      </div>
    </div>

    <div class="card mb-6">
      <div class="flex gap-4">
        <select v-model="actionFilter" class="input w-48" @change="fetchLogs">
          <option value="">All actions</option>
          <option value="UserStatusChanged">User Status Changed</option>
          <option value="TrackStatusChanged">Track Status Changed</option>
          <option value="TrackDeleted">Track Deleted</option>
          <option value="Login">Login</option>
        </select>
        <select v-model="resourceTypeFilter" class="input w-48" @change="fetchLogs">
          <option value="">All resources</option>
          <option value="User">User</option>
          <option value="Track">Track</option>
          <option value="Playlist">Playlist</option>
        </select>
      </div>
    </div>

    <div v-if="isLoading" class="flex items-center justify-center py-12">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
    </div>

    <div v-else class="space-y-3">
      <div v-for="log in logs" :key="log.id" class="card">
        <div class="flex items-start justify-between">
          <div class="flex-1">
            <div class="flex items-center gap-3 mb-2">
              <span :class="['badge', actionColors[log.action] || 'badge-info']">
                {{ log.action }}
              </span>
              <span class="text-slate-400 text-sm">{{ log.resourceType }} / {{ log.resourceId }}</span>
            </div>
            <p class="text-white">
              <span class="text-slate-400">by</span> {{ log.actorEmail }}
            </p>
            <div v-if="Object.keys(log.details).length > 0" class="mt-2 text-sm text-slate-400">
              <details>
                <summary class="cursor-pointer hover:text-slate-300">Details</summary>
                <pre class="mt-2 p-2 bg-slate-900 rounded text-xs overflow-auto">{{ JSON.stringify(log.details, null, 2) }}</pre>
              </details>
            </div>
          </div>
          <div class="text-right">
            <p class="text-slate-400 text-sm">{{ formatDate(log.timestamp) }}</p>
            <p class="text-slate-500 text-xs mt-1 font-mono" title="Hash">
              {{ log.currentHash.substring(0, 16) }}...
            </p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
