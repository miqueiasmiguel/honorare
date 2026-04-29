import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  // Auth endpoints are anonymous or self-authenticating — skip adding Bearer token.
  // This also prevents circular dependency with the refresh call.
  if (req.url.includes('/api/v1/auth/')) {
    return next(req);
  }

  const auth = inject(AuthService);
  const router = inject(Router);
  const token = auth.getAccessToken();

  const authedReq = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(authedReq).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && err.status === 401) {
        return auth.refresh().pipe(
          switchMap((refreshed) => {
            if (!refreshed) {
              void router.navigate(['/auth/login']);
              return throwError(() => err);
            }
            const newToken = auth.getAccessToken();
            const retryReq = newToken
              ? req.clone({ setHeaders: { Authorization: `Bearer ${newToken}` } })
              : req;
            return next(retryReq);
          }),
        );
      }
      return throwError(() => err);
    }),
  );
};
