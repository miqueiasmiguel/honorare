import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { PortesAnestesicosComponent } from './portes-anestesicos.component';
import { CatalogService } from '../../catalog.service';
import type { TabelaPorteAnestesicoItem } from '../../catalog.types';

const mockPortes: TabelaPorteAnestesicoItem[] = [
  {
    id: 'p-2',
    porteLetra: 'B',
    valorEnfermaria: 180,
    valorApartamento: 288,
    valorAmbulatorial: null,
    atualizadoEm: '2026-01-01T00:00:00Z',
  },
  {
    id: 'p-1',
    porteLetra: 'A',
    valorEnfermaria: 150,
    valorApartamento: 240,
    valorAmbulatorial: null,
    atualizadoEm: '2026-01-01T00:00:00Z',
  },
];

function setup(portes: TabelaPorteAnestesicoItem[] = mockPortes) {
  const catalogServiceSpy = {
    listarPortesAnestesico: vi.fn().mockReturnValue(of(portes)),
    excluirPorteAnestesico: vi.fn().mockReturnValue(of(undefined)),
  };

  TestBed.configureTestingModule({
    imports: [PortesAnestesicosComponent],
    providers: [{ provide: CatalogService, useValue: catalogServiceSpy }],
  });

  const fixture = TestBed.createComponent(PortesAnestesicosComponent);
  fixture.componentRef.setInput('operadoraId', 'op-1');
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    catalogService: catalogServiceSpy,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('PortesAnestesicosComponent', () => {
  it('lista portes ordenados por letra', () => {
    const { el } = setup();
    const linhas = el.querySelectorAll('.portes-anestesicos__linha');
    expect(linhas).toHaveLength(2);
    expect(linhas[0].textContent).toContain('A');
    expect(linhas[1].textContent).toContain('B');
  });

  it('clicar [X] com confirmação chama excluirPorteAnestesico', () => {
    const { el, catalogService } = setup();
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const btn = el.querySelector<HTMLButtonElement>('.portes-anestesicos__btn-excluir');
    btn?.click();
    expect(catalogService.excluirPorteAnestesico).toHaveBeenCalledOnce();
    confirmSpy.mockRestore();
  });

  it('clicar [X] sem confirmação não chama excluirPorteAnestesico', () => {
    const { el, catalogService } = setup();
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
    const btn = el.querySelector<HTMLButtonElement>('.portes-anestesicos__btn-excluir');
    btn?.click();
    expect(catalogService.excluirPorteAnestesico).not.toHaveBeenCalled();
    confirmSpy.mockRestore();
  });
});
