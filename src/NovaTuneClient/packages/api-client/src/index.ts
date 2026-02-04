// Re-export generated API clients and models
// After running `pnpm generate`, the generated files will be available here

// Placeholder exports - these will be replaced by generated code
export interface User {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
  createdAt: string;
}

export interface Track {
  id: string;
  title: string;
  artist: string;
  duration: number;
  coverUrl?: string;
  status: string;
  createdAt: string;
}

export interface Playlist {
  id: string;
  name: string;
  description?: string;
  trackCount: number;
  createdAt: string;
  updatedAt: string;
}

// API client instance will be configured after Orval generation
export { customInstance } from './custom-instance';
