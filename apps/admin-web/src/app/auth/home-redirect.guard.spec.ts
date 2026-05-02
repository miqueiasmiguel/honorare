import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { homeRedirectGuard } from './home-redirect.guard';

function makeFakeJwt(role: string): string {
  return `h.${btoa(JSON.stringify({ role, sub: 'user-id' }))}.s`;
}

describe('homeRedirectGuard', () => {
  let router: Router;
  let authService: AuthService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), AuthService],
    });
    router = TestBed.inject(Router);
    authService = TestBed.inject(AuthService);
  });

  afterEach(() => {
    localStorage.clear();
    TestBed.inject(HttpTestingController).verify();
  });

  function runGuard(): UrlTree {
    return TestBed.runInInjectionContext(() =>
      homeRedirectGuard({} as never, {} as never),
    ) as UrlTree;
  }

  it('homeRedirectGuard_WhenRoleIsSaasAdmin_RedirectsToSaasTenants', () => {
    authService.storeTokens({
      accessToken: makeFakeJwt('SaasAdmin'),
      refreshToken: 'rt',
      expiresIn: 900,
    });

    const result = runGuard();

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result)).toBe('/saas/tenants');
  });

  it('homeRedirectGuard_WhenRoleIsTenantAdmin_RedirectsToAdminUsers', () => {
    authService.storeTokens({
      accessToken: makeFakeJwt('TenantAdmin'),
      refreshToken: 'rt',
      expiresIn: 900,
    });

    const result = runGuard();

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result)).toBe('/admin/users');
  });

  it('homeRedirectGuard_WhenNotAuthenticated_RedirectsToAuthLogin', () => {
    const result = runGuard();

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result)).toBe('/auth/login');
  });

  it('homeRedirectGuard_WhenRoleIsMedico_RedirectsToAuthLogin', () => {
    authService.storeTokens({
      accessToken: makeFakeJwt('Medico'),
      refreshToken: 'rt',
      expiresIn: 900,
    });

    const result = runGuard();

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result)).toBe('/auth/login');
  });
});
