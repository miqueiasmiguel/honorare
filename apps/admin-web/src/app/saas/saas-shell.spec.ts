import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SaasShell } from './saas-shell';

describe('SaasShell', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SaasShell],
      providers: [provideRouter([])],
    });
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
});
