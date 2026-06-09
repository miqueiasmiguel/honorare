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
      {
        path: 'catalog',
        loadChildren: () => import('./catalog/catalog.routes').then((m) => m.catalogRoutes),
      },
      {
        path: 'guias',
        loadChildren: () =>
          import('./faturamento/faturamento.routes').then((m) => m.faturamentoRoutes),
      },
      {
        path: 'recursos',
        loadChildren: () => import('./faturamento/faturamento.routes').then((m) => m.recursoRoutes),
      },
      {
        path: 'configuracoes',
        loadComponent: () =>
          import('./configuracoes/configuracoes-page').then((m) => m.ConfiguracoesPage),
      },
      { path: '', redirectTo: 'users', pathMatch: 'full' },
    ],
  },
];
