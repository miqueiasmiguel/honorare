import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { CatalogService } from '../../catalog/catalog.service';
import { GuiaService } from '../guia.service';
import type { GuiaCalculoResult, GuiaDetalheItem } from '../guia.types';
import { GuiaFormComponent } from './guia-form.component';

const mockGuia: GuiaDetalheItem = {
  id: 'guia-1',
  prestadorId: 'prest-1',
  prestadorNome: 'Dr. João',
  operadoraId: 'op-1',
  operadoraNome: 'UNIMED',
  beneficiarioId: 'bene-1',
  beneficiarioNome: 'Maria',
  beneficiarioCarteira: '123456',
  numeroGuia: 'GUIA01',
  dataAtendimento: '2024-03-15',
  situacao: 'Apresentada',
  ehPacote: false,
  observacao: '',
  localAtendimento: 'Hospital Central',
  totalItens: 1,
  criadoEm: '2024-03-15T10:00:00Z',
  atualizadoEm: '2024-03-15T10:00:00Z',
  itens: [
    {
      id: 'item-1',
      procedimentoId: 'proc-1',
      codigoTuss: '4030501',
      descricaoProcedimento: 'Colecistectomia',
      posicaoExecutor: 'Cirurgiao',
      percentualOrdem: 1.0,
      viaAcesso: 'Convencional',
      acomodacao: 'Enfermaria',
      ehUrgencia: false,
      valorApurado: null,
      valorLiquidado: null,
      motivoGlosa: null,
    },
  ],
};

const mockCalculo: GuiaCalculoResult = {
  guiaId: 'guia-1',
  ehPacote: false,
  realizadoEm: '2024-03-15',
  itens: [],
};

function makeGuiaServiceSpy(guia: GuiaDetalheItem | null = null) {
  return {
    criar: vi.fn().mockReturnValue(of(guia ?? mockGuia)),
    atualizar: vi.fn().mockReturnValue(of(guia ?? mockGuia)),
    obterPorId: vi.fn().mockReturnValue(of(guia ?? mockGuia)),
    obterCalculo: vi.fn().mockReturnValue(of(mockCalculo)),
    atualizarPagamentoItem: vi.fn().mockReturnValue(
      of({
        id: 'item-1',
        procedimentoId: 'proc-1',
        codigoTuss: '4030501',
        descricaoProcedimento: 'Colecistectomia',
        posicaoExecutor: 'Cirurgiao',
        percentualOrdem: 1.0,
        viaAcesso: 'Convencional',
        acomodacao: 'Enfermaria',
        ehUrgencia: false,
        valorApurado: null,
        valorLiquidado: 150.5,
        motivoGlosa: 'CB',
      }),
    ),
  };
}

function makeCatalogServiceSpy() {
  return {
    listarPrestadores: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 200 })),
    listarOperadoras: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 200 })),
    listarBeneficiarios: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 1 })),
    lookupOrCreateBeneficiario: vi.fn().mockReturnValue(of(null)),
    listarProcedimentos: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 10 })),
  };
}

function makeRouterSpy() {
  return { navigate: vi.fn().mockReturnValue(Promise.resolve(true)) };
}

function setup(options: { id?: string; guia?: GuiaDetalheItem } = {}) {
  const guiaService = makeGuiaServiceSpy(options.guia ?? null);
  const catalogService = makeCatalogServiceSpy();
  const router = makeRouterSpy();
  const activatedRoute = {
    snapshot: {
      paramMap: { get: (key: string) => (key === 'id' ? (options.id ?? null) : null) },
    },
  };

  TestBed.configureTestingModule({
    imports: [GuiaFormComponent],
    providers: [
      { provide: GuiaService, useValue: guiaService },
      { provide: CatalogService, useValue: catalogService },
      { provide: Router, useValue: router },
      { provide: ActivatedRoute, useValue: activatedRoute },
    ],
  });

  const fixture = TestBed.createComponent(GuiaFormComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    guiaService,
    catalogService,
    router,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('GuiaFormComponent', () => {
  it('renderiza campos obrigatorios', () => {
    const { el } = setup();

    expect(el.querySelector('.guia-form__select--prestador')).not.toBeNull();
    expect(el.querySelector('.guia-form__select--operadora')).not.toBeNull();
    expect(el.querySelector('.guia-form__input--numero-guia')).not.toBeNull();
    expect(el.querySelector('.guia-form__input--data-atendimento')).not.toBeNull();
    expect(el.querySelector('.guia-form__checkbox--eh-pacote')).not.toBeNull();
    expect(el.querySelector('.guia-form__textarea--observacao')).not.toBeNull();
    expect(el.querySelector('.guia-form__btn-adicionar-item')).not.toBeNull();
  });

  it('submit sem itens mostra erro de validacao e nao chama service', () => {
    const { component, fixture, el, guiaService } = setup();

    component.prestadorId.set('p1');
    component.operadoraId.set('o1');
    component.beneficiarioId.set('b1');
    component.numeroGuia.set('12345');
    component.dataAtendimento.set('2024-01-01');
    fixture.detectChanges();

    el.querySelector<HTMLButtonElement>('.guia-form__btn-salvar')?.click();
    fixture.detectChanges();

    expect(el.querySelector('.guia-form__erro-validacao')).not.toBeNull();
    expect(guiaService.criar).not.toHaveBeenCalled();
  });

  it('em modo editar carrega dados da guia no form', () => {
    const { component } = setup({ id: 'guia-1', guia: mockGuia });

    expect(component.modoEditar()).toBe(true);
    expect(component.prestadorId()).toBe('prest-1');
    expect(component.operadoraId()).toBe('op-1');
    expect(component.numeroGuia()).toBe('GUIA01');
    expect(component.dataAtendimento()).toBe('2024-03-15');
    expect(component.localAtendimento()).toBe('Hospital Central');
    expect(component.itens()).toHaveLength(1);
  });

  it('modo criacao nao renderiza app-calculo-detalhe', () => {
    const { el } = setup();

    expect(el.querySelector('app-calculo-detalhe')).toBeNull();
  });

  it('modo edicao chama obterCalculo com id correto e renderiza app-calculo-detalhe', () => {
    const { el, guiaService } = setup({ id: 'guia-1', guia: mockGuia });

    expect(guiaService.obterCalculo).toHaveBeenCalledWith('guia-1');
    expect(el.querySelector('app-calculo-detalhe')).not.toBeNull();
  });

  it('erro em obterCalculo nao trava o form e oculta secao de calculo', () => {
    const guiaService = {
      ...makeGuiaServiceSpy(mockGuia),
      obterCalculo: vi.fn().mockReturnValue(throwError(() => new Error('server error'))),
    };
    const catalogService = makeCatalogServiceSpy();
    const router = makeRouterSpy();
    const activatedRoute = {
      snapshot: { paramMap: { get: (key: string) => (key === 'id' ? 'guia-1' : null) } },
    };

    TestBed.configureTestingModule({
      imports: [GuiaFormComponent],
      providers: [
        { provide: GuiaService, useValue: guiaService },
        { provide: CatalogService, useValue: catalogService },
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: activatedRoute },
      ],
    });
    const fixture = TestBed.createComponent(GuiaFormComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('app-calculo-detalhe')).toBeNull();
    expect(el.querySelector('.guia-form__btn-salvar')).not.toBeNull();
  });

  it('submit com dados validos chama GuiaService criar e navega para guias', () => {
    const { component, fixture, el, guiaService, router } = setup();

    component.prestadorId.set('p1');
    component.operadoraId.set('o1');
    component.beneficiarioId.set('b1');
    component.numeroGuia.set('12345');
    component.dataAtendimento.set('2024-01-01');
    component.itens.set([
      {
        procedimentoId: 'proc-1',
        posicaoExecutor: 'Cirurgiao',
        ordemProcedimento: 'Unico',
        viaAcesso: 'Convencional',
        acomodacao: 'Enfermaria',
        ehUrgencia: false,
        valorApurado: null,
      },
    ]);
    fixture.detectChanges();

    el.querySelector<HTMLButtonElement>('.guia-form__btn-salvar')?.click();

    expect(guiaService.criar).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/admin/guias']);
  });

  it('submit criar inclui localAtendimento no payload', () => {
    const { component, fixture, el, guiaService } = setup();

    component.prestadorId.set('p1');
    component.operadoraId.set('o1');
    component.dataAtendimento.set('2024-01-01');
    component.localAtendimento.set('Clínica Norte');
    component.itens.set([
      {
        procedimentoId: 'proc-1',
        posicaoExecutor: 'Cirurgiao',
        percentualOrdem: 1.0,
        viaAcesso: 'Convencional',
        acomodacao: 'Enfermaria',
        ehUrgencia: false,
        valorApurado: null,
      },
    ]);
    fixture.detectChanges();

    el.querySelector<HTMLButtonElement>('.guia-form__btn-salvar')?.click();

    expect(guiaService.criar).toHaveBeenCalledWith(
      expect.objectContaining({ localAtendimento: 'Clínica Norte' }),
    );
  });

  it('submit editar inclui localAtendimento no payload', () => {
    const { component, fixture, el, guiaService } = setup({ id: 'guia-1', guia: mockGuia });

    component.localAtendimento.set('Hospital Sul');
    component.itens.set([
      {
        procedimentoId: 'proc-1',
        posicaoExecutor: 'Cirurgiao',
        percentualOrdem: 1.0,
        viaAcesso: 'Convencional',
        acomodacao: 'Enfermaria',
        ehUrgencia: false,
        valorApurado: null,
      },
    ]);
    fixture.detectChanges();

    el.querySelector<HTMLButtonElement>('.guia-form__btn-salvar')?.click();

    expect(guiaService.atualizar).toHaveBeenCalledWith(
      'guia-1',
      expect.objectContaining({ localAtendimento: 'Hospital Sul' }),
    );
  });

  it('criar_Erro400ComDeflator_ExibeMensagemDetalhe', () => {
    const { component, fixture, el, guiaService } = setup();

    guiaService.criar.mockReturnValueOnce(
      throwError(
        () =>
          new HttpErrorResponse({
            error: {
              detail:
                "Não é possível criar a guia: 1 item(ns) com situação 'SemDeflator'. Verifique deflators, tabelas de procedimento e portes anestésicos.",
            },
            status: 400,
          }),
      ),
    );

    component.prestadorId.set('p1');
    component.operadoraId.set('o1');
    component.dataAtendimento.set('2024-01-01');
    component.itens.set([
      {
        procedimentoId: 'proc-1',
        posicaoExecutor: 'Cirurgiao',
        percentualOrdem: 1.0,
        viaAcesso: 'Convencional',
        acomodacao: 'Enfermaria',
        ehUrgencia: false,
        valorApurado: null,
      },
    ]);
    fixture.detectChanges();

    el.querySelector<HTMLButtonElement>('.guia-form__btn-salvar')?.click();
    fixture.detectChanges();

    const msgErro = el.querySelector('.guia-form__erro-validacao');
    expect(msgErro?.textContent).toContain('SemDeflator');
  });

  it('salvarPagamentoItem chama atualizarPagamentoItem com valores corretos', () => {
    const { component, guiaService } = setup({ id: 'guia-1', guia: mockGuia });

    component.valoresLiquidadoEmEdicao.update((m) => ({ ...m, 'item-1': '150.50' }));
    component.motivosGlosaEmEdicao.update((m) => ({ ...m, 'item-1': 'CB' }));

    component.salvarPagamentoItem('item-1');

    expect(guiaService.atualizarPagamentoItem).toHaveBeenCalledWith(
      'guia-1',
      'item-1',
      150.5,
      'CB',
    );
  });

  it('salvarPagamentoItem com valor vazio envia null', () => {
    const { component, guiaService } = setup({ id: 'guia-1', guia: mockGuia });

    component.valoresLiquidadoEmEdicao.update((m) => ({ ...m, 'item-1': '' }));
    component.motivosGlosaEmEdicao.update((m) => ({ ...m, 'item-1': '' }));

    component.salvarPagamentoItem('item-1');

    expect(guiaService.atualizarPagamentoItem).toHaveBeenCalledWith('guia-1', 'item-1', null, null);
  });

  it('atualizar_Erro400ComDetalhe_ExibeMensagemDetalhe', () => {
    const guiaService = makeGuiaServiceSpy();
    const catalogService = makeCatalogServiceSpy();
    const router = { navigate: vi.fn() };

    TestBed.configureTestingModule({
      imports: [GuiaFormComponent],
      providers: [
        { provide: GuiaService, useValue: guiaService },
        { provide: CatalogService, useValue: catalogService },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => 'guia-1' } } },
        },
        { provide: Router, useValue: router },
      ],
    });

    const fixture = TestBed.createComponent(GuiaFormComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const el = fixture.nativeElement as HTMLElement;

    guiaService.atualizar.mockReturnValueOnce(
      throwError(
        () =>
          new HttpErrorResponse({
            error: {
              detail:
                "Não é possível criar a guia: 1 item(ns) com situação 'SemDeflator'. Verifique deflators, tabelas de procedimento e portes anestésicos.",
            },
            status: 400,
          }),
      ),
    );

    component.dataAtendimento.set('2024-01-01');
    fixture.detectChanges();

    el.querySelector<HTMLButtonElement>('.guia-form__btn-salvar')?.click();
    fixture.detectChanges();

    const msgErro = el.querySelector('.guia-form__erro-validacao');
    expect(msgErro?.textContent).toContain('SemDeflator');
  });
});
