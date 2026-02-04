<script setup lang="ts">
import { RouterView } from 'vue-router';
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-slate-900">
    <div class="w-full max-w-md px-6">
      <div class="text-center mb-8">
        <h1 class="text-3xl font-bold text-white">NovaTune</h1>
        <p class="text-slate-400 mt-2">Your music, anywhere</p>
      </div>
      <div class="card">
        <RouterView />
      </div>
    </div>
  </div>
</template>
