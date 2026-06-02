import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { ImpersonationBanner } from './impersonation-banner';

function makeAuthMock(isImpersonating: boolean, tenantName: string | null = 'Clínica Alfa') {
  return {
    isImpersonating: signal(isImpersonating),
    actingTenantName: signal(tenantName),
    exitImpersonation: vi.fn().mockReturnValue(of(true)),
  };
}

describe('ImpersonationBanner', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('banner aparece quando isImpersonating é true', () => {
    const auth = makeAuthMock(true);
    TestBed.configureTestingModule({
      imports: [ImpersonationBanner],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
    const fixture = TestBed.createComponent(ImpersonationBanner);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="impersonation-banner"]')).toBeTruthy();
    expect(el.textContent).toContain('Clínica Alfa');
  });

  it('banner oculto quando isImpersonating é false', () => {
    const auth = makeAuthMock(false);
    TestBed.configureTestingModule({
      imports: [ImpersonationBanner],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
    const fixture = TestBed.createComponent(ImpersonationBanner);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="impersonation-banner"]')).toBeNull();
  });

  it('clicar Sair chama exitImpersonation e navega para /saas', () => {
    const auth = makeAuthMock(true);
    TestBed.configureTestingModule({
      imports: [ImpersonationBanner],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
    const fixture = TestBed.createComponent(ImpersonationBanner);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigate');
    const el = fixture.nativeElement as HTMLElement;

    const btn = el.querySelector<HTMLButtonElement>('[data-testid="btn-sair-impersonation"]');
    btn?.click();
    fixture.detectChanges();

    expect(auth.exitImpersonation).toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith(['/saas']);
  });
});
