<script setup lang="ts">
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';

const router = useRouter();
const auth = useAuthStore();

const displayName = ref('');
const email = ref('');
const password = ref('');
const confirmPassword = ref('');
const isLoading = ref(false);
const error = ref<string | null>(null);

async function handleSubmit() {
  if (password.value !== confirmPassword.value) {
    error.value = 'Passwords do not match';
    return;
  }

  if (password.value.length < 8) {
    error.value = 'Password must be at least 8 characters';
    return;
  }

  isLoading.value = true;
  error.value = null;

  try {
    await auth.register(email.value, password.value, displayName.value);
    router.push({ name: 'login', query: { registered: 'true' } });
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Registration failed';
  } finally {
    isLoading.value = false;
  }
}
</script>

<template>
  <form @submit.prevent="handleSubmit" class="space-y-4">
    <h2 class="text-xl font-semibold text-white mb-6">Create Account</h2>

    <div v-if="error" class="p-3 bg-red-900/50 border border-red-700 rounded-lg text-red-200 text-sm">
      {{ error }}
    </div>

    <div>
      <label for="displayName" class="block text-sm font-medium text-slate-300 mb-1">Display Name</label>
      <input
        id="displayName"
        v-model="displayName"
        type="text"
        required
        class="input"
        placeholder="Your name"
      />
    </div>

    <div>
      <label for="email" class="block text-sm font-medium text-slate-300 mb-1">Email</label>
      <input
        id="email"
        v-model="email"
        type="email"
        required
        class="input"
        placeholder="you@example.com"
      />
    </div>

    <div>
      <label for="password" class="block text-sm font-medium text-slate-300 mb-1">Password</label>
      <input
        id="password"
        v-model="password"
        type="password"
        required
        minlength="8"
        class="input"
        placeholder="At least 8 characters"
      />
    </div>

    <div>
      <label for="confirmPassword" class="block text-sm font-medium text-slate-300 mb-1">Confirm Password</label>
      <input
        id="confirmPassword"
        v-model="confirmPassword"
        type="password"
        required
        class="input"
        placeholder="Confirm your password"
      />
    </div>

    <button
      type="submit"
      :disabled="isLoading"
      class="w-full btn-primary disabled:opacity-50"
    >
      {{ isLoading ? 'Creating account...' : 'Create Account' }}
    </button>

    <p class="text-center text-sm text-slate-400">
      Already have an account?
      <RouterLink :to="{ name: 'login' }" class="text-blue-400 hover:text-blue-300">
        Sign in
      </RouterLink>
    </p>
  </form>
</template>
