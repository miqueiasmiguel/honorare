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
  operadoraId: 'op-1',
  operadoraNome: 'UNIMED',
  prestadorId: 'prest-1',
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
    totalItens: 2,
    ...overrides,
  };
}

function makeGuiaItem(overrides: Partial<GuiaItem> = {}): GuiaItem {
  return {
    id: 'guia-2',
    prestadorId: 'prest-1',
    prestadorNome: 'Dr. João',
    operadoraId: 'op-1',
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

function setup(options: { guias?: GuiaNoRecursoDto[]; searchResults?: GuiaItem[] } = {}) {
  const recursoService = {
    obterPorId: vi.fn().mockReturnValue(of(makeDetalhe(options.guias))),
    adicionarGuia: vi.fn().mockReturnValue(of(undefined)),
    removerGuia: vi.fn().mockReturnValue(of(undefined)),
    baixarPdf: vi.fn(),
  };
  const guiaService = {
    listar: vi.fn().mockReturnValue(of(makeListResult(options.searchResults ?? []))),
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
  afterEach(() => {
    vi.useRealTimers();
  });

  it('exibe guias vinculadas ao recurso', () => {
    const guias = [
      makeGuiaNoRecurso({ id: 'g-1', senha: 'S001' }),
      makeGuiaNoRecurso({ id: 'g-2', senha: 'S002' }),
    ];
    const { el } = setup({ guias });

    const rows = el.querySelectorAll('.recurso-guias__guia-row');
    expect(rows).toHaveLength(2);
    expect(rows[0].textContent).toContain('S001');
    expect(rows[1].textContent).toContain('S002');
  });

  it('botão remover chama service e atualiza lista', () => {
    const { el, component, recursoService } = setup({
      guias: [makeGuiaNoRecurso({ id: 'guia-1' })],
    });

    el.querySelector<HTMLButtonElement>('.recurso-guias__btn-remover')?.click();

    expect(recursoService.removerGuia).toHaveBeenCalledWith('rec-1', 'guia-1');
    expect(component.guias()).toHaveLength(0);
  });

  it('busca por senha retorna guias disponíveis', () => {
    vi.useFakeTimers();
    const searchResults = [makeGuiaItem({ id: 'guia-2', senha: 'S002' })];
    const { component, fixture, guiaService, el } = setup({ searchResults });

    component.onBuscaChange('S002');
    vi.advanceTimersByTime(400);
    fixture.detectChanges();

    expect(guiaService.listar).toHaveBeenCalledWith(expect.objectContaining({ senha: 'S002' }));
    const resultados = el.querySelectorAll('.recurso-guias__resultado');
    expect(resultados).toHaveLength(1);
  });

  it('botão adicionar chama service e adiciona à lista', () => {
    const { component, fixture, recursoService, el } = setup();

    component.guiasBusca.set([makeGuiaItem({ id: 'guia-2' })]);
    fixture.detectChanges();

    el.querySelector<HTMLButtonElement>('.recurso-guias__btn-adicionar')?.click();

    expect(recursoService.adicionarGuia).toHaveBeenCalledWith('rec-1', 'guia-2');
  });

  it('botão PDF chama baixarPdf do service', () => {
    const { el, recursoService } = setup();

    el.querySelector<HTMLButtonElement>('.recurso-guias__btn-pdf')?.click();

    expect(recursoService.baixarPdf).toHaveBeenCalledWith('rec-1');
  });

  it('erro ao adicionar exibe mensagem inline', () => {
    const { component, fixture, recursoService, el } = setup();

    recursoService.adicionarGuia.mockReturnValue(throwError(() => new Error('err')));
    component.guiasBusca.set([makeGuiaItem({ id: 'guia-2' })]);
    fixture.detectChanges();

    el.querySelector<HTMLButtonElement>('.recurso-guias__btn-adicionar')?.click();
    fixture.detectChanges();

    const erro = el.querySelector('.recurso-guias__erro');
    expect(erro).not.toBeNull();
    expect(erro?.textContent).toContain('Erro ao adicionar');
  });

  it('guia já EmRecurso não aparece nos resultados de busca', () => {
    vi.useFakeTimers();
    const searchResults = [makeGuiaItem({ id: 'guia-em-recurso', situacao: 'EmRecurso' })];
    const { component, fixture, el } = setup({ searchResults });

    component.onBuscaChange('S001');
    vi.advanceTimersByTime(400);
    fixture.detectChanges();

    const resultados = el.querySelectorAll('.recurso-guias__resultado');
    expect(resultados).toHaveLength(0);
  });
});
