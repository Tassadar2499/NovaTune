import axios, { type AxiosRequestConfig, type AxiosError } from 'axios';

// Create a shared axios instance
const instance = axios.create({
  baseURL: typeof window !== 'undefined' ? (import.meta as any).env?.VITE_API_BASE_URL : '',
  timeout: 30_000,
});

// Track refresh state
let isRefreshing = false;
let refreshPromise: Promise<void> | null = null;

// Token getter/setter - will be overridden by the app
let getAccessToken: () => string | null = () => null;
let getRefreshToken: () => string | null = () => null;
let setTokens: (access: string, refresh: string) => void = () => {};
let onAuthError: () => void = () => {};

/**
 * Configure the custom instance with auth callbacks.
 */
export function configureInstance(config: {
  baseURL?: string;
  getAccessToken: () => string | null;
  getRefreshToken: () => string | null;
  setTokens: (access: string, refresh: string) => void;
  onAuthError: () => void;
}) {
  if (config.baseURL) {
    instance.defaults.baseURL = config.baseURL;
  }
  getAccessToken = config.getAccessToken;
  getRefreshToken = config.getRefreshToken;
  setTokens = config.setTokens;
  onAuthError = config.onAuthError;
}

// Request interceptor
instance.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Response interceptor
instance.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as AxiosRequestConfig & { _retry?: boolean };

    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      if (!isRefreshing) {
        isRefreshing = true;
        const refreshToken = getRefreshToken();

        if (!refreshToken) {
          onAuthError();
          throw error;
        }

        refreshPromise = (async () => {
          try {
            const response = await axios.post(
              `${instance.defaults.baseURL}/auth/refresh`,
              { refreshToken }
            );
            setTokens(response.data.accessToken, response.data.refreshToken);
          } catch {
            onAuthError();
            throw error;
          } finally {
            isRefreshing = false;
            refreshPromise = null;
          }
        })();
      }

      await refreshPromise;
      return instance(originalRequest);
    }

    throw error;
  }
);

/**
 * Custom instance for Orval-generated clients.
 */
export const customInstance = async <T>(config: AxiosRequestConfig): Promise<T> => {
  const response = await instance(config);
  return response.data;
};

export default instance;
