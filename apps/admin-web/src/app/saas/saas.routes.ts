import { Routes } from '@angular/router';
import { SaasShell } from './saas-shell';
import { saasGuard } from './saas.guard';

export const saasRoutes: Routes = [
  {
    path: '',
    component: SaasShell,
    canActivate: [saasGuard],
    children: [
      {
        path: 'tenants',
        loadComponent: () => import('./tenants/tenant-list').then((m) => m.TenantList),
      },
      {
        path: 'tenants/:id',
        loadComponent: () => import('./tenants/tenant-detail').then((m) => m.TenantDetail),
      },
      { path: '', redirectTo: 'tenants', pathMatch: 'full' },
    ],
  },
];
