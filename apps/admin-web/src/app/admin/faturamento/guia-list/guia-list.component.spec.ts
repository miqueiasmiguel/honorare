import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { CatalogService } from '../../catalog/catalog.service';
import { GuiaService } from '../guia.service';
import type { GuiaItem, ListarGuiasResult } from '../guia.types';
import { GuiaListComponent } from './guia-list.component';

function makeGuia(overrides: Partial<GuiaItem> = {}): GuiaItem {
  return {
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
    totalItens: 1,
    criadoEm: '2024-03-15T10:00:00Z',
    atualizadoEm: '2024-03-15T10:00:00Z',
    ...overrides,
  };
}

function makeResult(itens: GuiaItem[], total = itens.length): ListarGuiasResult {
  return { itens, total, pagina: 1, itensPorPagina: 20 };
}

function setup(guias: GuiaItem[] = [makeGuia()], total?: number) {
  const guiaService = {
    listar: vi.fn().mockReturnValue(of(makeResult(guias, total ?? guias.length))),
    excluir: vi.fn().mockReturnValue(of(undefined)),
    importarCsv: vi.fn(),
  };
  const catalogService = {
    listarPrestadores: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 500 })),
    listarOperadoras: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 500 })),
  };
  const router = { navigate: vi.fn().mockReturnValue(Promise.resolve(true)) };

  TestBed.configureTestingModule({
    imports: [GuiaListComponent],
    providers: [
      { provide: GuiaService, useValue: guiaService },
      { provide: CatalogService, useValue: catalogService },
      { provide: Router, useValue: router },
    ],
  });

  const fixture = TestBed.createComponent(GuiaListComponent);
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

describe('GuiaListComponent', () => {
  it('renderiza tabela com guias', () => {
    const guias = [makeGuia({ id: 'g-1' }), makeGuia({ id: 'g-2' })];
    const { el } = setup(guias);
    const rows = el.querySelectorAll('.guia-list__row');
    expect(rows).toHaveLength(2);
  });

  it('linha Apresentada tem classe CSS correta', () => {
    const { el } = setup([makeGuia({ situacao: 'Apresentada' })]);
    const row = el.querySelector('.guia-list__row');
    expect(row?.classList.contains('guia-list__row--apresentada')).toBe(true);
  });

  it('linha Liquidada tem classe CSS correta', () => {
    const { el } = setup([makeGuia({ situacao: 'Liquidada' })]);
    const row = el.querySelector('.guia-list__row');
    expect(row?.classList.contains('guia-list__row--liquidada')).toBe(true);
  });

  it('linha EmRecurso tem classe CSS correta', () => {
    const { el } = setup([makeGuia({ situacao: 'EmRecurso' })]);
    const row = el.querySelector('.guia-list__row');
    expect(row?.classList.contains('guia-list__row--em-recurso')).toBe(true);
  });

  it('clicar Nova Guia navega para /admin/guias/nova', () => {
    const { el, router } = setup();
    el.querySelector<HTMLButtonElement>('.guia-list__btn-nova')?.click();
    expect(router.navigate).toHaveBeenCalledWith(['/admin/guias/nova']);
  });

  it('formata data no formato dd/MM/yyyy', () => {
    const { component } = setup();
    expect(component.formatarData('2024-03-15')).toBe('15/03/2024');
  });

  it('filtro por situacao recarrega lista com parametro correto', () => {
    const { component, guiaService } = setup();
    guiaService.listar.mockClear();

    component.onFiltroSituacaoChange('Liquidada');

    expect(guiaService.listar).toHaveBeenCalledWith(
      expect.objectContaining({ situacao: 'Liquidada' }),
    );
  });

  it('filtro por prestador recarrega lista com parâmetro correto', () => {
    const { component, guiaService } = setup();
    guiaService.listar.mockClear();

    component.onFiltroPrestadorChange('prest-99');

    expect(guiaService.listar).toHaveBeenCalledWith(
      expect.objectContaining({ prestadorId: 'prest-99' }),
    );
  });

  it('filtro por operadora recarrega lista com parâmetro correto', () => {
    const { component, guiaService } = setup();
    guiaService.listar.mockClear();

    component.onFiltroOperadoraChange('op-99');

    expect(guiaService.listar).toHaveBeenCalledWith(
      expect.objectContaining({ operadoraId: 'op-99' }),
    );
  });

  it('filtro semRecurso recarrega lista com parâmetro correto', () => {
    const { component, guiaService } = setup();
    guiaService.listar.mockClear();

    component.onFiltroSemRecursoChange(true);

    expect(guiaService.listar).toHaveBeenCalledWith(expect.objectContaining({ semRecurso: true }));
  });

  it('filtro somenteComGlosa recarrega lista com parâmetro correto', () => {
    const { component, guiaService } = setup();
    guiaService.listar.mockClear();

    component.onFiltroSomenteComGlosaChange(true);

    expect(guiaService.listar).toHaveBeenCalledWith(
      expect.objectContaining({ somenteComGlosa: true }),
    );
  });

  it('limparFiltros reseta todos os filtros e recarrega', () => {
    const { component, guiaService } = setup();
    component.filtroSituacao.set('Liquidada');
    component.filtroPrestadorId.set('prest-1');
    component.filtroOperadoraId.set('op-1');
    component.filtroNumeroGuia.set('SENHA01');
    component.filtroBeneficiario.set('Maria');
    component.filtroSemRecurso.set(true);
    component.filtroSomenteComGlosa.set(true);
    component.pagina.set(3);
    guiaService.listar.mockClear();

    component.limparFiltros();

    expect(component.filtroSituacao()).toBe('');
    expect(component.filtroPrestadorId()).toBe('');
    expect(component.filtroOperadoraId()).toBe('');
    expect(component.filtroNumeroGuia()).toBe('');
    expect(component.filtroBeneficiario()).toBe('');
    expect(component.filtroSemRecurso()).toBe(false);
    expect(component.filtroSomenteComGlosa()).toBe(false);
    expect(component.pagina()).toBe(1);
    expect(guiaService.listar).toHaveBeenCalledWith(
      expect.objectContaining({ pagina: 1, situacao: undefined }),
    );
  });

  it('não renderiza paginação quando total é igual ao número de itens', () => {
    const { el } = setup([makeGuia()]);
    expect(el.querySelector('.guia-list__pagination')).toBeNull();
  });

  it('renderiza controles de paginação quando total excede itensPorPagina', () => {
    const { el } = setup([makeGuia()], 50);
    expect(el.querySelector('.guia-list__pagination')).toBeTruthy();
  });

  it('totalPaginas calcula corretamente', () => {
    const { component } = setup([makeGuia()], 50);
    expect(component.totalPaginas()).toBe(3);
  });

  it('proximaPagina avança para página 2 e recarrega', () => {
    const { component, guiaService } = setup([makeGuia()], 50);
    guiaService.listar.mockClear();

    component.proximaPagina();

    expect(component.pagina()).toBe(2);
    expect(guiaService.listar).toHaveBeenCalledWith(expect.objectContaining({ pagina: 2 }));
  });

  it('paginaAnterior volta para página 1 a partir da página 2', () => {
    const { component, guiaService } = setup([makeGuia()], 50);
    component.pagina.set(2);
    guiaService.listar.mockClear();

    component.paginaAnterior();

    expect(component.pagina()).toBe(1);
    expect(guiaService.listar).toHaveBeenCalledWith(expect.objectContaining({ pagina: 1 }));
  });

  it('paginaAnterior não faz nada quando já está na página 1', () => {
    const { component, guiaService } = setup([makeGuia()], 50);
    guiaService.listar.mockClear();

    component.paginaAnterior();

    expect(component.pagina()).toBe(1);
    expect(guiaService.listar).not.toHaveBeenCalled();
  });

  it('proximaPagina não avança além da última página', () => {
    const { component, guiaService } = setup([makeGuia()], 50);
    component.pagina.set(3);
    guiaService.listar.mockClear();

    component.proximaPagina();

    expect(component.pagina()).toBe(3);
    expect(guiaService.listar).not.toHaveBeenCalled();
  });

  it('clicar Editar navega para a guia correta', () => {
    const { el, router } = setup([makeGuia({ id: 'guia-42' })]);
    el.querySelector<HTMLButtonElement>('.guia-list__btn-editar')?.click();
    expect(router.navigate).toHaveBeenCalledWith(['/admin/guias', 'guia-42']);
  });

  it('excluir após confirmação chama service e recarrega lista', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { component, guiaService } = setup();
    guiaService.listar.mockClear();

    component.excluir(makeGuia());

    expect(guiaService.excluir).toHaveBeenCalledWith('guia-1');
    expect(guiaService.listar).toHaveBeenCalled();
  });

  it('excluir sem confirmação não chama service', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    const { component, guiaService } = setup();

    component.excluir(makeGuia());

    expect(guiaService.excluir).not.toHaveBeenCalled();
  });

  it('onFiltroDataInicioChange recarrega lista com parâmetro correto', () => {
    const { component, guiaService } = setup();
    guiaService.listar.mockClear();

    component.onFiltroDataInicioChange('2024-01-01');

    expect(guiaService.listar).toHaveBeenCalledWith(
      expect.objectContaining({ dataInicio: '2024-01-01' }),
    );
  });

  it('onFiltroDataFimChange recarrega lista com parâmetro correto', () => {
    const { component, guiaService } = setup();
    guiaService.listar.mockClear();

    component.onFiltroDataFimChange('2024-12-31');

    expect(guiaService.listar).toHaveBeenCalledWith(
      expect.objectContaining({ dataFim: '2024-12-31' }),
    );
  });

  it('onFiltroNumeroGuiaChange atualiza o signal filtroNumeroGuia', () => {
    const { component } = setup();
    component.onFiltroNumeroGuiaChange('SENHA123');
    expect(component.filtroNumeroGuia()).toBe('SENHA123');
  });

  it('onFiltroBeneficiarioChange atualiza o signal filtroBeneficiario', () => {
    const { component } = setup();
    component.onFiltroBeneficiarioChange('Carlos');
    expect(component.filtroBeneficiario()).toBe('Carlos');
  });

  it('ordenar por nova coluna define ordenarPor, reseta página e usa direção padrão asc', () => {
    const { component, guiaService } = setup([makeGuia()], 50);
    component.pagina.set(3);
    guiaService.listar.mockClear();

    component.ordenar('prestadorNome');

    expect(component.ordenarPor()).toBe('prestadorNome');
    expect(component.descendente()).toBe(false);
    expect(component.pagina()).toBe(1);
    expect(guiaService.listar).toHaveBeenCalledWith(
      expect.objectContaining({ ordenarPor: 'prestadorNome', descendente: false, pagina: 1 }),
    );
  });

  it('ordenar pela mesma coluna alterna a direção', () => {
    const { component } = setup();
    expect(component.ordenarPor()).toBe('dataAtendimento');
    expect(component.descendente()).toBe(true);

    component.ordenar('dataAtendimento');
    expect(component.descendente()).toBe(false);

    component.ordenar('dataAtendimento');
    expect(component.descendente()).toBe(true);
  });

  it('iconeOrdenacao retorna seta apenas para a coluna ativa', () => {
    const { component } = setup();
    // padrão: dataAtendimento descendente
    expect(component.iconeOrdenacao('dataAtendimento')).toBe('↓');
    expect(component.iconeOrdenacao('numeroGuia')).toBe('');

    component.ordenar('numeroGuia');
    expect(component.iconeOrdenacao('numeroGuia')).toBe('↑');
    expect(component.iconeOrdenacao('dataAtendimento')).toBe('');
  });

  it('clicar Importar CSV abre o modal', () => {
    const { el, component } = setup();

    el.querySelector<HTMLButtonElement>('.guia-list__btn-importar-csv')?.click();

    expect(component.mostrarImportarCsvModal()).toBe(true);
  });

  it('onImportCsvConcluido fecha o modal e recarrega a lista', () => {
    const { component, guiaService } = setup();
    component.mostrarImportarCsvModal.set(true);
    guiaService.listar.mockClear();

    component.onImportCsvConcluido();

    expect(component.mostrarImportarCsvModal()).toBe(false);
    expect(guiaService.listar).toHaveBeenCalled();
  });
});
