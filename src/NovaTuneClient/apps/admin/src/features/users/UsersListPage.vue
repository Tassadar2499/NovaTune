<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { format } from 'date-fns';

const auth = useAuthStore();

interface User {
  id: string;
  email: string;
  displayName: string;
  status: 'Active' | 'Suspended' | 'Disabled';
  roles: string[];
  createdAt: string;
}

const users = ref<User[]>([]);
const isLoading = ref(true);
const searchQuery = ref('');
const statusFilter = ref('');

onMounted(async () => {
  await fetchUsers();
});

async function fetchUsers() {
  isLoading.value = true;
  try {
    const params = new URLSearchParams();
    if (searchQuery.value) params.set('search', searchQuery.value);
    if (statusFilter.value) params.set('status', statusFilter.value);

    const response = await fetch(
      `${import.meta.env.VITE_API_BASE_URL}/admin/users?${params.toString()}`,
      {
        headers: {
          Authorization: `Bearer ${auth.accessToken}`,
        },
      }
    );

    if (response.ok) {
      const data = await response.json();
      users.value = data.items || data;
    }
  } finally {
    isLoading.value = false;
  }
}

const statusColors = {
  Active: 'badge-success',
  Suspended: 'badge-warning',
  Disabled: 'badge-danger',
};

function formatDate(date: string): string {
  return format(new Date(date), 'MMM d, yyyy');
}
</script>

<template>
  <div>
    <div class="flex items-center justify-between mb-8">
      <h1 class="text-2xl font-bold text-white">Users</h1>
    </div>

    <div class="card mb-6">
      <div class="flex gap-4">
        <div class="flex-1">
          <input
            v-model="searchQuery"
            type="search"
            placeholder="Search users..."
            class="input"
            @keyup.enter="fetchUsers"
          />
        </div>
        <select v-model="statusFilter" class="input w-40" @change="fetchUsers">
          <option value="">All statuses</option>
          <option value="Active">Active</option>
          <option value="Suspended">Suspended</option>
          <option value="Disabled">Disabled</option>
        </select>
        <button @click="fetchUsers" class="btn-primary">Search</button>
      </div>
    </div>

    <div v-if="isLoading" class="flex items-center justify-center py-12">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
    </div>

    <div v-else class="card overflow-hidden">
      <table class="w-full">
        <thead class="bg-slate-900">
          <tr>
            <th class="table-header">User</th>
            <th class="table-header">Status</th>
            <th class="table-header">Roles</th>
            <th class="table-header">Created</th>
            <th class="table-header">Actions</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-700">
          <tr v-for="user in users" :key="user.id" class="hover:bg-slate-700/50">
            <td class="table-cell">
              <div class="flex items-center gap-3">
                <div class="w-8 h-8 rounded-full bg-blue-600 flex items-center justify-center text-white text-sm font-medium">
                  {{ user.displayName[0].toUpperCase() }}
                </div>
                <div>
                  <p class="text-white font-medium">{{ user.displayName }}</p>
                  <p class="text-slate-400 text-xs">{{ user.email }}</p>
                </div>
              </div>
            </td>
            <td class="table-cell">
              <span :class="['badge', statusColors[user.status]]">
                {{ user.status }}
              </span>
            </td>
            <td class="table-cell">
              <span v-for="role in user.roles" :key="role" class="badge badge-info mr-1">
                {{ role }}
              </span>
            </td>
            <td class="table-cell">{{ formatDate(user.createdAt) }}</td>
            <td class="table-cell">
              <RouterLink :to="`/users/${user.id}`" class="text-blue-400 hover:text-blue-300">
                View
              </RouterLink>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
