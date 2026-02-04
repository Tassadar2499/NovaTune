<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useAuthStore } from '@/stores/auth';

const auth = useAuthStore();

interface OverviewStats {
  totalUsers: number;
  totalTracks: number;
  totalPlays: number;
  activeUsers24h: number;
}

const stats = ref<OverviewStats | null>(null);
const isLoading = ref(true);

onMounted(async () => {
  try {
    const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/admin/analytics/overview`, {
      headers: {
        Authorization: `Bearer ${auth.accessToken}`,
      },
    });

    if (response.ok) {
      stats.value = await response.json();
    }
  } finally {
    isLoading.value = false;
  }
});

const statCards = [
  { key: 'totalUsers', label: 'Total Users', icon: 'M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z', color: 'blue' },
  { key: 'totalTracks', label: 'Total Tracks', icon: 'M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3', color: 'green' },
  { key: 'totalPlays', label: 'Total Plays', icon: 'M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z M21 12a9 9 0 11-18 0 9 9 0 0118 0z', color: 'purple' },
  { key: 'activeUsers24h', label: 'Active (24h)', icon: 'M13 10V3L4 14h7v7l9-11h-7z', color: 'yellow' },
];

const colorClasses = {
  blue: 'bg-blue-900/50 text-blue-400',
  green: 'bg-green-900/50 text-green-400',
  purple: 'bg-purple-900/50 text-purple-400',
  yellow: 'bg-yellow-900/50 text-yellow-400',
};
</script>

<template>
  <div>
    <h1 class="text-2xl font-bold text-white mb-8">Dashboard</h1>

    <div v-if="isLoading" class="flex items-center justify-center py-12">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
    </div>

    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
      <div v-for="card in statCards" :key="card.key" class="card">
        <div class="flex items-center gap-4">
          <div :class="['w-12 h-12 rounded-lg flex items-center justify-center', colorClasses[card.color as keyof typeof colorClasses]]">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" :d="card.icon" />
            </svg>
          </div>
          <div>
            <p class="text-slate-400 text-sm">{{ card.label }}</p>
            <p class="text-2xl font-bold text-white">
              {{ stats?.[card.key as keyof OverviewStats]?.toLocaleString() ?? '-' }}
            </p>
          </div>
        </div>
      </div>
    </div>

    <div class="mt-8 grid grid-cols-1 lg:grid-cols-2 gap-6">
      <div class="card">
        <h2 class="text-lg font-semibold text-white mb-4">Recent Activity</h2>
        <p class="text-slate-400">Activity chart will be displayed here.</p>
      </div>

      <div class="card">
        <h2 class="text-lg font-semibold text-white mb-4">Top Tracks</h2>
        <p class="text-slate-400">Top tracks list will be displayed here.</p>
      </div>
    </div>
  </div>
</template>
