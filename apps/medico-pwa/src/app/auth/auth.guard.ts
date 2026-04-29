import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  const rt = localStorage.getItem('_rt');
  if (rt) {
    return auth
      .refresh()
      .pipe(map((refreshed) => refreshed || router.createUrlTree(['/auth/login'])));
  }

  return router.createUrlTree(['/auth/login']);
};
