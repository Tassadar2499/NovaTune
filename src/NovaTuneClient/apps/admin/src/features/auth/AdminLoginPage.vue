<script setup lang="ts">
import { ref } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { useAuthStore } from '@/stores/auth';

const router = useRouter();
const route = useRoute();
const auth = useAuthStore();

const email = ref('');
const password = ref('');
const isLoading = ref(false);
const error = ref<string | null>(null);

async function handleSubmit() {
  isLoading.value = true;
  error.value = null;

  try {
    await auth.login(email.value, password.value);
    const redirect = route.query.redirect as string | undefined;
    router.push(redirect || '/');
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Login failed';
  } finally {
    isLoading.value = false;
  }
}
</script>

<template>
  <form @submit.prevent="handleSubmit" class="space-y-4">
    <h2 class="text-xl font-semibold text-white mb-6">Sign In</h2>

    <div v-if="route.query.registered === 'true'" class="p-3 bg-green-900/50 border border-green-700 rounded-lg text-green-200 text-sm">
      Account created successfully. Please sign in.
    </div>

    <div v-if="error" class="p-3 bg-red-900/50 border border-red-700 rounded-lg text-red-200 text-sm">
      {{ error }}
    </div>

    <div>
      <label for="email" class="block text-sm font-medium text-slate-300 mb-1">Email</label>
      <input
        id="email"
        v-model="email"
        type="email"
        required
        class="input"
        placeholder="admin@example.com"
      />
    </div>

    <div>
      <label for="password" class="block text-sm font-medium text-slate-300 mb-1">Password</label>
      <input
        id="password"
        v-model="password"
        type="password"
        required
        class="input"
        placeholder="Enter your password"
      />
    </div>

    <button
      type="submit"
      :disabled="isLoading"
      class="w-full btn-primary disabled:opacity-50"
    >
      {{ isLoading ? 'Signing in...' : 'Sign In' }}
    </button>

    <p class="text-center text-sm text-slate-400">
      Don't have an account?
      <RouterLink :to="{ name: 'register' }" class="text-blue-400 hover:text-blue-300">
        Sign up
      </RouterLink>
    </p>
  </form>
</template>
