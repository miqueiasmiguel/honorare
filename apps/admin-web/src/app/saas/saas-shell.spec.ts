import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { SaasShell } from './saas-shell';
import { AuthService } from '../auth/auth.service';

describe('SaasShell', () => {
  let authLogoutSpy: ReturnType<typeof vi.fn>;
  let routerNavigateSpy: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    authLogoutSpy = vi.fn();
    routerNavigateSpy = vi.fn().mockResolvedValue(true);

    TestBed.configureTestingModule({
      imports: [SaasShell],
      providers: [provideRouter([]), { provide: AuthService, useValue: { logout: authLogoutSpy } }],
    });

    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockImplementation(routerNavigateSpy);
  });

  it('should create the component', () => {
    const fixture = TestBed.createComponent(SaasShell);
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders sidebar link to tenants', () => {
    const fixture = TestBed.createComponent(SaasShell);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const text = el.querySelector('a')?.textContent ?? '';
    expect(text.trim()).toBe('Tenants');
  });

  it('renders router-outlet', () => {
    const fixture = TestBed.createComponent(SaasShell);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const outlet = el.querySelector('router-outlet');
    expect(outlet).toBeTruthy();
  });

  it('renders logout button', () => {
    const fixture = TestBed.createComponent(SaasShell);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector('button.saas-sidebar__logout');
    expect((btn?.textContent ?? '').trim()).toBe('Sair');
  });

  it('calls AuthService.logout and navigates to /auth/login on button click', () => {
    const fixture = TestBed.createComponent(SaasShell);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector<HTMLButtonElement>('button.saas-sidebar__logout');
    btn?.click();
    expect(authLogoutSpy).toHaveBeenCalledOnce();
    expect(routerNavigateSpy).toHaveBeenCalledWith(['/auth/login']);
  });
});
