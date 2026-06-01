import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { Painel } from './painel';
import { AuthService } from '../auth/auth.service';

const authServiceMock = {
  userEmail: signal<string | null>(null),
};

describe('Painel', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Painel],
      providers: [provideRouter([]), { provide: AuthService, useValue: authServiceMock }],
    }).compileComponents();
  });

  it('should create the component', () => {
    const fixture = TestBed.createComponent(Painel);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders honorare wordmark', () => {
    const fixture = TestBed.createComponent(Painel);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.painel-topbar__wordmark')?.textContent).toBe('honorare');
  });

  it('renders router-outlet', () => {
    const fixture = TestBed.createComponent(Painel);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('router-outlet')).not.toBeNull();
  });

  it('renders bottom navigation', () => {
    const fixture = TestBed.createComponent(Painel);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('app-bottom-nav')).not.toBeNull();
  });
});
