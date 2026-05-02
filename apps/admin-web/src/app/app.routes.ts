import { Routes } from '@angular/router';
import { authGuard } from './auth/auth.guard';
import { homeRedirectGuard } from './auth/home-redirect.guard';

export const routes: Routes = [
  {
    path: 'auth/login',
    loadComponent: () => import('./auth/login/login').then((m) => m.Login),
  },
  {
    path: 'auth/callback',
    loadComponent: () => import('./auth/callback/callback').then((m) => m.Callback),
  },
  {
    path: 'saas',
    canActivate: [authGuard],
    loadChildren: () => import('./saas/saas.routes').then((m) => m.saasRoutes),
  },
  {
    path: 'admin',
    canActivate: [authGuard],
    loadChildren: () => import('./admin/admin.routes').then((m) => m.adminRoutes),
  },
  {
    path: '',
    canActivate: [authGuard, homeRedirectGuard],
    children: [],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
