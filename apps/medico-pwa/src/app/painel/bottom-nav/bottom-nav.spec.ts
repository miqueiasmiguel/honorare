import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BottomNavComponent } from './bottom-nav';

describe('BottomNavComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BottomNavComponent],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(BottomNavComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders two navigation items', () => {
    const fixture = TestBed.createComponent(BottomNavComponent);
    fixture.detectChanges();
    const items = (fixture.nativeElement as HTMLElement).querySelectorAll('.bottom-nav__item');
    expect(items.length).toBe(2);
  });

  it('has link to /guias', () => {
    const fixture = TestBed.createComponent(BottomNavComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('a[href="/guias"]')).not.toBeNull();
  });

  it('has link to /perfil', () => {
    const fixture = TestBed.createComponent(BottomNavComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('a[href="/perfil"]')).not.toBeNull();
  });

  it('renders Guias and Perfil labels', () => {
    const fixture = TestBed.createComponent(BottomNavComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const labels = Array.from(el.querySelectorAll('.bottom-nav__label')).map((l) =>
      l.textContent.trim(),
    );
    expect(labels).toContain('Guias');
    expect(labels).toContain('Perfil');
  });
});
