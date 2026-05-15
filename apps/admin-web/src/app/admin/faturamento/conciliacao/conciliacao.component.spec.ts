import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { DemonstrativoService } from '../demonstrativo.service';
import { GuiaService } from '../guia.service';
import type { DemonstrativoDetalheDto, ItemDemonstrativoDto } from '../demonstrativo.types';
import { ConciliacaoComponent } from './conciliacao.component';

function makeItem(overrides: Partial<ItemDemonstrativoDto> = {}): ItemDemonstrativoDto {
  return {
    id: 'item-1',
    senha: 'ABC123',
    codigoTuss: '31303079',
    descricao: null,
    valorApresentado: 100,
    valorPago: 80,
    valorGlosado: 20,
    motivoGlosa: null,
    itemGuiaId: null,
    conciliado: false,
    ...overrides,
  };
}

function makeDetalhe(itens: ItemDemonstrativoDto[]): DemonstrativoDetalheDto {
  return {
    header: {
      id: 'demo-1',
      operadoraId: 'op-1',
      operadoraNome: 'UNIMED',
      competencia: '2026-05',
      dataRecebimento: '2026-05-10',
      observacao: null,
      totalItens: itens.length,
      itensConciliados: itens.filter((i) => i.conciliado).length,
      criadoEm: '2026-05-10T00:00:00Z',
    },
    itens,
  };
}

function setup(itens: ItemDemonstrativoDto[] = [makeItem()]) {
  const demonstrativoService = {
    obterPorId: vi.fn().mockReturnValue(of(makeDetalhe(itens))),
    conciliarItem: vi.fn().mockReturnValue(of(undefined)),
    desconciliarItem: vi.fn().mockReturnValue(of(undefined)),
  };
  const guiaService = {
    listar: vi.fn().mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 10 })),
    obterPorId: vi.fn().mockReturnValue(of(null)),
  };
  const activatedRoute = {
    snapshot: {
      paramMap: { get: (key: string) => (key === 'id' ? 'demo-1' : null) },
    },
  };

  TestBed.configureTestingModule({
    imports: [ConciliacaoComponent],
    providers: [
      { provide: DemonstrativoService, useValue: demonstrativoService },
      { provide: GuiaService, useValue: guiaService },
      { provide: ActivatedRoute, useValue: activatedRoute },
    ],
  });

  const fixture = TestBed.createComponent(ConciliacaoComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    demonstrativoService,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('ConciliacaoComponent', () => {
  it('exibe itens do demonstrativo', () => {
    const { el } = setup([makeItem({ id: 'item-1' }), makeItem({ id: 'item-2', senha: 'XYZ999' })]);
    expect(el.querySelectorAll('.conciliacao__item')).toHaveLength(2);
  });

  it('item conciliado exibe badge verde e botão Desvincular', () => {
    const { el } = setup([makeItem({ conciliado: true, itemGuiaId: 'guia-item-1' })]);
    expect(el.querySelector('.conciliacao__badge--conciliado')).not.toBeNull();
    expect(el.querySelector('.conciliacao__btn-desvincular')).not.toBeNull();
  });

  it('item pendente exibe badge amarelo e botão Vincular', () => {
    const { el } = setup([makeItem({ conciliado: false })]);
    expect(el.querySelector('.conciliacao__badge--pendente')).not.toBeNull();
    expect(el.querySelector('.conciliacao__btn-vincular')).not.toBeNull();
  });

  it('progresso reflete contagem correta', () => {
    const { el } = setup([
      makeItem({ id: 'item-1', conciliado: true }),
      makeItem({ id: 'item-2', conciliado: false }),
    ]);
    const progresso = el.querySelector('.conciliacao__progresso');
    expect(progresso?.textContent).toContain('1');
    expect(progresso?.textContent).toContain('2');
  });

  it('clicar Vincular emite evento com itemDemId', () => {
    const { el, component } = setup([makeItem({ id: 'item-a', conciliado: false })]);
    el.querySelector<HTMLButtonElement>('.conciliacao__btn-vincular')?.click();
    expect(component.itemBuscandoId()).toBe('item-a');
  });

  it('clicar Desvincular chama desconciliar e atualiza estado', () => {
    const { el, fixture, component, demonstrativoService } = setup([
      makeItem({ id: 'item-c', conciliado: true, itemGuiaId: 'guia-item-1' }),
    ]);
    el.querySelector<HTMLButtonElement>('.conciliacao__btn-desvincular')?.click();
    fixture.detectChanges();
    expect(demonstrativoService.desconciliarItem).toHaveBeenCalledWith('demo-1', 'item-c');
    expect(component.itens()[0].conciliado).toBe(false);
  });

  it('erro na conciliação exibe mensagem inline', () => {
    const { el, fixture, demonstrativoService } = setup([
      makeItem({ id: 'item-err', conciliado: true }),
    ]);
    demonstrativoService.desconciliarItem.mockReturnValue(throwError(() => new Error('fail')));
    el.querySelector<HTMLButtonElement>('.conciliacao__btn-desvincular')?.click();
    fixture.detectChanges();
    expect(el.querySelector('.conciliacao__erro')).not.toBeNull();
  });
});
