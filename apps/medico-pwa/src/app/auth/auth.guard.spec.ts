import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom, Observable, of } from 'rxjs';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

@Component({ template: '' })
class StubComponent {}

describe('authGuard', () => {
  let router: Router;
  let authService: AuthService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', canActivate: [authGuard], component: StubComponent }]),
        provideHttpClient(),
        provideHttpClientTesting(),
        AuthService,
      ],
    });
    router = TestBed.inject(Router);
    authService = TestBed.inject(AuthService);
  });

  afterEach(() => {
    localStorage.clear();
    TestBed.inject(HttpTestingController).verify();
  });

  function runGuard(): boolean | UrlTree | Observable<boolean | UrlTree> {
    return TestBed.runInInjectionContext(() => authGuard({} as never, {} as never)) as
      | boolean
      | UrlTree
      | Observable<boolean | UrlTree>;
  }

  async function resolveGuard(): Promise<boolean | UrlTree> {
    const result = runGuard();
    if (result instanceof Observable) {
      return firstValueFrom(result);
    }
    return Promise.resolve(result);
  }

  it('returns true when isAuthenticated is true', () => {
    authService.storeTokens({ accessToken: 'at', refreshToken: 'rt', expiresIn: 900 });
    const result = runGuard();
    expect(result).toBe(true);
  });

  it('returns UrlTree to /auth/login when no access token and no refresh token in localStorage', () => {
    const result = runGuard() as UrlTree;
    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result)).toBe('/auth/login');
  });

  it('calls auth.refresh() and returns true when refresh token exists and refresh succeeds', async () => {
    localStorage.setItem('_rt', 'stored-rt');
    vi.spyOn(authService, 'refresh').mockReturnValue(of(true));

    const result = await resolveGuard();
    expect(result).toBe(true);
  });

  it('returns UrlTree to /auth/login when refresh token exists but refresh fails', async () => {
    localStorage.setItem('_rt', 'stored-rt');
    vi.spyOn(authService, 'refresh').mockReturnValue(of(false));

    const result = await resolveGuard();
    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/auth/login');
  });
});
