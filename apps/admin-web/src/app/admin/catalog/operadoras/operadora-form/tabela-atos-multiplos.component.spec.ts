import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { TabelaAtosMultiplosComponent } from './tabela-atos-multiplos.component';
import { CatalogService } from '../../catalog.service';
import type { TabelaOrdemOperadoraItem } from '../../catalog.types';

function makeServiceSpy(items: TabelaOrdemOperadoraItem[] = []) {
  return {
    listarTabelaOrdem: vi.fn().mockReturnValue(of(items)),
    salvarTabelaOrdem: vi.fn().mockReturnValue(of(undefined)),
    excluirTabelaOrdem: vi.fn().mockReturnValue(of(undefined)),
  };
}

function setup(items: TabelaOrdemOperadoraItem[] = []) {
  const catalogService = makeServiceSpy(items);
  TestBed.configureTestingModule({
    imports: [TabelaAtosMultiplosComponent],
    providers: [{ provide: CatalogService, useValue: catalogService }],
  });
  const fixture = TestBed.createComponent(TabelaAtosMultiplosComponent);
  fixture.componentRef.setInput('operadoraId', 'op-1');
  fixture.detectChanges();
  return {
    fixture,
    component: fixture.componentInstance,
    catalogService,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('TabelaAtosMultiplosComponent', () => {
  it('exibe 6 linhas com valores padrao quando tabela vazia', () => {
    const { el } = setup([]);
    const linhas = el.querySelectorAll('.tabela-atos__linha');
    expect(linhas).toHaveLength(6);
    const inputs = el.querySelectorAll<HTMLInputElement>('.tabela-atos__input');
    expect(inputs[0].value).toBe('100');
    expect(inputs[1].value).toBe('100');
    expect(inputs[2].value).toBe('50');
    expect(inputs[3].value).toBe('70');
  });

  it('popula linhas com valores da API quando tabela configurada', () => {
    const items: TabelaOrdemOperadoraItem[] = [
      { numeroProcedimento: 1, tipoVia: 'MesmaVia', percentual: 1.0 },
      { numeroProcedimento: 1, tipoVia: 'ViaDiferente', percentual: 1.0 },
      { numeroProcedimento: 2, tipoVia: 'MesmaVia', percentual: 0.5 },
      { numeroProcedimento: 2, tipoVia: 'ViaDiferente', percentual: 0.7 },
      { numeroProcedimento: 3, tipoVia: 'MesmaVia', percentual: 0.4 },
      { numeroProcedimento: 3, tipoVia: 'ViaDiferente', percentual: 0.5 },
      { numeroProcedimento: 4, tipoVia: 'MesmaVia', percentual: 0.3 },
      { numeroProcedimento: 4, tipoVia: 'ViaDiferente', percentual: 0.4 },
      { numeroProcedimento: 5, tipoVia: 'MesmaVia', percentual: 0.2 },
      { numeroProcedimento: 5, tipoVia: 'ViaDiferente', percentual: 0.3 },
      { numeroProcedimento: 6, tipoVia: 'MesmaVia', percentual: 0.1 },
      { numeroProcedimento: 6, tipoVia: 'ViaDiferente', percentual: 0.1 },
    ];
    const { el } = setup(items);
    const inputs = el.querySelectorAll<HTMLInputElement>('.tabela-atos__input');
    expect(inputs[2].value).toBe('50');
    expect(inputs[3].value).toBe('70');
  });

  it('botao salvar chama salvarTabelaOrdem com payload correto', () => {
    const { el, catalogService } = setup([]);
    const btn = el.querySelector<HTMLButtonElement>('.tabela-atos__btn-salvar');
    btn?.click();
    expect(catalogService.salvarTabelaOrdem).toHaveBeenCalledWith(
      'op-1',
      expect.arrayContaining([
        expect.objectContaining({ numeroProcedimento: 1, tipoVia: 'MesmaVia', percentual: 1.0 }),
      ]),
    );
  });

  it('restaurar padroes com confirmacao chama excluirTabelaOrdem e reseta linhas', () => {
    const { el, catalogService, component } = setup([
      { numeroProcedimento: 1, tipoVia: 'MesmaVia', percentual: 0.8 },
      { numeroProcedimento: 1, tipoVia: 'ViaDiferente', percentual: 0.8 },
    ]);

    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const btn = el.querySelector<HTMLButtonElement>('.tabela-atos__btn-restaurar');
    btn?.click();

    expect(catalogService.excluirTabelaOrdem).toHaveBeenCalledWith('op-1');
    expect(component.linhas()[0].mesmaVia).toBe(100);
    confirmSpy.mockRestore();
  });

  it('restaurar padroes sem confirmacao nao chama excluirTabelaOrdem', () => {
    const { el, catalogService } = setup([]);
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
    const btn = el.querySelector<HTMLButtonElement>('.tabela-atos__btn-restaurar');
    btn?.click();
    expect(catalogService.excluirTabelaOrdem).not.toHaveBeenCalled();
    confirmSpy.mockRestore();
  });

  it('onMesmaViaChange atualiza percentual da linha correspondente', () => {
    const { component } = setup([]);
    const linhaAntes = component.linhas()[1];
    component.onMesmaViaChange(linhaAntes, '60');
    expect(component.linhas()[1].mesmaVia).toBe(60);
  });

  it('onViaDiferenteChange atualiza percentual da linha correspondente', () => {
    const { component } = setup([]);
    const linhaAntes = component.linhas()[1];
    component.onViaDiferenteChange(linhaAntes, '65');
    expect(component.linhas()[1].viaDiferente).toBe(65);
  });

  it('onMesmaViaChange ignora valor nao numerico', () => {
    const { component } = setup([]);
    const valorAntes = component.linhas()[0].mesmaVia;
    component.onMesmaViaChange(component.linhas()[0], 'abc');
    expect(component.linhas()[0].mesmaVia).toBe(valorAntes);
  });

  it('salvar exibe erro quando service falha', () => {
    const catalogServiceErr = {
      listarTabelaOrdem: vi.fn().mockReturnValue(of([])),
      salvarTabelaOrdem: vi.fn().mockReturnValue({
        subscribe: (o: { error: () => void }) => {
          o.error();
        },
      }),
      excluirTabelaOrdem: vi.fn().mockReturnValue(of(undefined)),
    };

    TestBed.configureTestingModule({
      imports: [TabelaAtosMultiplosComponent],
      providers: [{ provide: CatalogService, useValue: catalogServiceErr }],
    });
    const fixture = TestBed.createComponent(TabelaAtosMultiplosComponent);
    fixture.componentRef.setInput('operadoraId', 'op-err');
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.salvar();
    expect(component.erro()).toBeTruthy();
  });
});
