import { Routes } from '@angular/router';

export const recursoRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./recurso-list/recurso-list.component').then((m) => m.RecursoListComponent),
  },
  {
    path: 'novo',
    loadComponent: () =>
      import('./recurso-form/recurso-form.component').then((m) => m.RecursoFormComponent),
  },
  {
    path: ':id/guias',
    loadComponent: () =>
      import('./recurso-guias/recurso-guias.component').then((m) => m.RecursoGuiasComponent),
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./recurso-form/recurso-form.component').then((m) => m.RecursoFormComponent),
  },
];

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
    path: ':id/conciliar',
    loadComponent: () =>
      import('./conciliacao/conciliacao.component').then((m) => m.ConciliacaoComponent),
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./demonstrativo-form/demonstrativo-form.component').then(
        (m) => m.DemonstrativoFormComponent,
      ),
  },
];
