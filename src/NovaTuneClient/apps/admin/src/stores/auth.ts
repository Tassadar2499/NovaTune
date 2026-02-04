import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { getOrCreateDeviceId } from '@novatune/core/device';

export interface User {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
}

export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string | null>(null);
  const refreshToken = ref<string | null>(localStorage.getItem('admin_refresh_token'));
  const user = ref<User | null>(null);

  const isAuthenticated = computed(() => !!accessToken.value);
  const isAdmin = computed(() => user.value?.roles.includes('Admin') ?? false);
  const deviceId = getOrCreateDeviceId();

  async function login(email: string, password: string) {
    const apiBase = import.meta.env.VITE_API_BASE_URL || '/api';
    const response = await fetch(`${apiBase}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password, deviceId }),
    });

    if (!response.ok) {
      throw new Error('Login failed');
    }

    const data = await response.json();

    // Check if user has admin role
    if (!data.user.roles.includes('Admin')) {
      throw new Error('Admin access required');
    }

    accessToken.value = data.accessToken;
    refreshToken.value = data.refreshToken;
    user.value = data.user;

    localStorage.setItem('admin_refresh_token', data.refreshToken);
  }

  async function refreshTokens() {
    if (!refreshToken.value) {
      throw new Error('No refresh token');
    }

    const apiBase = import.meta.env.VITE_API_BASE_URL || '/api';
    const response = await fetch(`${apiBase}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: refreshToken.value, deviceId }),
    });

    if (!response.ok) {
      throw new Error('Token refresh failed');
    }

    const data = await response.json();
    accessToken.value = data.accessToken;
    refreshToken.value = data.refreshToken;
    localStorage.setItem('admin_refresh_token', data.refreshToken);
  }

  async function logout() {
    const apiBase = import.meta.env.VITE_API_BASE_URL || '/api';
    try {
      await fetch(`${apiBase}/auth/logout`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${accessToken.value}`,
        },
        body: JSON.stringify({ deviceId }),
      });
    } finally {
      accessToken.value = null;
      refreshToken.value = null;
      user.value = null;
      localStorage.removeItem('admin_refresh_token');
    }
  }

  return {
    accessToken,
    user,
    isAuthenticated,
    isAdmin,
    deviceId,
    login,
    refreshTokens,
    logout,
  };
});
