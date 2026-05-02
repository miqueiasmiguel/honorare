import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from '../auth/auth.service';
import { adminGuard } from './admin.guard';

function makeFakeJwt(role: string): string {
  return `h.${btoa(JSON.stringify({ role, sub: 'user-id' }))}.s`;
}

describe('adminGuard', () => {
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

  function runGuard(): boolean | UrlTree {
    return TestBed.runInInjectionContext(() => adminGuard({} as never, {} as never)) as
      | boolean
      | UrlTree;
  }

  it('adminGuard_WhenRoleIsTenantAdmin_AllowsActivation', () => {
    authService.storeTokens({
      accessToken: makeFakeJwt('TenantAdmin'),
      refreshToken: 'rt',
      expiresIn: 900,
    });

    const result = runGuard();

    expect(result).toBe(true);
  });

  it('adminGuard_WhenRoleIsSaasAdmin_RedirectsToRoot', () => {
    authService.storeTokens({
      accessToken: makeFakeJwt('SaasAdmin'),
      refreshToken: 'rt',
      expiresIn: 900,
    });

    const result = runGuard() as UrlTree;

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result)).toBe('/');
  });

  it('adminGuard_WhenNotAuthenticated_RedirectsToRoot', () => {
    const result = runGuard() as UrlTree;

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result)).toBe('/');
  });

  it('adminGuard_WhenRoleIsMedico_RedirectsToRoot', () => {
    authService.storeTokens({
      accessToken: makeFakeJwt('Medico'),
      refreshToken: 'rt',
      expiresIn: 900,
    });

    const result = runGuard() as UrlTree;

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result)).toBe('/');
  });
});
