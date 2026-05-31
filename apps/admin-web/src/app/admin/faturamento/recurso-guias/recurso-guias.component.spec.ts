import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { RecursoService } from '../recurso.service';
import { GuiaService } from '../guia.service';
import type { GuiaItem, ListarGuiasResult } from '../guia.types';
import type { GuiaNoRecursoDto, RecursoDetalheDto, RecursoDto } from '../recurso.types';
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

function makeGuiaNoRecurso(overrides: Partial<GuiaNoRecursoDto> = {}): GuiaNoRecursoDto {
  return {
    id: 'guia-1',
    senha: 'S001',
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
    senha: 'S002',
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
      makeGuiaNoRecurso({ id: 'g-1', senha: 'S001' }),
      makeGuiaNoRecurso({ id: 'g-2', senha: 'S002' }),
    ];
    const { el } = setup({ guias });
    expect(el.querySelectorAll('.recurso-guias__linha-guia')).toHaveLength(2);
  });

  it('botaoRemoverChamaServiceEAtualizaLista', () => {
    const { el, component, recursoService } = setup({
      guias: [makeGuiaNoRecurso({ id: 'guia-1' })],
    });
    el.querySelector<HTMLButtonElement>('.recurso-guias__btn-remover')?.click();
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
    component.filtroSenha.set('ABC');
    component.filtroSomenteGlosa.set(true);
    el.querySelector<HTMLButtonElement>('.recurso-guias__btn-filtrar')?.click();
    expect(guiaService.listar).toHaveBeenCalledWith(
      expect.objectContaining({ senha: 'ABC', somenteComGlosa: true }),
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
    el.querySelector<HTMLButtonElement>('.recurso-guias__btn-remover')?.click();
    fixture.detectChanges();
    expect(el.querySelector('.recurso-guias__erro')).not.toBeNull();
  });

  it('botaoPdfChamaBaixarPdf', () => {
    const { el, recursoService } = setup();
    el.querySelector<HTMLButtonElement>('.recurso-guias__btn-pdf')?.click();
    expect(recursoService.baixarPdf).toHaveBeenCalledWith('rec-1');
  });
});
