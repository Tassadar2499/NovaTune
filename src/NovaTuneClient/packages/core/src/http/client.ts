import axios, { type AxiosInstance, type AxiosRequestConfig } from 'axios';
import { setupInterceptors } from './interceptors';

export interface HttpClientConfig {
  baseURL: string;
  timeout?: number;
  getAccessToken: () => string | null;
  refreshTokens: () => Promise<void>;
  onAuthError?: () => void;
}

/**
 * Creates a configured Axios instance with auth interceptors.
 */
export function createHttpClient(config: HttpClientConfig): AxiosInstance {
  const instance = axios.create({
    baseURL: config.baseURL,
    timeout: config.timeout ?? 30_000,
    headers: {
      'Content-Type': 'application/json',
    },
  });

  setupInterceptors(instance, {
    getAccessToken: config.getAccessToken,
    refreshTokens: config.refreshTokens,
    onAuthError: config.onAuthError,
  });

  return instance;
}

/**
 * Custom instance function for use with Orval-generated clients.
 */
export function createCustomInstance(instance: AxiosInstance) {
  return async <T>(config: AxiosRequestConfig): Promise<T> => {
    const response = await instance(config);
    return response.data;
  };
}
