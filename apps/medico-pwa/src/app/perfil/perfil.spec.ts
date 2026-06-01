import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { signal } from '@angular/core';
import { Perfil } from './perfil';
import { AuthService } from '../auth/auth.service';

const authMock = {
  userEmail: signal<string | null>('medico@example.com'),
  logout: vi.fn(),
};

describe('Perfil', () => {
  beforeEach(async () => {
    authMock.userEmail.set('medico@example.com');
    authMock.logout.mockClear();
    await TestBed.configureTestingModule({
      imports: [Perfil],
      providers: [provideRouter([]), { provide: AuthService, useValue: authMock }],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(Perfil);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders user email', () => {
    const fixture = TestBed.createComponent(Perfil);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('medico@example.com');
  });

  it('renders dash when email is null', () => {
    authMock.userEmail.set(null);
    const fixture = TestBed.createComponent(Perfil);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const valueEl = el.querySelector('.perfil__value');
    expect(valueEl ? valueEl.textContent.trim() : '').toBe('—');
  });

  it('calls logout and navigates to /auth/login on button click', () => {
    const fixture = TestBed.createComponent(Perfil);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    (fixture.nativeElement as HTMLElement).querySelector('button')?.click();

    expect(authMock.logout).toHaveBeenCalledOnce();
    expect(navigateSpy).toHaveBeenCalledWith(['/auth/login']);
  });
});
