import { Routes } from '@angular/router';
import { AdminShell } from './admin-shell';
import { adminGuard } from './admin.guard';

export const adminRoutes: Routes = [
  {
    path: '',
    component: AdminShell,
    canActivate: [adminGuard],
    children: [
      {
        path: 'users',
        loadComponent: () => import('./users/user-list').then((m) => m.UserList),
      },
      {
        path: 'profile',
        loadComponent: () => import('./profile/profile-page').then((m) => m.ProfilePage),
      },
      { path: '', redirectTo: 'users', pathMatch: 'full' },
    ],
  },
];
