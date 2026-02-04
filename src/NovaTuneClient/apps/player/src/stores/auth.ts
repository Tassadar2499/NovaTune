import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { getOrCreateDeviceId } from '@novatune/core/device';
import type { User } from '@novatune/api-client';

export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string | null>(null);
  const refreshToken = ref<string | null>(localStorage.getItem('refresh_token'));
  const user = ref<User | null>(null);

  const isAuthenticated = computed(() => !!accessToken.value);
  const deviceId = getOrCreateDeviceId();

  async function login(email: string, password: string) {
    const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password, deviceId }),
    });

    if (!response.ok) {
      throw new Error('Login failed');
    }

    const data = await response.json();
    accessToken.value = data.accessToken;
    refreshToken.value = data.refreshToken;
    user.value = data.user;

    localStorage.setItem('refresh_token', data.refreshToken);
  }

  async function refreshTokens() {
    if (!refreshToken.value) {
      throw new Error('No refresh token');
    }

    const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/auth/refresh`, {
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
    localStorage.setItem('refresh_token', data.refreshToken);
  }

  async function logout() {
    try {
      await fetch(`${import.meta.env.VITE_API_BASE_URL}/auth/logout`, {
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
      localStorage.removeItem('refresh_token');
    }
  }

  async function register(email: string, password: string, displayName: string) {
    const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password, displayName, deviceId }),
    });

    if (!response.ok) {
      throw new Error('Registration failed');
    }

    const data = await response.json();
    accessToken.value = data.accessToken;
    refreshToken.value = data.refreshToken;
    user.value = data.user;

    localStorage.setItem('refresh_token', data.refreshToken);
  }

  return {
    accessToken,
    user,
    isAuthenticated,
    deviceId,
    login,
    register,
    refreshTokens,
    logout,
  };
});
