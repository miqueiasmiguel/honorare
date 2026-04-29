import { TestBed } from '@angular/core/testing';
import {
  HttpClient,
  HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let router: Router;
  let authService: AuthService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        AuthService,
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    authService = TestBed.inject(AuthService);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('adds Authorization: Bearer header to /api/data when token is present', () => {
    authService.storeTokens({ accessToken: 'my-token', refreshToken: 'rt', expiresIn: 900 });
    http.get('/api/data').subscribe();
    const req = httpMock.expectOne('/api/data');
    expect(req.request.headers.get('Authorization')).toBe('Bearer my-token');
  });

  it('does not add Authorization header when access token is null', () => {
    http.get('/api/data').subscribe();
    const req = httpMock.expectOne('/api/data');
    expect(req.request.headers.has('Authorization')).toBe(false);
  });

  it('does NOT add Authorization header to /api/v1/auth/refresh requests', () => {
    authService.storeTokens({ accessToken: 'my-token', refreshToken: 'rt', expiresIn: 900 });
    http.post('/api/v1/auth/refresh', {}).subscribe();
    const req = httpMock.expectOne('/api/v1/auth/refresh');
    expect(req.request.headers.has('Authorization')).toBe(false);
  });

  it('does NOT add Authorization header to /api/v1/auth/logout requests', () => {
    // logout is handled via the service using HttpBackend, not the intercepted HttpClient,
    // but for robustness, verify the interceptor still skips auth endpoints
    authService.storeTokens({ accessToken: 'my-token', refreshToken: 'rt', expiresIn: 900 });
    http.post('/api/v1/auth/logout', {}).subscribe();
    const req = httpMock.expectOne('/api/v1/auth/logout');
    expect(req.request.headers.has('Authorization')).toBe(false);
  });

  it('on 401, calls auth.refresh() and retries the request with the new token', () => {
    authService.storeTokens({ accessToken: 'old-token', refreshToken: 'rt', expiresIn: 900 });
    vi.spyOn(authService, 'refresh').mockReturnValue(
      of(true)
        .pipe
        // Simulate what refresh does: update the stored token
        // (In reality AuthService.refresh() calls storeTokens internally)
        (),
    );
    // Override getAccessToken so retry uses new token
    let callCount = 0;
    vi.spyOn(authService, 'getAccessToken').mockImplementation(() => {
      callCount++;
      return callCount === 1 ? 'old-token' : 'new-token';
    });

    let responseError: unknown;
    http.get('/api/data').subscribe({
      error: (e: unknown) => {
        responseError = e;
      },
      next: () => undefined,
    });

    // First request fails with 401
    const firstReq = httpMock.expectOne('/api/data');
    expect(firstReq.request.headers.get('Authorization')).toBe('Bearer old-token');
    firstReq.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    // Interceptor retries after refresh
    const retryReq = httpMock.expectOne('/api/data');
    expect(retryReq.request.headers.get('Authorization')).toBe('Bearer new-token');
    retryReq.flush({ data: 'ok' });

    expect(responseError).toBeUndefined();
  });

  it('on 401 where refresh fails, navigates to /auth/login', () => {
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    authService.storeTokens({ accessToken: 'token', refreshToken: 'rt', expiresIn: 900 });
    vi.spyOn(authService, 'refresh').mockReturnValue(of(false));

    let errorCaught = false;
    http.get('/api/data').subscribe({
      error: (e: unknown) => {
        if (e instanceof HttpErrorResponse) errorCaught = true;
      },
    });

    const req = httpMock.expectOne('/api/data');
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(navigateSpy).toHaveBeenCalledWith(['/auth/login']);
    expect(errorCaught).toBe(true);
  });

  it('does not call refresh on non-401 errors', () => {
    const refreshSpy = vi.spyOn(authService, 'refresh');
    authService.storeTokens({ accessToken: 'token', refreshToken: 'rt', expiresIn: 900 });

    http.get('/api/data').subscribe({ error: () => undefined });
    const req = httpMock.expectOne('/api/data');
    req.flush('Server Error', { status: 500, statusText: 'Internal Server Error' });

    expect(refreshSpy).not.toHaveBeenCalled();
  });
});
