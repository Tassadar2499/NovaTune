# Frontend Implementation Plan (Vue + TypeScript)

This plan addresses the requirements from `doc/implementation/frontend/task.md`:

- TypeScript + Vue + Vite SPA for web
- Main music player: Desktop (Electron) and Android app
- Admin and other services: simple SPA

---

## 0. Current Repository State (Constraints)

- **Backend:** ASP.NET Core minimal APIs in `src/NovaTuneApp/NovaTuneApp.ApiService` with OpenAPI at `/openapi/v1.json` and Scalar UI at `/scalar/v1`
- **Orchestration:** Aspire in `src/NovaTuneApp/NovaTuneApp.AppHost` (RavenDB, MinIO, Redpanda, Garnet)
- **Existing Web:** `src/NovaTuneApp/NovaTuneApp.Web` contains a Blazor/Razor Components starter UI; `doc/requirements/stack.md` specifies Vue + TypeScript

---

## 1. Key Decisions

### 1.1 Repository Structure: Monorepo with Multiple Apps

Use a small monorepo containing two SPAs plus shared packages:

| App/Package | Purpose |
|-------------|---------|
| `apps/player` | Listener experience: library, playback, playlists, upload |
| `apps/admin` | Admin dashboards, user management, track moderation |
| `packages/api-client` | OpenAPI-generated types and client |
| `packages/core` | Auth, HTTP wrapper, telemetry, device ID, error handling |
| `packages/ui` | Shared Vue components and design system |

### 1.2 SPA Hosting Strategy

| Environment | Approach |
|-------------|----------|
| **Development** | Vite dev server with API proxy to `NovaTuneApp.ApiService` |
| **Production** | Static hosting (nginx container or CDN); remove or repurpose `NovaTuneApp.Web` |

### 1.3 Authentication Storage

The API uses JWT access token + refresh token with refresh via `POST /auth/refresh`:

| Platform | Access Token | Refresh Token |
|----------|--------------|---------------|
| Web (MVP) | Memory (reactive state) | `localStorage` |
| Web (hardened) | Memory | HttpOnly cookie via BFF pattern |
| Electron | Memory | OS keychain (`keytar` or `keychain-access`) |
| Android | Memory | Capacitor Secure Storage plugin |

### 1.4 Device ID Strategy

Clients must generate and persist a per-installation device ID:

```typescript
// packages/core/src/device.ts
import { sha256 } from '@noble/hashes/sha256';
import { bytesToHex } from '@noble/hashes/utils';

const DEVICE_ID_KEY = 'novatune_device_id';

export function getOrCreateDeviceId(): string {
  let deviceId = localStorage.getItem(DEVICE_ID_KEY);
  if (!deviceId) {
    deviceId = crypto.randomUUID();
    localStorage.setItem(DEVICE_ID_KEY, deviceId);
  }
  return deviceId;
}

export function hashDeviceId(deviceId: string): string {
  // Hash before sending to telemetry (per stage-7-telemetry.md)
  return bytesToHex(sha256(new TextEncoder().encode(deviceId)));
}
```

**Headers:**
- `X-Device-Id`: Raw device ID for auth requests
- Telemetry payloads: Use `hashDeviceId()` before transmission

---

## 2. Repository Layout

```
src/NovaTuneClient/
├── package.json
├── pnpm-workspace.yaml
├── tsconfig.base.json
├── .env.example
├── apps/
│   ├── player/
│   │   ├── package.json
│   │   ├── vite.config.ts
│   │   ├── index.html
│   │   └── src/
│   │       ├── main.ts
│   │       ├── App.vue
│   │       ├── router/
│   │       │   └── index.ts
│   │       ├── stores/
│   │       │   ├── auth.ts
│   │       │   ├── player.ts
│   │       │   ├── library.ts
│   │       │   └── playlists.ts
│   │       ├── features/
│   │       │   ├── auth/
│   │       │   ├── library/
│   │       │   ├── player/
│   │       │   ├── playlists/
│   │       │   └── upload/
│   │       ├── layouts/
│   │       └── composables/
│   └── admin/
│       ├── package.json
│       ├── vite.config.ts
│       └── src/
│           ├── main.ts
│           ├── router/
│           ├── stores/
│           └── features/
│               ├── users/
│               ├── tracks/
│               ├── analytics/
│               └── audit/
└── packages/
    ├── api-client/
    │   ├── package.json
    │   ├── orval.config.ts
    │   └── src/
    │       └── generated/
    ├── core/
    │   ├── package.json
    │   └── src/
    │       ├── auth/
    │       ├── http/
    │       ├── telemetry/
    │       ├── device.ts
    │       └── errors.ts
    └── ui/
        ├── package.json
        └── src/
            ├── components/
            ├── composables/
            └── styles/
```

---

## 3. Recommended Dependencies

### Core Framework

| Package | Purpose | Version |
|---------|---------|---------|
| `vue` | UI framework | ^3.5 |
| `vue-router` | SPA routing | ^4.4 |
| `pinia` | State management | ^2.2 |
| `vite` | Build tool | ^6.0 |
| `typescript` | Type safety | ^5.6 |

### API & HTTP

| Package | Purpose |
|---------|---------|
| `orval` | OpenAPI client generation |
| `axios` | HTTP client (used by orval output) |
| `@tanstack/vue-query` | Server state management, caching, retries |

### UI Components

| Package | Purpose |
|---------|---------|
| `@headlessui/vue` | Accessible, unstyled primitives |
| `@vueuse/core` | Vue composables collection |
| `tailwindcss` | Utility-first CSS |

### Utilities

| Package | Purpose |
|---------|---------|
| `@noble/hashes` | Device ID hashing (SHA-256) |
| `date-fns` | Date formatting |
| `zod` | Runtime validation |

### Testing

| Package | Purpose |
|---------|---------|
| `vitest` | Unit testing |
| `@vue/test-utils` | Vue component testing |
| `@testing-library/vue` | User-centric component testing |
| `playwright` | E2E testing |
| `msw` | API mocking |

### Desktop & Mobile

| Package | Purpose |
|---------|---------|
| `electron` | Desktop shell |
| `electron-builder` | Desktop packaging |
| `@capacitor/core` | Mobile wrapper |
| `@capacitor/secure-storage` | Secure token storage (mobile) |

---

## 4. API Integration

### 4.1 OpenAPI Client Generation

Use **Orval** to generate a typed TypeScript client:

```typescript
// packages/api-client/orval.config.ts
import { defineConfig } from 'orval';

export default defineConfig({
  novatune: {
    input: {
      target: 'http://localhost:5000/openapi/v1.json',
    },
    output: {
      mode: 'tags-split',
      target: './src/generated',
      schemas: './src/generated/models',
      client: 'axios',
      override: {
        mutator: {
          path: '../http/axios-instance.ts',
          name: 'customInstance',
        },
      },
    },
  },
});
```

**Workflow:**
```bash
# Generate/update API client
pnpm --filter @novatune/api-client generate

# Commit generated code (or generate in CI)
```

### 4.2 HTTP Wrapper (`packages/core/src/http`)

Centralize:
- Base URL from `VITE_API_BASE_URL`
- `Authorization: Bearer <token>` header injection
- Refresh-on-401 flow with single-flight refresh
- RFC 7807 Problem Details parsing
- Request/response logging in development

```typescript
// packages/core/src/http/axios-instance.ts
import axios, { AxiosError, InternalAxiosRequestConfig } from 'axios';
import { useAuthStore } from '../auth/store';
import { ProblemDetails, parseApiError } from '../errors';

const instance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  timeout: 30_000,
});

let isRefreshing = false;
let refreshPromise: Promise<void> | null = null;

instance.interceptors.request.use((config) => {
  const auth = useAuthStore();
  if (auth.accessToken) {
    config.headers.Authorization = `Bearer ${auth.accessToken}`;
  }
  return config;
});

instance.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      if (!isRefreshing) {
        isRefreshing = true;
        const auth = useAuthStore();
        refreshPromise = auth.refreshTokens().finally(() => {
          isRefreshing = false;
          refreshPromise = null;
        });
      }

      await refreshPromise;
      return instance(originalRequest);
    }

    throw parseApiError(error);
  }
);

export const customInstance = <T>(config: AxiosRequestConfig): Promise<T> => {
  return instance(config).then(({ data }) => data);
};
```

### 4.3 Error Handling

```typescript
// packages/core/src/errors.ts
export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  extensions?: Record<string, unknown>;
}

export class ApiError extends Error {
  constructor(
    public readonly problem: ProblemDetails,
    public readonly originalError?: unknown
  ) {
    super(problem.title);
    this.name = 'ApiError';
  }

  get errorCode(): string {
    // Extract error code from type URL
    const match = this.problem.type.match(/\/errors\/(.+)$/);
    return match?.[1] ?? 'unknown';
  }
}

export function parseApiError(error: AxiosError): ApiError {
  if (error.response?.data && typeof error.response.data === 'object') {
    const data = error.response.data as Partial<ProblemDetails>;
    if (data.type && data.title && data.status) {
      return new ApiError(data as ProblemDetails, error);
    }
  }

  return new ApiError({
    type: 'https://novatune.dev/errors/network-error',
    title: 'Network Error',
    status: 0,
    detail: error.message,
  }, error);
}
```

### 4.4 CORS Configuration

Add CORS in `NovaTuneApp.ApiService` for development:

```csharp
// Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5173",  // player dev
            "http://localhost:5174"   // admin dev
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});
```

Also configure MinIO bucket CORS for cross-origin audio playback with Range headers (see `doc/implementation/stage-4-streaming.md`).

---

## 5. State Management (Pinia)

### 5.1 Auth Store

```typescript
// apps/player/src/stores/auth.ts
import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { authApi } from '@novatune/api-client';
import { getOrCreateDeviceId } from '@novatune/core';

export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string | null>(null);
  const refreshToken = ref<string | null>(localStorage.getItem('refresh_token'));
  const user = ref<User | null>(null);

  const isAuthenticated = computed(() => !!accessToken.value);
  const deviceId = getOrCreateDeviceId();

  async function login(email: string, password: string) {
    const response = await authApi.login({
      email,
      password,
      deviceId,
    });

    accessToken.value = response.accessToken;
    refreshToken.value = response.refreshToken;
    user.value = response.user;

    localStorage.setItem('refresh_token', response.refreshToken);
  }

  async function refreshTokens() {
    if (!refreshToken.value) {
      throw new Error('No refresh token');
    }

    const response = await authApi.refresh({
      refreshToken: refreshToken.value,
      deviceId,
    });

    accessToken.value = response.accessToken;
    refreshToken.value = response.refreshToken;
    localStorage.setItem('refresh_token', response.refreshToken);
  }

  async function logout() {
    try {
      await authApi.logout({ deviceId });
    } finally {
      accessToken.value = null;
      refreshToken.value = null;
      user.value = null;
      localStorage.removeItem('refresh_token');
    }
  }

  return {
    accessToken,
    user,
    isAuthenticated,
    deviceId,
    login,
    refreshTokens,
    logout,
  };
});
```

### 5.2 Player Store

```typescript
// apps/player/src/stores/player.ts
import { defineStore } from 'pinia';
import { ref, computed, watch } from 'vue';
import { streamApi, telemetryApi } from '@novatune/api-client';
import { hashDeviceId, getOrCreateDeviceId } from '@novatune/core';

export const usePlayerStore = defineStore('player', () => {
  const audio = ref<HTMLAudioElement | null>(null);
  const currentTrack = ref<Track | null>(null);
  const isPlaying = ref(false);
  const currentTime = ref(0);
  const duration = ref(0);
  const volume = ref(1);
  const queue = ref<Track[]>([]);

  const sessionId = ref(crypto.randomUUID());
  const hashedDeviceId = hashDeviceId(getOrCreateDeviceId());

  async function play(track: Track) {
    if (currentTrack.value?.id !== track.id) {
      currentTrack.value = track;

      // Get presigned streaming URL
      const { streamUrl } = await streamApi.getStreamUrl(track.id);

      if (!audio.value) {
        audio.value = new Audio();
        setupAudioListeners();
      }

      audio.value.src = streamUrl;
    }

    await audio.value?.play();
    isPlaying.value = true;

    // Report play_start telemetry
    await reportTelemetry('play_start');
  }

  async function pause() {
    audio.value?.pause();
    isPlaying.value = false;
    await reportTelemetry('play_stop');
  }

  async function reportTelemetry(eventType: string) {
    if (!currentTrack.value) return;

    await telemetryApi.ingestPlayback({
      eventType,
      trackId: currentTrack.value.id,
      clientTimestamp: new Date().toISOString(),
      positionSeconds: currentTime.value,
      sessionId: sessionId.value,
      deviceId: hashedDeviceId,
      clientVersion: import.meta.env.VITE_APP_VERSION,
    });
  }

  function setupAudioListeners() {
    if (!audio.value) return;

    audio.value.addEventListener('timeupdate', () => {
      currentTime.value = audio.value?.currentTime ?? 0;
    });

    audio.value.addEventListener('loadedmetadata', () => {
      duration.value = audio.value?.duration ?? 0;
    });

    audio.value.addEventListener('ended', async () => {
      await reportTelemetry('play_complete');
      playNext();
    });
  }

  function playNext() {
    const currentIndex = queue.value.findIndex(t => t.id === currentTrack.value?.id);
    if (currentIndex >= 0 && currentIndex < queue.value.length - 1) {
      play(queue.value[currentIndex + 1]);
    }
  }

  return {
    currentTrack,
    isPlaying,
    currentTime,
    duration,
    volume,
    queue,
    play,
    pause,
    playNext,
  };
});
```

---

## 6. Player App Scope (MVP)

### 6.1 Routes

```typescript
// apps/player/src/router/index.ts
import { createRouter, createWebHistory } from 'vue-router';
import { useAuthStore } from '@/stores/auth';

const routes = [
  {
    path: '/auth',
    component: () => import('@/layouts/AuthLayout.vue'),
    children: [
      { path: 'login', name: 'login', component: () => import('@/features/auth/LoginPage.vue') },
      { path: 'register', name: 'register', component: () => import('@/features/auth/RegisterPage.vue') },
    ],
  },
  {
    path: '/',
    component: () => import('@/layouts/MainLayout.vue'),
    meta: { requiresAuth: true },
    children: [
      { path: '', name: 'library', component: () => import('@/features/library/LibraryPage.vue') },
      { path: 'track/:id', name: 'track', component: () => import('@/features/library/TrackDetailPage.vue') },
      { path: 'playlists', name: 'playlists', component: () => import('@/features/playlists/PlaylistsPage.vue') },
      { path: 'playlist/:id', name: 'playlist', component: () => import('@/features/playlists/PlaylistDetailPage.vue') },
      { path: 'upload', name: 'upload', component: () => import('@/features/upload/UploadPage.vue') },
    ],
  },
];

const router = createRouter({
  history: createWebHistory(),
  routes,
});

router.beforeEach((to, from, next) => {
  const auth = useAuthStore();

  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    next({ name: 'login', query: { redirect: to.fullPath } });
  } else {
    next();
  }
});

export default router;
```

### 6.2 Feature Checklist

| Feature | API Endpoints | Priority |
|---------|---------------|----------|
| **Authentication** | `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout` | P0 |
| **Library** | `GET /tracks` (filters, search, pagination) | P0 |
| **Track Details** | `GET /tracks/{id}` | P0 |
| **Streaming** | `POST /tracks/{id}/stream` | P0 |
| **Playlists** | `GET /playlists`, `POST /playlists`, `PATCH /playlists/{id}`, `DELETE /playlists/{id}` | P1 |
| **Playlist Tracks** | `POST /playlists/{id}/tracks`, `DELETE /playlists/{id}/tracks/{trackId}`, `POST /playlists/{id}/reorder` | P1 |
| **Upload** | `POST /uploads/initiate`, `POST /uploads/{id}/complete` | P2 |
| **Telemetry** | `POST /telemetry/playback` | P1 |

---

## 7. Admin App Scope (MVP)

### 7.1 Routes

```typescript
// apps/admin/src/router/index.ts
const routes = [
  {
    path: '/auth',
    children: [
      { path: 'login', name: 'login', component: () => import('@/features/auth/AdminLoginPage.vue') },
    ],
  },
  {
    path: '/',
    component: () => import('@/layouts/AdminLayout.vue'),
    meta: { requiresAuth: true, requiresAdmin: true },
    children: [
      { path: '', name: 'dashboard', component: () => import('@/features/analytics/DashboardPage.vue') },
      { path: 'users', name: 'users', component: () => import('@/features/users/UsersListPage.vue') },
      { path: 'users/:id', name: 'user-detail', component: () => import('@/features/users/UserDetailPage.vue') },
      { path: 'tracks', name: 'tracks', component: () => import('@/features/tracks/TracksListPage.vue') },
      { path: 'tracks/:id', name: 'track-detail', component: () => import('@/features/tracks/TrackDetailPage.vue') },
      { path: 'analytics', name: 'analytics', component: () => import('@/features/analytics/AnalyticsPage.vue') },
      { path: 'audit', name: 'audit', component: () => import('@/features/audit/AuditLogPage.vue') },
    ],
  },
];
```

### 7.2 Feature Checklist

| Feature | API Endpoints | Priority |
|---------|---------------|----------|
| **Admin Auth** | Login with admin role check | P0 |
| **User Management** | `GET /admin/users`, `PATCH /admin/users/{id}/status` | P0 |
| **Track Moderation** | `GET /admin/tracks`, `PATCH /admin/tracks/{id}/status`, `DELETE /admin/tracks/{id}` | P0 |
| **Analytics Dashboard** | `GET /admin/analytics/overview`, `GET /admin/analytics/tracks/{id}` | P1 |
| **Audit Logs** | `GET /admin/audit-logs` | P1 |
| **Integrity Verification** | `POST /admin/audit-logs/verify` | P2 |

---

## 8. Testing Strategy

### 8.1 Unit Tests (Vitest)

```typescript
// packages/core/src/__tests__/device.test.ts
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { getOrCreateDeviceId, hashDeviceId } from '../device';

describe('device', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('creates a new device ID if none exists', () => {
    const id = getOrCreateDeviceId();
    expect(id).toMatch(/^[0-9a-f-]{36}$/);
  });

  it('returns existing device ID', () => {
    const id1 = getOrCreateDeviceId();
    const id2 = getOrCreateDeviceId();
    expect(id1).toBe(id2);
  });

  it('hashes device ID for telemetry', () => {
    const id = 'test-device-id';
    const hashed = hashDeviceId(id);
    expect(hashed).toHaveLength(64); // SHA-256 hex
    expect(hashed).not.toBe(id);
  });
});
```

### 8.2 Component Tests

```typescript
// apps/player/src/features/library/__tests__/TrackCard.test.ts
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/vue';
import { createTestingPinia } from '@pinia/testing';
import TrackCard from '../TrackCard.vue';

describe('TrackCard', () => {
  const mockTrack = {
    id: '01HXK...',
    title: 'Test Track',
    artist: 'Test Artist',
    duration: 180,
  };

  it('renders track information', () => {
    render(TrackCard, {
      props: { track: mockTrack },
      global: {
        plugins: [createTestingPinia()],
      },
    });

    expect(screen.getByText('Test Track')).toBeInTheDocument();
    expect(screen.getByText('Test Artist')).toBeInTheDocument();
    expect(screen.getByText('3:00')).toBeInTheDocument();
  });
});
```

### 8.3 E2E Tests (Playwright)

```typescript
// apps/player/e2e/auth.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Authentication', () => {
  test('user can log in and see library', async ({ page }) => {
    await page.goto('/auth/login');

    await page.fill('[data-testid="email"]', 'test@example.com');
    await page.fill('[data-testid="password"]', 'password123');
    await page.click('[data-testid="login-button"]');

    await expect(page).toHaveURL('/');
    await expect(page.getByRole('heading', { name: 'My Library' })).toBeVisible();
  });
});
```

### 8.4 API Mocking (MSW)

```typescript
// apps/player/src/mocks/handlers.ts
import { http, HttpResponse } from 'msw';

export const handlers = [
  http.post('/auth/login', async ({ request }) => {
    const body = await request.json();

    if (body.email === 'test@example.com') {
      return HttpResponse.json({
        accessToken: 'mock-access-token',
        refreshToken: 'mock-refresh-token',
        user: { id: '01HXK...', email: 'test@example.com' },
      });
    }

    return HttpResponse.json(
      { type: 'https://novatune.dev/errors/invalid-credentials', title: 'Invalid credentials', status: 401 },
      { status: 401 }
    );
  }),
];
```

---

## 9. Performance Optimization

### 9.1 Code Splitting

Routes are lazy-loaded by default (see router examples above). Additional strategies:

```typescript
// Lazy load heavy components
const WaveformVisualization = defineAsyncComponent(
  () => import('@/components/WaveformVisualization.vue')
);

// Prefetch on hover/focus
router.beforeResolve(async (to) => {
  const matchedComponents = to.matched.flatMap(record =>
    Object.values(record.components ?? {})
  );
  // Vue Router handles prefetching automatically
});
```

### 9.2 Virtual Scrolling

For large track lists:

```vue
<script setup lang="ts">
import { useVirtualList } from '@vueuse/core';

const { list, containerProps, wrapperProps } = useVirtualList(tracks, {
  itemHeight: 64,
});
</script>

<template>
  <div v-bind="containerProps" class="h-[600px] overflow-auto">
    <div v-bind="wrapperProps">
      <TrackRow v-for="{ data, index } in list" :key="data.id" :track="data" />
    </div>
  </div>
</template>
```

### 9.3 Server State Caching (TanStack Query)

```typescript
// apps/player/src/features/library/composables/useTracks.ts
import { useQuery, useInfiniteQuery } from '@tanstack/vue-query';
import { tracksApi } from '@novatune/api-client';

export function useTracks(filters: Ref<TrackFilters>) {
  return useInfiniteQuery({
    queryKey: ['tracks', filters],
    queryFn: ({ pageParam }) => tracksApi.listTracks({
      ...filters.value,
      cursor: pageParam,
    }),
    getNextPageParam: (lastPage) => lastPage.nextCursor,
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

export function useTrack(trackId: Ref<string>) {
  return useQuery({
    queryKey: ['track', trackId],
    queryFn: () => tracksApi.getTrack(trackId.value),
    staleTime: 10 * 60 * 1000, // 10 minutes
  });
}
```

---

## 10. Security Hardening

### 10.1 Content Security Policy

```typescript
// vite.config.ts (development)
export default defineConfig({
  server: {
    headers: {
      'Content-Security-Policy': [
        "default-src 'self'",
        "script-src 'self'",
        "style-src 'self' 'unsafe-inline'",
        "img-src 'self' data: blob:",
        "media-src 'self' blob: https://*.minio.local",
        "connect-src 'self' http://localhost:* ws://localhost:*",
      ].join('; '),
    },
  },
});
```

### 10.2 Input Sanitization

```typescript
// packages/core/src/validation.ts
import { z } from 'zod';
import DOMPurify from 'dompurify';

export const sanitizeHtml = (dirty: string): string =>
  DOMPurify.sanitize(dirty, { ALLOWED_TAGS: [] });

export const trackSearchSchema = z.object({
  query: z.string().max(200).transform(sanitizeHtml),
  genre: z.string().max(50).optional(),
  sortBy: z.enum(['createdAt', 'title', 'artist']).default('createdAt'),
});
```

### 10.3 XSS Prevention

- Use Vue's built-in template escaping (avoid `v-html` where possible)
- Sanitize any user-generated content before rendering
- Use CSP headers
- Validate all inputs with Zod schemas

---

## 11. Accessibility (a11y)

### 11.1 Guidelines

- Use semantic HTML (`<main>`, `<nav>`, `<article>`, etc.)
- Ensure all interactive elements are keyboard accessible
- Provide ARIA labels for icons and non-text elements
- Support screen readers with proper heading hierarchy
- Maintain 4.5:1 color contrast ratio (WCAG AA)

### 11.2 Player Controls

```vue
<template>
  <div role="region" aria-label="Audio player">
    <button
      @click="togglePlay"
      :aria-label="isPlaying ? 'Pause' : 'Play'"
      :aria-pressed="isPlaying"
    >
      <PlayIcon v-if="!isPlaying" aria-hidden="true" />
      <PauseIcon v-else aria-hidden="true" />
    </button>

    <input
      type="range"
      :value="currentTime"
      :max="duration"
      @input="seek"
      aria-label="Seek position"
      :aria-valuetext="`${formatTime(currentTime)} of ${formatTime(duration)}`"
    />

    <span aria-live="polite" class="sr-only">
      Now playing: {{ currentTrack?.title }} by {{ currentTrack?.artist }}
    </span>
  </div>
</template>
```

---

## 12. Desktop (Electron) Packaging

### 12.1 Project Structure

```
apps/player-electron/
├── package.json
├── electron-builder.yml
├── src/
│   ├── main/
│   │   ├── index.ts
│   │   └── preload.ts
│   └── renderer/
│       └── (symlink to apps/player/dist)
```

### 12.2 Security Configuration

```typescript
// apps/player-electron/src/main/index.ts
import { app, BrowserWindow, ipcMain } from 'electron';
import { join } from 'path';

const createWindow = () => {
  const win = new BrowserWindow({
    width: 1200,
    height: 800,
    webPreferences: {
      preload: join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  });

  // Load built Vue app
  if (process.env.NODE_ENV === 'development') {
    win.loadURL('http://localhost:5173');
  } else {
    win.loadFile(join(__dirname, '../renderer/index.html'));
  }
};

app.whenReady().then(createWindow);
```

### 12.3 Secure Token Storage

```typescript
// apps/player-electron/src/main/preload.ts
import { contextBridge, ipcRenderer } from 'electron';

contextBridge.exposeInMainWorld('electronAPI', {
  getSecureToken: () => ipcRenderer.invoke('get-secure-token'),
  setSecureToken: (token: string) => ipcRenderer.invoke('set-secure-token', token),
  deleteSecureToken: () => ipcRenderer.invoke('delete-secure-token'),
});

// Main process handler using keytar
import keytar from 'keytar';

ipcMain.handle('get-secure-token', async () => {
  return keytar.getPassword('novatune', 'refresh_token');
});

ipcMain.handle('set-secure-token', async (_, token: string) => {
  await keytar.setPassword('novatune', 'refresh_token', token);
});
```

---

## 13. Android (Capacitor) Packaging

### 13.1 Setup

```bash
# In apps/player
pnpm add @capacitor/core @capacitor/cli @capacitor/android
npx cap init NovaTune dev.novatune.player --web-dir=dist
npx cap add android
```

### 13.2 Configuration

```typescript
// apps/player/capacitor.config.ts
import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'dev.novatune.player',
  appName: 'NovaTune',
  webDir: 'dist',
  server: {
    androidScheme: 'https',
  },
  plugins: {
    SecureStoragePlugin: {
      // For refresh token storage
    },
  },
};

export default config;
```

### 13.3 Secure Storage

```typescript
// packages/core/src/auth/storage-capacitor.ts
import { SecureStoragePlugin } from 'capacitor-secure-storage-plugin';

export const capacitorStorage = {
  async getRefreshToken(): Promise<string | null> {
    try {
      const { value } = await SecureStoragePlugin.get({ key: 'refresh_token' });
      return value;
    } catch {
      return null;
    }
  },

  async setRefreshToken(token: string): Promise<void> {
    await SecureStoragePlugin.set({ key: 'refresh_token', value: token });
  },

  async clearRefreshToken(): Promise<void> {
    await SecureStoragePlugin.remove({ key: 'refresh_token' });
  },
};
```

---

## 14. Local Development Workflow

### 14.1 Prerequisites

- Node.js 20+
- pnpm 9+
- .NET 9 SDK (for backend)

### 14.2 Commands

```bash
# 1. Start backend (Aspire)
dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost

# 2. Install frontend dependencies
pnpm -C src/NovaTuneClient install

# 3. Generate API client
pnpm -C src/NovaTuneClient --filter @novatune/api-client generate

# 4. Start player dev server
pnpm -C src/NovaTuneClient --filter player dev

# 5. Start admin dev server (parallel terminal)
pnpm -C src/NovaTuneClient --filter admin dev

# Run tests
pnpm -C src/NovaTuneClient test
pnpm -C src/NovaTuneClient test:e2e
```

### 14.3 Environment Variables

```bash
# apps/player/.env.local
VITE_API_BASE_URL=http://localhost:5000
VITE_APP_VERSION=1.0.0-dev

# apps/admin/.env.local
VITE_API_BASE_URL=http://localhost:5000
VITE_APP_VERSION=1.0.0-dev
```

---

## 15. CI/CD Pipeline

### 15.1 GitHub Actions

```yaml
# .github/workflows/frontend.yml
name: Frontend CI

on:
  push:
    paths:
      - 'src/NovaTuneClient/**'
  pull_request:
    paths:
      - 'src/NovaTuneClient/**'

jobs:
  lint-and-test:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: src/NovaTuneClient
    steps:
      - uses: actions/checkout@v4
      - uses: pnpm/action-setup@v3
        with:
          version: 9
      - uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: pnpm
          cache-dependency-path: src/NovaTuneClient/pnpm-lock.yaml

      - run: pnpm install --frozen-lockfile
      - run: pnpm lint
      - run: pnpm typecheck
      - run: pnpm test

  build:
    runs-on: ubuntu-latest
    needs: lint-and-test
    defaults:
      run:
        working-directory: src/NovaTuneClient
    steps:
      - uses: actions/checkout@v4
      - uses: pnpm/action-setup@v3
        with:
          version: 9
      - uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: pnpm
          cache-dependency-path: src/NovaTuneClient/pnpm-lock.yaml

      - run: pnpm install --frozen-lockfile
      - run: pnpm --filter player build
      - run: pnpm --filter admin build

      - uses: actions/upload-artifact@v4
        with:
          name: player-dist
          path: src/NovaTuneClient/apps/player/dist

      - uses: actions/upload-artifact@v4
        with:
          name: admin-dist
          path: src/NovaTuneClient/apps/admin/dist
```

---

## 16. Work Breakdown (Recommended Order)

### Phase 1: Foundation

1. Create `src/NovaTuneClient` workspace scaffold with pnpm workspaces
2. Set up `packages/core` with auth, HTTP wrapper, error handling, device ID
3. Configure Orval and generate `packages/api-client` from OpenAPI spec
4. Add CORS configuration to `NovaTuneApp.ApiService`

### Phase 2: Player MVP

5. Build `apps/player` scaffold with Vue Router and Pinia stores
6. Implement auth flows (register, login, refresh, logout)
7. Build library view with track listing and pagination
8. Implement audio player with streaming integration
9. Add telemetry reporting (play events)

### Phase 3: Playlists

10. Implement playlist CRUD operations
11. Add track management within playlists
12. Implement drag-and-drop reordering

### Phase 4: Admin MVP

13. Build `apps/admin` scaffold
14. Implement user management views
15. Implement track moderation views
16. Build analytics dashboard
17. Add audit log viewer

### Phase 5: Platform Packaging

18. Add Electron wrapper for desktop
19. Add Capacitor for Android
20. Implement platform-specific secure storage

### Phase 6: Polish

21. Add MinIO CORS configuration for range requests
22. Implement error boundaries and loading states
23. Add comprehensive E2E tests
24. Performance optimization (code splitting, virtual scrolling)

---

## 17. Claude Skills

The following skills are available to assist with frontend implementation:

| Skill | Description | Location |
|-------|-------------|----------|
| **setup-vue-workspace** | Set up Vue+Vite+TypeScript monorepo with pnpm | `.claude/skills/setup-vue-workspace/SKILL.md` |
| **generate-api-client** | Generate TypeScript API client from OpenAPI using Orval | `.claude/skills/generate-api-client/SKILL.md` |
| **implement-player-app** | Build player SPA with auth, library, playback, playlists | `.claude/skills/implement-player-app/SKILL.md` |
| **implement-admin-app** | Build admin SPA with user management, moderation, analytics | `.claude/skills/implement-admin-app/SKILL.md` |
| **add-electron-wrapper** | Add Electron wrapper for desktop app | `.claude/skills/add-electron-wrapper/SKILL.md` |
| **add-capacitor-android** | Add Capacitor wrapper for Android app | `.claude/skills/add-capacitor-android/SKILL.md` |

---

## 18. Claude Agents

The following agents are available for frontend development tasks:

| Agent | Description | Location |
|-------|-------------|----------|
| **frontend-planner** | Plan frontend implementation with architecture decisions | `.claude/agents/frontend-planner.md` |
| **vue-app-implementer** | Implement Vue components, stores, and routing | `.claude/agents/vue-app-implementer.md` |
| **frontend-tester** | Write and run tests (Vitest, Testing Library, Playwright) | `.claude/agents/frontend-tester.md` |

### Agent Usage

**Planning Phase:**
```
Use the frontend-planner agent to analyze requirements and create detailed implementation plans.
```

**Implementation Phase:**
```
Use the vue-app-implementer agent to build components, stores, and features.
```

**Testing Phase:**
```
Use the frontend-tester agent to write unit, component, and E2E tests.
```

---

## 19. Open Items

- [ ] Finalize design system and component library scope
- [ ] Determine if i18n support is needed for MVP
- [ ] Evaluate PWA/offline support requirements
- [ ] Decide on waveform visualization library
- [ ] Plan iOS (Capacitor) support timeline
- [ ] Determine upload progress UI requirements
- [ ] Evaluate real-time features (WebSocket for live updates)
