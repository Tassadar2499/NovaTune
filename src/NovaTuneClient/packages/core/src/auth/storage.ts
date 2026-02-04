const REFRESH_TOKEN_KEY = 'novatune_refresh_token';

/**
 * Token storage interface for different platforms.
 */
export interface TokenStorage {
  getRefreshToken(): Promise<string | null>;
  setRefreshToken(token: string): Promise<void>;
  clearRefreshToken(): Promise<void>;
}

/**
 * LocalStorage-based token storage for web.
 */
export const webStorage: TokenStorage = {
  async getRefreshToken() {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  },

  async setRefreshToken(token: string) {
    localStorage.setItem(REFRESH_TOKEN_KEY, token);
  },

  async clearRefreshToken() {
    localStorage.removeItem(REFRESH_TOKEN_KEY);
  },
};

/**
 * Memory-based token storage (for testing or SSR).
 */
export function createMemoryStorage(): TokenStorage {
  let refreshToken: string | null = null;

  return {
    async getRefreshToken() {
      return refreshToken;
    },

    async setRefreshToken(token: string) {
      refreshToken = token;
    },

    async clearRefreshToken() {
      refreshToken = null;
    },
  };
}
