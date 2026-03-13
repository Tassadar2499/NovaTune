import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { getOrCreateDeviceId } from '@novatune/core/device';

interface AuthErrorResponse
{
  detail?: string;
  title?: string;
  message?: string;
}

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
  const isInitializing = ref(false);

  const isAuthenticated = computed(() => !!accessToken.value);
  const isAdmin = computed(() => user.value?.roles?.includes('Admin') ?? false);
  const deviceId = getOrCreateDeviceId();
  const apiBase = import.meta.env.VITE_API_BASE_URL || '/api';

  function getAuthHeaders(includeAccessToken = false): Record<string, string> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      'X-Device-Id': deviceId,
    };

    if (includeAccessToken && accessToken.value) {
      headers.Authorization = `Bearer ${accessToken.value}`;
    }

    return headers;
  }

  async function getErrorMessage(response: Response, fallback: string): Promise<string> {
    const payload = await response.json().catch(() => null) as AuthErrorResponse | null;
    return payload?.detail ?? payload?.message ?? payload?.title ?? fallback;
  }

  function clearSession() {
    accessToken.value = null;
    refreshToken.value = null;
    user.value = null;
    localStorage.removeItem('admin_refresh_token');
  }

  async function login(email: string, password: string) {
    const response = await fetch(`${apiBase}/auth/login`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ email, password }),
    });

    if (!response.ok) {
      throw new Error(await getErrorMessage(response, 'Login failed'));
    }

    const data = await response.json();

    // Check if user has admin role
    if (!data.user?.roles?.includes('Admin')) {
      clearSession();
      throw new Error('Admin access required');
    }

    accessToken.value = data.accessToken;
    refreshToken.value = data.refreshToken;
    user.value = data.user;

    localStorage.setItem('admin_refresh_token', data.refreshToken);
  }

  async function register(email: string, password: string, displayName: string) {
    const response = await fetch(`${apiBase}/auth/register`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ email, password, displayName }),
    });

    if (!response.ok) {
      throw new Error(await getErrorMessage(response, 'Registration failed'));
    }

    return await response.json();
  }

  async function refreshTokens() {
    if (!refreshToken.value) {
      throw new Error('No refresh token');
    }

    const response = await fetch(`${apiBase}/auth/refresh`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ refreshToken: refreshToken.value }),
    });

    if (!response.ok) {
      const errorMessage = await getErrorMessage(response, 'Token refresh failed');
      clearSession();
      throw new Error(errorMessage);
    }

    const data = await response.json();
    if (!data.user?.roles?.includes('Admin')) {
      clearSession();
      throw new Error('Admin access required');
    }

    accessToken.value = data.accessToken;
    refreshToken.value = data.refreshToken;
    user.value = data.user;
    localStorage.setItem('admin_refresh_token', data.refreshToken);
  }

  async function logout() {
    try {
      await fetch(`${apiBase}/auth/logout`, {
        method: 'POST',
        headers: getAuthHeaders(true),
        body: JSON.stringify(
          refreshToken.value
            ? { refreshToken: refreshToken.value }
            : {}
        ),
      });
    } finally {
      clearSession();
    }
  }

  async function initializeSession() {
    if (!refreshToken.value || isInitializing.value || accessToken.value) {
      return;
    }

    isInitializing.value = true;

    try {
      await refreshTokens();
    } catch {
      // Stale sessions should silently fall back to login.
    } finally {
      isInitializing.value = false;
    }
  }

  return {
    accessToken,
    user,
    isAuthenticated,
    isAdmin,
    isInitializing,
    deviceId,
    login,
    register,
    refreshTokens,
    initializeSession,
    logout,
  };
});
