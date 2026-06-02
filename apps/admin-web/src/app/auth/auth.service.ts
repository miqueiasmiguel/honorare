import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpBackend, HttpClient } from '@angular/common/http';
import {
  catchError,
  finalize,
  firstValueFrom,
  map,
  Observable,
  of,
  shareReplay,
  switchMap,
  tap,
} from 'rxjs';

export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

const RefreshTokenKey = '_rt';
const SaasRefreshKey = '_rt_saas';
const ImpNameKey = '_imp_name';

@Injectable({ providedIn: 'root' })
export class AuthService {
  // Uses HttpBackend directly to bypass the auth interceptor and avoid circular dependency.
  private readonly _http = new HttpClient(inject(HttpBackend));

  private readonly _accessToken = signal<string | null>(null);
  private readonly _expiresAt = signal<number | null>(null);
  private readonly _actingTenantName = signal<string | null>(localStorage.getItem(ImpNameKey));
  private _refreshInFlight$: Observable<boolean> | null = null;

  readonly isAuthenticated = computed(() => {
    const token = this._accessToken();
    const exp = this._expiresAt();
    return token !== null && exp !== null && Date.now() < exp;
  });

  readonly role = computed((): string | null => {
    const token = this._accessToken();
    if (!token) return null;
    try {
      // JWT uses Base64URL (- and _ instead of + and /); atob requires standard Base64.
      const segment = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      const padded = segment + '='.repeat((4 - (segment.length % 4)) % 4);
      const payload = JSON.parse(atob(padded)) as Record<string, unknown>;
      return (payload['role'] as string | undefined) ?? null;
    } catch {
      return null;
    }
  });

  readonly isImpersonating = computed((): boolean => {
    const token = this._accessToken();
    if (!token) return false;
    try {
      const segment = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      const padded = segment + '='.repeat((4 - (segment.length % 4)) % 4);
      const payload = JSON.parse(atob(padded)) as Record<string, unknown>;
      return payload['role'] === 'SaasAdmin' && payload['tenant_id'] != null;
    } catch {
      return false;
    }
  });

  readonly actingTenantName = computed(() => this._actingTenantName());

  getAccessToken(): string | null {
    return this._accessToken();
  }

  storeTokens(tokens: TokenResponse): void {
    this._accessToken.set(tokens.accessToken);
    this._expiresAt.set(Date.now() + tokens.expiresIn * 1000);
    localStorage.setItem(RefreshTokenKey, tokens.refreshToken);
  }

  logout(): void {
    this._clearTokens();
    // Best-effort — fire and forget; errors are intentionally ignored.
    this._http.post('/api/v1/auth/logout', {}).subscribe({ error: () => undefined });
  }

  refresh(): Observable<boolean> {
    if (this._refreshInFlight$) {
      return this._refreshInFlight$;
    }

    this._refreshInFlight$ = this._doRefresh().pipe(
      shareReplay(1),
      finalize(() => {
        this._refreshInFlight$ = null;
      }),
    );
    return this._refreshInFlight$;
  }

  initSession(): Promise<void> {
    const rt = localStorage.getItem(RefreshTokenKey);
    if (!rt) {
      return Promise.resolve();
    }
    return firstValueFrom(this.refresh().pipe(map(() => undefined)));
  }

  enterImpersonation(tenantId: string, tenantName: string): Observable<boolean> {
    const currentToken = this._accessToken();
    return this._http
      .post<TokenResponse>(
        `/api/v1/saas/tenants/${tenantId}/impersonate`,
        {},
        {
          headers: currentToken ? { Authorization: `Bearer ${currentToken}` } : {},
        },
      )
      .pipe(
        tap((resp) => {
          const currentRt = localStorage.getItem(RefreshTokenKey);
          if (currentRt) {
            localStorage.setItem(SaasRefreshKey, currentRt);
          }
          this.storeTokens(resp);
          localStorage.setItem(ImpNameKey, tenantName);
          this._actingTenantName.set(tenantName);
        }),
        map(() => true),
        catchError(() => of(false)),
      );
  }

  exitImpersonation(): Observable<boolean> {
    const currentToken = this._accessToken();
    return this._http
      .post(
        '/api/v1/saas/impersonation/exit',
        {},
        {
          headers: currentToken ? { Authorization: `Bearer ${currentToken}` } : {},
        },
      )
      .pipe(
        catchError(() => of(null)),
        switchMap(() => {
          const saasRt = localStorage.getItem(SaasRefreshKey);
          if (saasRt) {
            localStorage.setItem(RefreshTokenKey, saasRt);
          } else {
            localStorage.removeItem(RefreshTokenKey);
          }
          localStorage.removeItem(SaasRefreshKey);
          localStorage.removeItem(ImpNameKey);
          this._actingTenantName.set(null);
          return this.refresh();
        }),
      );
  }

  private _doRefresh(): Observable<boolean> {
    const rt = localStorage.getItem(RefreshTokenKey);
    if (!rt) {
      return of(false);
    }

    return this._http.post<TokenResponse>('/api/v1/auth/refresh', { refreshToken: rt }).pipe(
      tap((tokens) => {
        this.storeTokens(tokens);
      }),
      map(() => true),
      catchError(() => {
        this._clearTokens();
        return of(false);
      }),
    );
  }

  private _clearTokens(): void {
    this._accessToken.set(null);
    this._expiresAt.set(null);
    localStorage.removeItem(RefreshTokenKey);
  }
}
