import { createRouter, createWebHistory } from 'vue-router';
import type { RouteRecordRaw } from 'vue-router';
import { useAuthStore } from '@/stores/auth';

const routes: RouteRecordRaw[] = [
  {
    path: '/auth',
    component: () => import('@/layouts/AuthLayout.vue'),
    children: [
      {
        path: 'login',
        name: 'login',
        component: () => import('@/features/auth/LoginPage.vue'),
      },
      {
        path: 'register',
        name: 'register',
        component: () => import('@/features/auth/RegisterPage.vue'),
      },
    ],
  },
  {
    path: '/',
    component: () => import('@/layouts/MainLayout.vue'),
    meta: { requiresAuth: true },
    children: [
      {
        path: '',
        name: 'library',
        component: () => import('@/features/library/LibraryPage.vue'),
      },
      {
        path: 'track/:id',
        name: 'track',
        component: () => import('@/features/library/TrackDetailPage.vue'),
      },
      {
        path: 'playlists',
        name: 'playlists',
        component: () => import('@/features/playlists/PlaylistsPage.vue'),
      },
      {
        path: 'playlist/:id',
        name: 'playlist',
        component: () => import('@/features/playlists/PlaylistDetailPage.vue'),
      },
      {
        path: 'upload',
        name: 'upload',
        component: () => import('@/features/upload/UploadPage.vue'),
      },
    ],
  },
];

const router = createRouter({
  history: createWebHistory(),
  routes,
});

router.beforeEach((to, _from, next) => {
  const auth = useAuthStore();

  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    next({ name: 'login', query: { redirect: to.fullPath } });
  } else {
    next();
  }
});

export default router;
