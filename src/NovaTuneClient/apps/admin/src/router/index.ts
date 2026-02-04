import { createRouter, createWebHistory } from 'vue-router';
import type { RouteRecordRaw } from 'vue-router';
import { useAuthStore } from '@/stores/auth';

const routes: RouteRecordRaw[] = [
  {
    path: '/auth',
    children: [
      {
        path: 'login',
        name: 'login',
        component: () => import('@/features/auth/AdminLoginPage.vue'),
      },
    ],
  },
  {
    path: '/',
    component: () => import('@/layouts/AdminLayout.vue'),
    meta: { requiresAuth: true, requiresAdmin: true },
    children: [
      {
        path: '',
        name: 'dashboard',
        component: () => import('@/features/analytics/DashboardPage.vue'),
      },
      {
        path: 'users',
        name: 'users',
        component: () => import('@/features/users/UsersListPage.vue'),
      },
      {
        path: 'users/:id',
        name: 'user-detail',
        component: () => import('@/features/users/UserDetailPage.vue'),
      },
      {
        path: 'tracks',
        name: 'tracks',
        component: () => import('@/features/tracks/TracksListPage.vue'),
      },
      {
        path: 'tracks/:id',
        name: 'track-detail',
        component: () => import('@/features/tracks/TrackDetailPage.vue'),
      },
      {
        path: 'analytics',
        name: 'analytics',
        component: () => import('@/features/analytics/AnalyticsPage.vue'),
      },
      {
        path: 'audit',
        name: 'audit',
        component: () => import('@/features/audit/AuditLogPage.vue'),
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
  } else if (to.meta.requiresAdmin && !auth.isAdmin) {
    next({ name: 'login' });
  } else {
    next();
  }
});

export default router;
