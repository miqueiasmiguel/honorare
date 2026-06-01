import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { RecursoService } from '../recurso.service';
import { GuiaService } from '../guia.service';
import type { GuiaItem, ListarGuiasResult } from '../guia.types';
import type {
  GuiaNoRecursoDto,
  ItemGuiaNoRecursoDto,
  RecursoDetalheDto,
  RecursoDto,
} from '../recurso.types';
import { RecursoGuiasComponent } from './recurso-guias.component';

const RECURSO: RecursoDto = {
  id: 'rec-1',
  operadoraId: 'op1',
  operadoraNome: 'UNIMED',
  prestadorId: 'p1',
  prestadorNome: 'Dr. João',
  prestadorRegistroProfissional: null,
  numero: '202601',
  dataEmissao: '2026-01-15',
  observacao: null,
  totalGuias: 1,
  criadoEm: '2026-01-15T00:00:00Z',
};

function makeItemGuiaNoRecurso(
  overrides: Partial<ItemGuiaNoRecursoDto> = {},
): ItemGuiaNoRecursoDto {
  return {
    id: 'item-1',
    codigoTuss: '31303079',
    descricaoProcedimento: 'Consulta médica',
    posicaoExecutor: 'Cirurgiao',
    percentualOrdem: 100,
    viaAcesso: 'Convencional',
    acomodacao: 'Ambulatorial',
    ehUrgencia: false,
    valorApurado: null,
    valorLiquidado: null,
    ...overrides,
  };
}

function makeGuiaNoRecurso(overrides: Partial<GuiaNoRecursoDto> = {}): GuiaNoRecursoDto {
  return {
    id: 'guia-1',
    numeroGuia: 'S001',
    dataAtendimento: '2026-01-10',
    beneficiarioNome: 'Paciente A',
    beneficiarioCarteira: '123',
    situacao: 'EmRecurso',
    observacao: null,
    itens: [],
    ...overrides,
  };
}

function makeGuiaItem(overrides: Partial<GuiaItem> = {}): GuiaItem {
  return {
    id: 'guia-2',
    prestadorId: 'p1',
    prestadorNome: 'Dr. João',
    operadoraId: 'op1',
    operadoraNome: 'UNIMED',
    beneficiarioId: null,
    beneficiarioNome: 'Paciente B',
    beneficiarioCarteira: '456',
    numeroGuia: 'S002',
    dataAtendimento: '2026-01-11',
    situacao: 'Apresentada',
    ehPacote: false,
    observacao: '',
    totalItens: 1,
    criadoEm: '2026-01-11T00:00:00Z',
    atualizadoEm: '2026-01-11T00:00:00Z',
    ...overrides,
  };
}

function makeDetalhe(guias: GuiaNoRecursoDto[] = [makeGuiaNoRecurso()]): RecursoDetalheDto {
  return { header: RECURSO, guias };
}

function makeListResult(itens: GuiaItem[] = []): ListarGuiasResult {
  return { itens, total: itens.length, pagina: 1, itensPorPagina: 20 };
}

function setup(options: { guias?: GuiaNoRecursoDto[]; candidatas?: GuiaItem[] } = {}) {
  const recursoService = {
    obterPorId: vi.fn().mockReturnValue(of(makeDetalhe(options.guias))),
    adicionarGuia: vi.fn().mockReturnValue(of(undefined)),
    adicionarGuiasLote: vi.fn().mockReturnValue(of({ adicionadas: 0 })),
    removerGuia: vi.fn().mockReturnValue(of(undefined)),
    baixarPdf: vi.fn(),
  };
  const guiaService = {
    listar: vi.fn().mockReturnValue(of(makeListResult(options.candidatas ?? []))),
    atualizarObservacao: vi.fn().mockReturnValue(of({})),
    atualizarValorApuradoItem: vi.fn().mockReturnValue(of({})),
  };
  const activatedRoute = {
    snapshot: {
      paramMap: { get: (key: string) => (key === 'id' ? 'rec-1' : null) },
    },
  };

  TestBed.configureTestingModule({
    imports: [RecursoGuiasComponent],
    providers: [
      { provide: RecursoService, useValue: recursoService },
      { provide: GuiaService, useValue: guiaService },
      { provide: ActivatedRoute, useValue: activatedRoute },
    ],
  });

  const fixture = TestBed.createComponent(RecursoGuiasComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    recursoService,
    guiaService,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('RecursoGuiasComponent', () => {
  it('exibeGuiasVinculadas', () => {
    const guias = [
      makeGuiaNoRecurso({ id: 'g-1', numeroGuia: 'S001' }),
      makeGuiaNoRecurso({ id: 'g-2', numeroGuia: 'S002' }),
    ];
    const { el } = setup({ guias });
    expect(el.querySelectorAll('.guia-card')).toHaveLength(2);
  });

  it('botaoRemoverChamaServiceEAtualizaLista', () => {
    const { el, component, recursoService } = setup({
      guias: [makeGuiaNoRecurso({ id: 'guia-1' })],
    });
    el.querySelector<HTMLButtonElement>('.guia-card__remover')?.click();
    expect(recursoService.removerGuia).toHaveBeenCalledWith('rec-1', 'guia-1');
    expect(component.guias()).toHaveLength(0);
  });

  it('filtrarChamaListarComPrestadorEOperadoraDoRecurso', () => {
    const { el, guiaService } = setup();
    el.querySelector<HTMLButtonElement>('.recurso-guias__btn-filtrar')?.click();
    expect(guiaService.listar).toHaveBeenCalledWith(
      expect.objectContaining({ prestadorId: 'p1', operadoraId: 'op1', semRecurso: true }),
    );
  });

  it('filtrosSaoPassadosParaListar', () => {
    const { el, component, guiaService } = setup();
    component.filtroNumeroGuia.set('ABC');
    component.filtroSomenteGlosa.set(true);
    el.querySelector<HTMLButtonElement>('.recurso-guias__btn-filtrar')?.click();
    expect(guiaService.listar).toHaveBeenCalledWith(
      expect.objectContaining({ numeroGuia: 'ABC', somenteComGlosa: true }),
    );
  });

  it('tabelaCandidataExibeResultados', () => {
    const candidatas = [
      makeGuiaItem({ id: 'g-1' }),
      makeGuiaItem({ id: 'g-2' }),
      makeGuiaItem({ id: 'g-3' }),
    ];
    const { el, fixture } = setup({ candidatas });
    el.querySelector<HTMLButtonElement>('.recurso-guias__btn-filtrar')?.click();
    fixture.detectChanges();
    expect(el.querySelectorAll('.recurso-guias__linha-candidata')).toHaveLength(3);
  });

  it('estadoInicialNaoCarregaCandidatas', () => {
    const { guiaService, el } = setup();
    expect(guiaService.listar).not.toHaveBeenCalled();
    expect(el.querySelector('.recurso-guias__hint')).not.toBeNull();
  });

  it('adicionarUmaGuiaChamaServiceCorretamente', () => {
    const { component, fixture, recursoService, el } = setup();
    component.filtroAplicado.set(true);
    component.candidatas.set([makeGuiaItem({ id: 'guia-x' })]);
    fixture.detectChanges();
    el.querySelector<HTMLButtonElement>('.recurso-guias__btn-adicionar')?.click();
    expect(recursoService.adicionarGuia).toHaveBeenCalledWith('rec-1', 'guia-x');
  });

  it('adicionarTodasChamaLoteComFiltrosAtuais', () => {
    const candidatas = Array.from({ length: 5 }, (_, i) => makeGuiaItem({ id: `g-${String(i)}` }));
    const { el, component, fixture, recursoService } = setup({ candidatas });
    component.filtroDataInicio.set('2026-03-01');
    component.filtroDataFim.set('2026-03-31');
    el.querySelector<HTMLButtonElement>('.recurso-guias__btn-filtrar')?.click();
    fixture.detectChanges();
    el.querySelector<HTMLButtonElement>('.recurso-guias__btn-adicionar-todas')?.click();
    expect(recursoService.adicionarGuiasLote).toHaveBeenCalledWith(
      'rec-1',
      expect.objectContaining({
        prestadorId: 'p1',
        operadoraId: 'op1',
        dataInicio: '2026-03-01',
        dataFim: '2026-03-31',
      }),
    );
  });

  it('erroRemoverExibeMensagem', () => {
    const { el, fixture, recursoService } = setup({
      guias: [makeGuiaNoRecurso({ id: 'guia-1' })],
    });
    recursoService.removerGuia.mockReturnValue(throwError(() => new Error('err')));
    el.querySelector<HTMLButtonElement>('.guia-card__remover')?.click();
    fixture.detectChanges();
    expect(el.querySelector('.recurso-guias__erro')).not.toBeNull();
  });

  it('botaoPdfChamaBaixarPdf', () => {
    const { el, recursoService } = setup();
    el.querySelector<HTMLButtonElement>('.recurso-guias__btn-pdf')?.click();
    expect(recursoService.baixarPdf).toHaveBeenCalledWith('rec-1');
  });

  it('deve_exibir_guias_com_numero_de_itens', () => {
    const item = makeItemGuiaNoRecurso();
    const guia = makeGuiaNoRecurso({ itens: [item] });
    const { el } = setup({ guias: [guia] });
    expect(el.querySelector('.guia-card__itens')?.textContent).toContain('1');
  });

  it('deve_expandir_guia_ao_clicar', () => {
    const guia = makeGuiaNoRecurso({ id: 'g-1' });
    const { el, fixture } = setup({ guias: [guia] });
    expect(el.querySelector('.guia-card__detalhe')).toBeNull();
    el.querySelector<HTMLElement>('.guia-card__header')?.click();
    fixture.detectChanges();
    expect(el.querySelector('.guia-card__detalhe')).not.toBeNull();
  });

  it('deve_fechar_guia_expandida_ao_clicar_novamente', () => {
    const guia = makeGuiaNoRecurso({ id: 'g-1' });
    const { el, fixture } = setup({ guias: [guia] });
    const header = el.querySelector<HTMLElement>('.guia-card__header');
    header?.click();
    fixture.detectChanges();
    expect(el.querySelector('.guia-card__detalhe')).not.toBeNull();
    header?.click();
    fixture.detectChanges();
    expect(el.querySelector('.guia-card__detalhe')).toBeNull();
  });

  it('deve_salvar_observacao_ao_clicar_botao', () => {
    const guia = makeGuiaNoRecurso({ id: 'guia-1' });
    const { el, fixture, component, guiaService } = setup({ guias: [guia] });
    el.querySelector<HTMLElement>('.guia-card__header')?.click();
    fixture.detectChanges();
    component.observacoesEmEdicao.update((m) => ({ ...m, 'guia-1': 'nova obs' }));
    el.querySelector<HTMLButtonElement>('.guia-card__obs-salvar')?.click();
    expect(guiaService.atualizarObservacao).toHaveBeenCalledWith('guia-1', 'nova obs');
  });

  it('deve_salvar_valor_apurado_ao_sair_do_campo_blur', () => {
    const item = makeItemGuiaNoRecurso({ id: 'item-1' });
    const guia = makeGuiaNoRecurso({ id: 'guia-1', itens: [item] });
    const { el, fixture, component, guiaService } = setup({ guias: [guia] });
    el.querySelector<HTMLElement>('.guia-card__header')?.click();
    fixture.detectChanges();
    component.valoresEmEdicao.update((m) => ({ ...m, 'item-1': '100.50' }));
    const input = el.querySelector<HTMLInputElement>('.guia-card__valor-input');
    input?.dispatchEvent(new Event('blur'));
    expect(guiaService.atualizarValorApuradoItem).toHaveBeenCalledWith('guia-1', 'item-1', 100.5);
  });

  it('deve_atualizar_lista_local_apos_salvar_observacao', () => {
    const guia = makeGuiaNoRecurso({ id: 'guia-1', observacao: null });
    const { component, el, fixture } = setup({ guias: [guia] });
    el.querySelector<HTMLElement>('.guia-card__header')?.click();
    fixture.detectChanges();
    component.observacoesEmEdicao.update((m) => ({ ...m, 'guia-1': 'texto salvo' }));
    el.querySelector<HTMLButtonElement>('.guia-card__obs-salvar')?.click();
    expect(component.guias()[0].observacao).toBe('texto salvo');
  });

  it('deve_atualizar_lista_local_apos_salvar_valor_apurado', () => {
    const item = makeItemGuiaNoRecurso({ id: 'item-1', valorApurado: null });
    const guia = makeGuiaNoRecurso({ id: 'guia-1', itens: [item] });
    const { component, el, fixture } = setup({ guias: [guia] });
    el.querySelector<HTMLElement>('.guia-card__header')?.click();
    fixture.detectChanges();
    component.valoresEmEdicao.update((m) => ({ ...m, 'item-1': '200' }));
    const input = el.querySelector<HTMLInputElement>('.guia-card__valor-input');
    input?.dispatchEvent(new Event('blur'));
    expect(component.guias()[0].itens[0].valorApurado).toBe(200);
  });

  it('deve_exibir_glosa_quando_valor_apurado_maior_que_liquidado', () => {
    const item = makeItemGuiaNoRecurso({ id: 'item-1', valorApurado: 300, valorLiquidado: 200 });
    const guia = makeGuiaNoRecurso({ id: 'guia-1', itens: [item] });
    const { el, fixture } = setup({ guias: [guia] });
    el.querySelector<HTMLElement>('.guia-card__header')?.click();
    fixture.detectChanges();
    expect(el.querySelector('.guia-card__glosa')).not.toBeNull();
  });

  it('deve_exibir_traco_em_glosa_quando_faltam_valores', () => {
    const item = makeItemGuiaNoRecurso({ id: 'item-1', valorApurado: null, valorLiquidado: 100 });
    const guia = makeGuiaNoRecurso({ id: 'guia-1', itens: [item] });
    const { el, fixture } = setup({ guias: [guia] });
    el.querySelector<HTMLElement>('.guia-card__header')?.click();
    fixture.detectChanges();
    expect(el.querySelector('.guia-card__glosa')).toBeNull();
  });
});
