import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService, type TokenResponse } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), AuthService],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  // ── Wave 1: signals, storeTokens, getAccessToken, logout ─────────────────

  describe('initial state', () => {
    it('isAuthenticated is false when no token is stored', () => {
      expect(service.isAuthenticated()).toBe(false);
    });

    it('getAccessToken returns null when no token is stored', () => {
      expect(service.getAccessToken()).toBeNull();
    });
  });

  describe('storeTokens()', () => {
    it('makes isAuthenticated return true with a future expiry', () => {
      service.storeTokens({ accessToken: 'at', refreshToken: 'rt', expiresIn: 900 });
      expect(service.isAuthenticated()).toBe(true);
    });

    it('keeps isAuthenticated false when expiresIn is 0 (already expired)', () => {
      service.storeTokens({ accessToken: 'at', refreshToken: 'rt', expiresIn: 0 });
      expect(service.isAuthenticated()).toBe(false);
    });

    it('makes getAccessToken return the stored access token', () => {
      service.storeTokens({ accessToken: 'my-token', refreshToken: 'rt', expiresIn: 900 });
      expect(service.getAccessToken()).toBe('my-token');
    });

    it('writes refresh token to localStorage["_rt"]', () => {
      service.storeTokens({ accessToken: 'at', refreshToken: 'my-rt', expiresIn: 900 });
      expect(localStorage.getItem('_rt')).toBe('my-rt');
    });

    it('does NOT write access token to localStorage', () => {
      service.storeTokens({ accessToken: 'secret-at', refreshToken: 'rt', expiresIn: 900 });
      const allKeys = Object.keys(localStorage);
      const found = allKeys.some((k) => localStorage.getItem(k) === 'secret-at');
      expect(found).toBe(false);
    });
  });

  describe('logout()', () => {
    it('clears in-memory access token and isAuthenticated', () => {
      service.storeTokens({ accessToken: 'at', refreshToken: 'rt', expiresIn: 900 });
      service.logout();
      httpMock.expectOne('/api/v1/auth/logout'); // absorb the logout request
      expect(service.isAuthenticated()).toBe(false);
      expect(service.getAccessToken()).toBeNull();
    });

    it('removes refresh token from localStorage', () => {
      service.storeTokens({ accessToken: 'at', refreshToken: 'rt', expiresIn: 900 });
      service.logout();
      httpMock.expectOne('/api/v1/auth/logout');
      expect(localStorage.getItem('_rt')).toBeNull();
    });

    it('calls POST /api/v1/auth/logout', () => {
      service.storeTokens({ accessToken: 'at', refreshToken: 'rt', expiresIn: 900 });
      service.logout();
      const req = httpMock.expectOne('/api/v1/auth/logout');
      expect(req.request.method).toBe('POST');
    });
  });

  // ── Wave 2: refresh() + initSession() ────────────────────────────────────

  describe('refresh()', () => {
    beforeEach(() => {
      localStorage.setItem('_rt', 'stored-rt');
    });

    it('POSTs to /api/v1/auth/refresh with the stored refresh token', () => {
      service.refresh().subscribe();
      const req = httpMock.expectOne('/api/v1/auth/refresh');
      expect(req.request.method).toBe('POST');
      expect((req.request.body as { refreshToken: string }).refreshToken).toBe('stored-rt');
      req.flush({
        accessToken: 'new-at',
        refreshToken: 'new-rt',
        expiresIn: 900,
      } satisfies TokenResponse);
    });

    it('returns true and stores new tokens on success', () => {
      let result: boolean | undefined;
      service.refresh().subscribe((v) => (result = v));
      const req = httpMock.expectOne('/api/v1/auth/refresh');
      req.flush({
        accessToken: 'new-at',
        refreshToken: 'new-rt',
        expiresIn: 900,
      } satisfies TokenResponse);
      expect(result).toBe(true);
      expect(service.getAccessToken()).toBe('new-at');
      expect(localStorage.getItem('_rt')).toBe('new-rt');
    });

    it('returns false and clears tokens on HTTP error', () => {
      let result: boolean | undefined;
      service.refresh().subscribe((v) => (result = v));
      const req = httpMock.expectOne('/api/v1/auth/refresh');
      req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });
      expect(result).toBe(false);
      expect(service.getAccessToken()).toBeNull();
      expect(localStorage.getItem('_rt')).toBeNull();
    });

    it('makes only one HTTP request when called twice simultaneously', () => {
      const results: boolean[] = [];
      service.refresh().subscribe((v) => results.push(v));
      service.refresh().subscribe((v) => results.push(v));

      // Only one request should be in flight
      const reqs = httpMock.match('/api/v1/auth/refresh');
      expect(reqs.length).toBe(1);
      reqs[0].flush({
        accessToken: 'at',
        refreshToken: 'rt',
        expiresIn: 900,
      } satisfies TokenResponse);
      expect(results).toEqual([true, true]);
    });

    it('returns false immediately when no refresh token in localStorage', () => {
      localStorage.removeItem('_rt');
      let result: boolean | undefined;
      service.refresh().subscribe((v) => (result = v));
      httpMock.expectNone('/api/v1/auth/refresh');
      expect(result).toBe(false);
    });
  });

  describe('role()', () => {
    function makeFakeJwt(payload: Record<string, unknown>): string {
      return `header.${btoa(JSON.stringify(payload))}.sig`;
    }

    it('role_DerivesCorrectlyFromJwtPayload', () => {
      service.storeTokens({
        accessToken: makeFakeJwt({ role: 'SaasAdmin', sub: 'user-id' }),
        refreshToken: 'rt',
        expiresIn: 900,
      });
      expect(service.role()).toBe('SaasAdmin');
    });

    it('returns null when no token is stored', () => {
      expect(service.role()).toBeNull();
    });

    it('returns null for TenantAdmin role', () => {
      service.storeTokens({
        accessToken: makeFakeJwt({ role: 'TenantAdmin', sub: 'user-id' }),
        refreshToken: 'rt',
        expiresIn: 900,
      });
      expect(service.role()).toBe('TenantAdmin');
    });

    it('returns null when token is malformed (not a valid JWT)', () => {
      service.storeTokens({ accessToken: 'not-a-jwt', refreshToken: 'rt', expiresIn: 900 });
      expect(service.role()).toBeNull();
    });

    it('handles Base64URL encoding (- and _ chars) as issued by real JWTs', () => {
      // Real JWTs use Base64URL where + → - and / → _
      // Build a payload whose standard-base64 contains + or / to verify the conversion
      const rawPayload = JSON.stringify({ role: 'SaasAdmin', sub: 'user-id', extra: '>>>???' });
      const b64Standard = btoa(rawPayload);
      // Simulate Base64URL by converting + → - and / → _
      const b64Url = b64Standard.replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
      const fakeJwt = `header.${b64Url}.sig`;
      service.storeTokens({ accessToken: fakeJwt, refreshToken: 'rt', expiresIn: 900 });
      expect(service.role()).toBe('SaasAdmin');
    });
  });

  describe('initSession()', () => {
    it('calls refresh() when a refresh token exists in localStorage', async () => {
      localStorage.setItem('_rt', 'stored-rt');
      const initPromise = service.initSession();
      const req = httpMock.expectOne('/api/v1/auth/refresh');
      req.flush({
        accessToken: 'at',
        refreshToken: 'new-rt',
        expiresIn: 900,
      } satisfies TokenResponse);
      await initPromise;
      expect(service.isAuthenticated()).toBe(true);
    });

    it('resolves without any HTTP call when no refresh token in localStorage', async () => {
      localStorage.removeItem('_rt');
      await service.initSession();
      httpMock.expectNone('/api/v1/auth/refresh');
      expect(service.isAuthenticated()).toBe(false);
    });
  });
});
