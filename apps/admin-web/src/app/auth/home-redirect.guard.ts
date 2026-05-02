import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const homeRedirectGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const role = auth.role();
  if (role === 'SaasAdmin') return router.createUrlTree(['/saas/tenants']);
  if (role === 'TenantAdmin') return router.createUrlTree(['/admin/users']);
  return router.createUrlTree(['/auth/login']);
};
