import { Routes } from '@angular/router';

export const catalogRoutes: Routes = [
  {
    path: 'operadoras',
    loadComponent: () =>
      import('./operadoras/operadora-list/operadora-list.component').then(
        (m) => m.OperadoraListComponent,
      ),
  },
  {
    path: 'operadoras/nova',
    loadComponent: () =>
      import('./operadoras/operadora-form/operadora-form.component').then(
        (m) => m.OperadoraFormComponent,
      ),
  },
  {
    path: 'operadoras/:id',
    loadComponent: () =>
      import('./operadoras/operadora-form/operadora-form.component').then(
        (m) => m.OperadoraFormComponent,
      ),
  },
  {
    path: 'procedimentos',
    loadComponent: () =>
      import('./procedimentos/procedimento-list/procedimento-list.component').then(
        (m) => m.ProcedimentoListComponent,
      ),
  },
  {
    path: 'procedimentos/novo',
    loadComponent: () =>
      import('./procedimentos/procedimento-form/procedimento-form.component').then(
        (m) => m.ProcedimentoFormComponent,
      ),
  },
  {
    path: 'procedimentos/:id',
    loadComponent: () =>
      import('./procedimentos/procedimento-form/procedimento-form.component').then(
        (m) => m.ProcedimentoFormComponent,
      ),
  },
];
