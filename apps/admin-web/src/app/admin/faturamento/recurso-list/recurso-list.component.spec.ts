import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { RecursoService } from '../recurso.service';
import { CatalogService } from '../../catalog/catalog.service';
import type { ListarRecursosResult, RecursoDto } from '../recurso.types';
import { RecursoListComponent } from './recurso-list.component';

function makeRecurso(overrides: Partial<RecursoDto> = {}): RecursoDto {
  return {
    id: 'rec-1',
    operadoraId: 'op-1',
    operadoraNome: 'UNIMED',
    prestadorId: 'prest-1',
    prestadorNome: 'Dr. João',
    prestadorRegistroProfissional: null,
    numero: '202601-001',
    dataEmissao: '2026-01-15',
    observacao: null,
    totalGuias: 3,
    criadoEm: '2026-01-15T00:00:00Z',
    tipo: 'GlosaParcial',
    ...overrides,
  };
}

function makeResult(itens: RecursoDto[]): ListarRecursosResult {
  return { itens, total: itens.length, pagina: 1, itensPorPagina: 20 };
}

function setup(recursos: RecursoDto[] = [makeRecurso()]) {
  const recursoService = {
    listar: vi.fn().mockReturnValue(of(makeResult(recursos))),
    excluir: vi.fn().mockReturnValue(of(undefined)),
    baixarPdf: vi.fn(),
  };
  const catalogService = {
    listarPrestadores: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 200 })),
    listarOperadoras: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 200 })),
  };
  const router = { navigate: vi.fn().mockReturnValue(Promise.resolve(true)) };

  TestBed.configureTestingModule({
    imports: [RecursoListComponent],
    providers: [
      { provide: RecursoService, useValue: recursoService },
      { provide: CatalogService, useValue: catalogService },
      { provide: Router, useValue: router },
    ],
  });

  const fixture = TestBed.createComponent(RecursoListComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    recursoService,
    router,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('RecursoListComponent', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('lista exibe recursos com badge de guias', () => {
    const recursos = [
      makeRecurso({ id: 'r-1', totalGuias: 5 }),
      makeRecurso({ id: 'r-2', totalGuias: 2 }),
    ];
    const { el } = setup(recursos);

    const rows = el.querySelectorAll('.recurso-list__row');
    expect(rows).toHaveLength(2);

    const badge = el.querySelector('.recurso-list__badge--guias');
    expect(badge).not.toBeNull();
    expect(badge?.textContent).toContain('5');
  });

  it('filtro por operadora dispara busca', () => {
    vi.useFakeTimers();
    const { component, recursoService } = setup();
    recursoService.listar.mockClear();

    component.onFiltroOperadoraChange('op-2');
    expect(recursoService.listar).not.toHaveBeenCalled();

    vi.advanceTimersByTime(400);
    expect(recursoService.listar).toHaveBeenCalledWith(
      expect.objectContaining({ operadoraId: 'op-2' }),
    );
  });

  it('botão PDF dispara download via service', () => {
    const { el, recursoService } = setup([makeRecurso({ id: 'rec-1' })]);
    const items = Array.from(el.querySelectorAll<HTMLButtonElement>('.recurso-list__menu-item'));
    const pdfBtnIdx = items.findIndex((b) => b.innerHTML.includes('Baixar PDF'));
    expect(pdfBtnIdx).toBeGreaterThanOrEqual(0);
    items[pdfBtnIdx].click();
    expect(recursoService.baixarPdf).toHaveBeenCalledWith('rec-1');
  });
});
