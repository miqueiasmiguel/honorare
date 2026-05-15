import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { CatalogService } from '../../catalog/catalog.service';
import { DemonstrativoService } from '../demonstrativo.service';
import type { DemonstrativoDetalheDto } from '../demonstrativo.types';
import { DemonstrativoFormComponent } from './demonstrativo-form.component';

const mockDetalhe: DemonstrativoDetalheDto = {
  header: {
    id: 'demo-1',
    operadoraId: 'op-1',
    operadoraNome: 'UNIMED',
    competencia: '2026-05',
    dataRecebimento: '2026-05-10',
    observacao: 'obs teste',
    totalItens: 1,
    itensConciliados: 0,
    criadoEm: '2026-05-10T00:00:00Z',
  },
  itens: [
    {
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
    },
  ],
};

function makeDemonstrativoServiceSpy() {
  return {
    criar: vi.fn().mockReturnValue(of(mockDetalhe)),
    atualizar: vi.fn().mockReturnValue(of(mockDetalhe.header)),
    obterPorId: vi.fn().mockReturnValue(of(mockDetalhe)),
    adicionarItem: vi.fn().mockReturnValue(of(mockDetalhe)),
    removerItem: vi.fn().mockReturnValue(of(undefined)),
  };
}

function makeCatalogServiceSpy() {
  return {
    listarOperadoras: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 200 })),
  };
}

function setup(options: { id?: string } = {}) {
  const demonstrativoService = makeDemonstrativoServiceSpy();
  const catalogService = makeCatalogServiceSpy();
  const router = { navigate: vi.fn().mockReturnValue(Promise.resolve(true)) };
  const activatedRoute = {
    snapshot: {
      paramMap: { get: (key: string) => (key === 'id' ? (options.id ?? null) : null) },
    },
  };

  TestBed.configureTestingModule({
    imports: [DemonstrativoFormComponent],
    providers: [
      { provide: DemonstrativoService, useValue: demonstrativoService },
      { provide: CatalogService, useValue: catalogService },
      { provide: Router, useValue: router },
      { provide: ActivatedRoute, useValue: activatedRoute },
    ],
  });

  const fixture = TestBed.createComponent(DemonstrativoFormComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    demonstrativoService,
    catalogService,
    router,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('DemonstrativoFormComponent', () => {
  it('form cria demonstrativo e navega para lista', () => {
    const { component, fixture, el, demonstrativoService, router } = setup();

    component.operadoraId.set('op-1');
    component.competencia.set('2026-05');
    component.dataRecebimento.set('2026-05-01');
    fixture.detectChanges();

    el.querySelector<HTMLButtonElement>('.demonstrativo-form__btn-salvar')?.click();

    expect(demonstrativoService.criar).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/admin/demonstrativos']);
  });

  it('form carrega demonstrativo existente e preenche campos', () => {
    const { component } = setup({ id: 'demo-1' });

    expect(component.modoEditar()).toBe(true);
    expect(component.operadoraId()).toBe('op-1');
    expect(component.competencia()).toBe('2026-05');
    expect(component.dataRecebimento()).toBe('2026-05-10');
  });

  it('adicionar item inline exibe linha nova', () => {
    const { el, fixture } = setup();

    expect(el.querySelectorAll('.demonstrativo-form__item-row')).toHaveLength(0);

    el.querySelector<HTMLButtonElement>('.demonstrativo-form__btn-adicionar-item')?.click();
    fixture.detectChanges();

    expect(el.querySelectorAll('.demonstrativo-form__item-row')).toHaveLength(1);
  });

  it('remover item não-conciliado remove da lista', () => {
    const { el, fixture, component } = setup();

    component.itens.set([
      {
        id: null,
        senha: 'XYZ',
        codigoTuss: '999',
        descricao: null,
        valorApresentado: 50,
        valorPago: 40,
        motivoGlosa: null,
        conciliado: false,
      },
    ]);
    fixture.detectChanges();

    expect(el.querySelectorAll('.demonstrativo-form__item-row')).toHaveLength(1);

    el.querySelector<HTMLButtonElement>('.demonstrativo-form__btn-remover-item')?.click();
    fixture.detectChanges();

    expect(el.querySelectorAll('.demonstrativo-form__item-row')).toHaveLength(0);
  });

  it('remover item conciliado mantém na lista (desabilitado)', () => {
    const { el, fixture, component } = setup();

    component.itens.set([
      {
        id: 'item-1',
        senha: 'XYZ',
        codigoTuss: '999',
        descricao: null,
        valorApresentado: 50,
        valorPago: 40,
        motivoGlosa: null,
        conciliado: true,
      },
    ]);
    fixture.detectChanges();

    const btn = el.querySelector<HTMLButtonElement>('.demonstrativo-form__btn-remover-item');
    expect(btn?.disabled).toBe(true);

    btn?.click();
    fixture.detectChanges();

    expect(el.querySelectorAll('.demonstrativo-form__item-row')).toHaveLength(1);
  });

  it('valorGlosado exibido = valorApresentado − valorPago', () => {
    const { el, fixture, component } = setup();

    component.itens.set([
      {
        id: null,
        senha: 'ABC',
        codigoTuss: '123',
        descricao: null,
        valorApresentado: 100,
        valorPago: 80,
        motivoGlosa: null,
        conciliado: false,
      },
    ]);
    fixture.detectChanges();

    const glosado = el.querySelector('.demonstrativo-form__valor-glosado');
    expect(glosado?.textContent).toContain('20');
  });
});
