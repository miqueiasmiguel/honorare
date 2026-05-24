import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { TabelaListComponent } from './tabela-list.component';
import { CatalogService } from '../../catalog.service';
import type { ListarTabelasResult, OperadoraItem, TabelaItem } from '../../catalog.types';

const mockOperadoras: OperadoraItem[] = [
  {
    id: 'op-1',
    nome: 'UNIMED João Pessoa',
    registroAns: '012345',
    cnpj: '12345678000195',
    tipoRuleSet: 'Unimed',
    ativa: true,
    criadaEm: '2026-01-01T00:00:00Z',
  },
];

const mockTabelas: TabelaItem[] = [
  {
    id: 'tab-1',
    operadoraId: 'op-1',
    procedimentoId: 'proc-1',
    codigoTuss: '30715013',
    descricao: 'Herniorrafia inguinal',
    valor: 150.5,
    atualizadoEm: '2026-01-01T00:00:00Z',
  },
];

function makeResult(itens: TabelaItem[] = mockTabelas): ListarTabelasResult {
  return { itens, total: itens.length, pagina: 1, itensPorPagina: 20 };
}

function setup() {
  const catalogServiceSpy = {
    listarOperadoras: vi
      .fn()
      .mockReturnValue(of({ itens: mockOperadoras, total: 1, pagina: 1, itensPorPagina: 200 })),
    listarTabelas: vi.fn().mockReturnValue(of(makeResult())),
    obterTabela: vi.fn().mockReturnValue(of(mockTabelas[0])),
    criarTabela: vi.fn().mockReturnValue(of(mockTabelas[0])),
    atualizarTabela: vi.fn().mockReturnValue(of(mockTabelas[0])),
    excluirTabela: vi.fn().mockReturnValue(of(undefined)),
    listarProcedimentos: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 20 })),
    importarTabelaCsv: vi
      .fn()
      .mockReturnValue(of({ inseridos: 1, atualizados: 0, ignorados: 0, erros: [] })),
    importarTabelaPorteAnestesico: vi
      .fn()
      .mockReturnValue(
        of({
          portesAtualizados: 0,
          procedimentosAtualizados: 0,
          procedimentosNaoEncontrados: [],
          erros: [],
        }),
      ),
  };

  TestBed.configureTestingModule({
    imports: [TabelaListComponent],
    providers: [{ provide: CatalogService, useValue: catalogServiceSpy }],
  });

  const fixture = TestBed.createComponent(TabelaListComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    catalogService: catalogServiceSpy,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('TabelaListComponent', () => {
  it('exibe seletor de operadora antes de mostrar tabela', () => {
    const { el } = setup();
    const select = el.querySelector('.tabela-list__select-operadora');
    expect(select).toBeTruthy();
    const table = el.querySelector('.tabela-list__table');
    expect(table).toBeNull();
  });

  it('selecionar operadora dispara GET /tabelas?operadoraId=...', () => {
    const { component, catalogService, fixture } = setup();
    catalogService.listarTabelas.mockClear();

    component.onOperadoraChange('op-1');
    fixture.detectChanges();

    expect(catalogService.listarTabelas).toHaveBeenCalledWith(
      expect.objectContaining({ operadoraId: 'op-1' }),
    );
  });

  it('exibe entradas retornadas com CodigoTuss, Descrição e Valor formatado', () => {
    const { component, fixture, el } = setup();
    component.onOperadoraChange('op-1');
    fixture.detectChanges();

    const rows = el.querySelectorAll('.tabela-list__row');
    expect(rows).toHaveLength(1);
    const cells = rows[0].querySelectorAll('.tabela-list__cell');
    expect(cells[0].textContent.trim()).toBe('30715013');
    expect(cells[1].textContent.trim()).toBe('Herniorrafia inguinal');
    expect(cells[2].textContent.trim()).toContain('150');
  });

  it('clicar "Importar CSV" abre modal', () => {
    const { component, fixture, el } = setup();
    component.onOperadoraChange('op-1');
    fixture.detectChanges();

    const btns = Array.from(el.querySelectorAll('button'));
    const csvBtn = btns.find((b) => b.textContent.trim() === 'Importar CSV');
    csvBtn?.click();
    fixture.detectChanges();

    const modal = el.querySelector('app-tabela-csv-modal');
    expect(modal).toBeTruthy();
  });

  it('botão "Importar Tabela Anestesista" está desabilitado sem operadora selecionada', () => {
    const { el } = setup();
    const btns = Array.from(el.querySelectorAll('button'));
    const porteBtn = btns.find((b) => b.textContent.trim() === 'Importar Tabela Anestesista');
    expect(porteBtn).toBeTruthy();
    expect(porteBtn?.disabled).toBe(true);
  });

  it('clicar "Importar Tabela Anestesista" abre modal de porte anestésico', () => {
    const { component, fixture, el } = setup();
    component.onOperadoraChange('op-1');
    fixture.detectChanges();

    const btns = Array.from(el.querySelectorAll('button'));
    const porteBtn = btns.find((b) => b.textContent.trim() === 'Importar Tabela Anestesista');
    porteBtn?.click();
    fixture.detectChanges();

    const modal = el.querySelector('app-tabela-porte-anestesico-csv-modal');
    expect(modal).toBeTruthy();
  });

  it('clicar "Nova entrada" abre modal de formulário', () => {
    const { component, fixture, el } = setup();
    component.onOperadoraChange('op-1');
    fixture.detectChanges();

    const btns = Array.from(el.querySelectorAll('button'));
    const novoBtn = btns.find((b) => b.textContent.trim() === 'Nova entrada');
    novoBtn?.click();
    fixture.detectChanges();

    const form = el.querySelector('app-tabela-form');
    expect(form).toBeTruthy();
  });

  it('clicar "Editar" abre form preenchido', () => {
    const { component, fixture, el } = setup();
    component.onOperadoraChange('op-1');
    fixture.detectChanges();

    const btns = Array.from(el.querySelectorAll('button'));
    const editarBtn = btns.find((b) => b.textContent.trim() === 'Editar');
    editarBtn?.click();
    fixture.detectChanges();

    expect(component.editandoTabelaId()).toBe('tab-1');
    const form = el.querySelector('app-tabela-form');
    expect(form).toBeTruthy();
  });

  it('clicar "Excluir" e confirmar chama DELETE', () => {
    const { component, catalogService, fixture, el } = setup();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    component.onOperadoraChange('op-1');
    fixture.detectChanges();
    catalogService.listarTabelas.mockClear();

    const btns = Array.from(el.querySelectorAll('button'));
    const excluirBtn = btns.find((b) => b.textContent.trim() === 'Excluir');
    excluirBtn?.click();
    fixture.detectChanges();

    expect(catalogService.excluirTabela).toHaveBeenCalledWith('tab-1');
    expect(catalogService.listarTabelas).toHaveBeenCalledOnce();
  });
});
