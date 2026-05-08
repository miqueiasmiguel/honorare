import { Routes } from '@angular/router';

export const faturamentoRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./guia-list/guia-list.component').then((m) => m.GuiaListComponent),
  },
  {
    path: 'nova',
    loadComponent: () => import('./guia-form/guia-form.component').then((m) => m.GuiaFormComponent),
  },
  {
    path: ':id',
    loadComponent: () => import('./guia-form/guia-form.component').then((m) => m.GuiaFormComponent),
  },
];
