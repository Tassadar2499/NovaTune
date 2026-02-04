<script setup lang="ts">
import { ref } from 'vue';

const timeRange = ref('7d');

const timeRanges = [
  { value: '24h', label: 'Last 24 hours' },
  { value: '7d', label: 'Last 7 days' },
  { value: '30d', label: 'Last 30 days' },
  { value: '90d', label: 'Last 90 days' },
];
</script>

<template>
  <div>
    <div class="flex items-center justify-between mb-8">
      <h1 class="text-2xl font-bold text-white">Analytics</h1>
      <select v-model="timeRange" class="input w-48">
        <option v-for="range in timeRanges" :key="range.value" :value="range.value">
          {{ range.label }}
        </option>
      </select>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
      <div class="card">
        <h2 class="text-lg font-semibold text-white mb-4">Plays Over Time</h2>
        <div class="h-64 flex items-center justify-center text-slate-400">
          Chart placeholder - integrate with charting library
        </div>
      </div>

      <div class="card">
        <h2 class="text-lg font-semibold text-white mb-4">User Activity</h2>
        <div class="h-64 flex items-center justify-center text-slate-400">
          Chart placeholder - integrate with charting library
        </div>
      </div>

      <div class="card">
        <h2 class="text-lg font-semibold text-white mb-4">Top Genres</h2>
        <div class="h-64 flex items-center justify-center text-slate-400">
          Chart placeholder - integrate with charting library
        </div>
      </div>

      <div class="card">
        <h2 class="text-lg font-semibold text-white mb-4">Upload Trends</h2>
        <div class="h-64 flex items-center justify-center text-slate-400">
          Chart placeholder - integrate with charting library
        </div>
      </div>
    </div>
  </div>
</template>
