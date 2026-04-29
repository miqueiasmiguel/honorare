import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpBackend, HttpClient } from '@angular/common/http';
import { catchError, finalize, firstValueFrom, map, Observable, of, shareReplay, tap } from 'rxjs';

export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

const RefreshTokenKey = '_rt';

@Injectable({ providedIn: 'root' })
export class AuthService {
  // Uses HttpBackend directly to bypass the auth interceptor and avoid circular dependency.
  private readonly _http = new HttpClient(inject(HttpBackend));

  private readonly _accessToken = signal<string | null>(null);
  private readonly _expiresAt = signal<number | null>(null);
  private _refreshInFlight$: Observable<boolean> | null = null;

  readonly isAuthenticated = computed(() => {
    const token = this._accessToken();
    const exp = this._expiresAt();
    return token !== null && exp !== null && Date.now() < exp;
  });

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
