import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Painel } from './painel';

describe('Painel', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Painel],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('should create the component', () => {
    const fixture = TestBed.createComponent(Painel);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders médico-specific content', () => {
    const fixture = TestBed.createComponent(Painel);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Portal do Médico');
  });
});
