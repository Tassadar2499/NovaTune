import type { AxiosInstance, AxiosError, InternalAxiosRequestConfig } from 'axios';
import { parseApiError } from '../errors';

export interface InterceptorConfig {
  getAccessToken: () => string | null;
  refreshTokens: () => Promise<void>;
  onAuthError?: () => void;
}

interface RetryableConfig extends InternalAxiosRequestConfig {
  _retry?: boolean;
}

let isRefreshing = false;
let refreshPromise: Promise<void> | null = null;

/**
 * Sets up request and response interceptors for authentication and error handling.
 */
export function setupInterceptors(instance: AxiosInstance, config: InterceptorConfig): void {
  // Request interceptor: Add auth header
  instance.interceptors.request.use(
    (requestConfig) => {
      const token = config.getAccessToken();
      if (token) {
        requestConfig.headers.Authorization = `Bearer ${token}`;
      }
      return requestConfig;
    },
    (error) => Promise.reject(error)
  );

  // Response interceptor: Handle 401 and transform errors
  instance.interceptors.response.use(
    (response) => response,
    async (error: AxiosError) => {
      const originalRequest = error.config as RetryableConfig;

      // Handle 401 Unauthorized
      if (error.response?.status === 401 && !originalRequest._retry) {
        originalRequest._retry = true;

        // Use single-flight pattern for token refresh
        if (!isRefreshing) {
          isRefreshing = true;
          refreshPromise = config.refreshTokens().finally(() => {
            isRefreshing = false;
            refreshPromise = null;
          });
        }

        try {
          await refreshPromise;
          // Retry the original request with new token
          return instance(originalRequest);
        } catch {
          // Refresh failed, trigger auth error callback
          config.onAuthError?.();
          throw parseApiError(error);
        }
      }

      // Transform all errors to ApiError
      throw parseApiError(error);
    }
  );
}
