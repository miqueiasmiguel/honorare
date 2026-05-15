import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { DemonstrativoService } from '../demonstrativo.service';
import type { DemonstrativoDto, ListarDemonstrativosResult } from '../demonstrativo.types';
import { DemonstrativoListComponent } from './demonstrativo-list.component';

function makeDemo(overrides: Partial<DemonstrativoDto> = {}): DemonstrativoDto {
  return {
    id: 'demo-1',
    operadoraId: 'op-1',
    operadoraNome: 'UNIMED',
    competencia: '2026-04',
    dataRecebimento: '2026-04-30',
    observacao: null,
    totalItens: 5,
    itensConciliados: 2,
    criadoEm: '2026-04-30T10:00:00Z',
    ...overrides,
  };
}

function makeResult(itens: DemonstrativoDto[]): ListarDemonstrativosResult {
  return { itens, total: itens.length, pagina: 1, itensPorPagina: 20 };
}

function setup(demos: DemonstrativoDto[] = [makeDemo()]) {
  const demonstrativoService = {
    listar: vi.fn().mockReturnValue(of(makeResult(demos))),
    excluir: vi.fn().mockReturnValue(of(undefined)),
  };
  const router = { navigate: vi.fn().mockReturnValue(Promise.resolve(true)) };

  TestBed.configureTestingModule({
    imports: [DemonstrativoListComponent],
    providers: [
      { provide: DemonstrativoService, useValue: demonstrativoService },
      { provide: Router, useValue: router },
    ],
  });

  const fixture = TestBed.createComponent(DemonstrativoListComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    demonstrativoService,
    router,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('DemonstrativoListComponent', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('lista exibe demonstrativos com badge de conciliação', () => {
    const demos = [makeDemo({ itensConciliados: 3, totalItens: 5 })];
    const { el } = setup(demos);

    const badge = el.querySelector('.demonstrativo-list__badge');
    expect(badge).not.toBeNull();
    expect(badge?.textContent).toContain('3');
    expect(badge?.textContent).toContain('5');
  });

  it('filtro por competencia dispara busca com debounce', () => {
    vi.useFakeTimers();
    const { component, demonstrativoService } = setup();
    demonstrativoService.listar.mockClear();

    component.onFiltroCompetenciaChange('2026-03');
    expect(demonstrativoService.listar).not.toHaveBeenCalled();

    vi.advanceTimersByTime(400);
    expect(demonstrativoService.listar).toHaveBeenCalledWith(
      expect.objectContaining({ competencia: '2026-03' }),
    );
  });

  it('botão excluir desabilitado para itens com conciliados > 0', () => {
    const demos = [
      makeDemo({ id: 'demo-com-conciliados', itensConciliados: 1 }),
      makeDemo({ id: 'demo-sem-conciliados', itensConciliados: 0 }),
    ];
    const { el } = setup(demos);

    const btns = el.querySelectorAll<HTMLButtonElement>('.demonstrativo-list__btn-excluir');
    expect(btns).toHaveLength(2);
    expect(btns[0].disabled).toBe(true);
    expect(btns[1].disabled).toBe(false);
  });
});
