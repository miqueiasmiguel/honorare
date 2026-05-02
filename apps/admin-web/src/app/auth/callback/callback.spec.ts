import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { Location } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute } from '@angular/router';
import { Callback } from './callback';
import { AuthService } from '../auth.service';

function makeRoute(params: Record<string, string>): Partial<ActivatedRoute> {
  return { snapshot: { queryParams: params } as never };
}

function setupTestBed(params: Record<string, string>): void {
  TestBed.configureTestingModule({
    imports: [Callback],
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      AuthService,
      { provide: ActivatedRoute, useValue: makeRoute(params) },
    ],
  });
}

describe('Callback — happy path', () => {
  let router: Router;
  let location: Location;
  let authService: AuthService;

  beforeEach(async () => {
    localStorage.clear();
    setupTestBed({ accessToken: 'at-value', refreshToken: 'rt-value', expiresIn: '900' });
    await TestBed.compileComponents();
    router = TestBed.inject(Router);
    location = TestBed.inject(Location);
    authService = TestBed.inject(AuthService);
    // Previne NG04002: provideRouter([]) não registra as rotas destino do Callback.
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('calls auth.storeTokens() with correct values from query params', async () => {
    const storeTokensSpy = vi.spyOn(authService, 'storeTokens');
    const fixture = TestBed.createComponent(Callback);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(storeTokensSpy).toHaveBeenCalledWith({
      accessToken: 'at-value',
      refreshToken: 'rt-value',
      expiresIn: 900,
    });
  });

  it('calls location.replaceState() to strip query params from browser history', async () => {
    const replaceStateSpy = vi.spyOn(location, 'replaceState');
    const fixture = TestBed.createComponent(Callback);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(replaceStateSpy).toHaveBeenCalled();
    const calledWith = replaceStateSpy.mock.calls[0][0];
    expect(calledWith).not.toContain('accessToken');
    expect(calledWith).not.toContain('refreshToken');
  });

  it('location.replaceState() is called BEFORE storeTokens() (security order)', async () => {
    const callOrder: string[] = [];
    vi.spyOn(location, 'replaceState').mockImplementation(() => {
      callOrder.push('replaceState');
    });
    vi.spyOn(authService, 'storeTokens').mockImplementation(() => {
      callOrder.push('storeTokens');
    });

    const fixture = TestBed.createComponent(Callback);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(callOrder[0]).toBe('replaceState');
    expect(callOrder[1]).toBe('storeTokens');
  });

  it('navigates to /admin/users after storing tokens when role is not SaasAdmin', async () => {
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(Callback);
    fixture.detectChanges();
    await fixture.whenStable();

    // at-value is not a valid JWT → role() returns null → fallback to /admin/users
    expect(navigateSpy).toHaveBeenCalledWith(['/admin/users']);
  });
});

describe('Callback — role-based redirect', () => {
  it('navigates to /saas after storing tokens when role is SaasAdmin', async () => {
    localStorage.clear();
    const saasPayload = btoa(JSON.stringify({ role: 'SaasAdmin', sub: 'admin-id' }));
    const saasJwt = `header.${saasPayload}.sig`;
    setupTestBed({ accessToken: saasJwt, refreshToken: 'rt-value', expiresIn: '900' });
    await TestBed.compileComponents();
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    const fixture = TestBed.createComponent(Callback);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(navigateSpy).toHaveBeenCalledWith(['/saas/tenants']);
    localStorage.clear();
  });
});

describe('Callback — missing params', () => {
  async function expectLoginRedirect(params: Record<string, string>): Promise<void> {
    localStorage.clear();
    setupTestBed(params);
    await TestBed.compileComponents();
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    const fixture = TestBed.createComponent(Callback);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(navigateSpy).toHaveBeenCalledWith(['/auth/login']);
    localStorage.clear();
  }

  afterEach(() => {
    localStorage.clear();
  });

  it('navigates to /auth/login when accessToken is missing', async () => {
    await expectLoginRedirect({ refreshToken: 'rt', expiresIn: '900' });
  });

  it('navigates to /auth/login when refreshToken is missing', async () => {
    await expectLoginRedirect({ accessToken: 'at', expiresIn: '900' });
  });

  it('navigates to /auth/login when expiresIn is missing', async () => {
    await expectLoginRedirect({ accessToken: 'at', refreshToken: 'rt' });
  });
});
