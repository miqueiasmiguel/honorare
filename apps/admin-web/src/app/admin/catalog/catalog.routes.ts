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
  {
    path: 'prestadores',
    loadComponent: () =>
      import('./prestadores/prestador-list/prestador-list.component').then(
        (m) => m.PrestadorListComponent,
      ),
  },
  {
    path: 'prestadores/novo',
    loadComponent: () =>
      import('./prestadores/prestador-form/prestador-form.component').then(
        (m) => m.PrestadorFormComponent,
      ),
  },
  {
    path: 'prestadores/:id',
    loadComponent: () =>
      import('./prestadores/prestador-form/prestador-form.component').then(
        (m) => m.PrestadorFormComponent,
      ),
  },
  {
    path: 'tabelas',
    loadComponent: () =>
      import('./tabelas/tabela-list/tabela-list.component').then((m) => m.TabelaListComponent),
  },
  {
    path: 'beneficiarios',
    loadComponent: () =>
      import('./beneficiarios/beneficiario-autocomplete/beneficiario-autocomplete.component').then(
        (m) => m.BeneficiarioAutocompleteComponent,
      ),
  },
];
