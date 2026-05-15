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

export const demonstrativoRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./demonstrativo-list/demonstrativo-list.component').then(
        (m) => m.DemonstrativoListComponent,
      ),
  },
  {
    path: 'novo',
    loadComponent: () =>
      import('./demonstrativo-form/demonstrativo-form.component').then(
        (m) => m.DemonstrativoFormComponent,
      ),
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./demonstrativo-form/demonstrativo-form.component').then(
        (m) => m.DemonstrativoFormComponent,
      ),
  },
];
