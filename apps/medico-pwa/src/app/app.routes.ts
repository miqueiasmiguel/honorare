import { Routes } from '@angular/router';
import { authGuard } from './auth/auth.guard';

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
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./painel/painel').then((m) => m.Painel),
    children: [
      {
        path: 'guias',
        loadComponent: () => import('./guias/guia-list/guia-list').then((m) => m.GuiaListComponent),
      },
      {
        path: 'guias/:id',
        loadComponent: () =>
          import('./guias/guia-detalhe/guia-detalhe').then((m) => m.GuiaDetalheComponent),
      },
      {
        path: 'perfil',
        loadComponent: () => import('./perfil/perfil').then((m) => m.Perfil),
      },
      {
        path: '',
        redirectTo: 'guias',
        pathMatch: 'full',
      },
    ],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
