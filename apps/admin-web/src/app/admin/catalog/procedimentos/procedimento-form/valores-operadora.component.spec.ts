import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ValoresOperadoraComponent } from './valores-operadora.component';
import { CatalogService } from '../../catalog.service';
import type { ProcedimentoValorOperadoraItem, TabelaItem } from '../../catalog.types';

const linhaSemValor: ProcedimentoValorOperadoraItem = {
  operadoraId: 'op-1',
  operadoraNome: 'Bradesco Saúde',
  tipoRuleSet: 'Nulo',
  tabelaId: null,
  valor: null,
  atualizadoEm: null,
};

const linhaComValor: ProcedimentoValorOperadoraItem = {
  operadoraId: 'op-2',
  operadoraNome: 'UNIMED João Pessoa',
  tipoRuleSet: 'Unimed',
  tabelaId: 'tab-1',
  valor: 526.5,
  atualizadoEm: '2026-01-01T00:00:00Z',
};

const mockTabelaUpsert: TabelaItem = {
  id: 'tab-1',
  operadoraId: 'op-2',
  procedimentoId: 'proc-1',
  codigoTuss: '30715013',
  descricao: 'Herniorrafia inguinal',
  valor: 600,
  atualizadoEm: '2026-01-01T00:00:00Z',
};

function setup(valores: ProcedimentoValorOperadoraItem[] = [linhaSemValor, linhaComValor]) {
  const catalogServiceSpy = {
    listarValoresPorProcedimento: vi.fn().mockReturnValue(of(valores)),
    upsertValorPorProcedimento: vi.fn().mockReturnValue(of(mockTabelaUpsert)),
    excluirValorPorProcedimento: vi.fn().mockReturnValue(of(undefined)),
  };

  TestBed.configureTestingModule({
    imports: [ValoresOperadoraComponent],
    providers: [{ provide: CatalogService, useValue: catalogServiceSpy }],
  });

  const fixture = TestBed.createComponent(ValoresOperadoraComponent);
  fixture.componentRef.setInput('procedimentoId', 'proc-1');
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    catalogService: catalogServiceSpy,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('ValoresOperadoraComponent', () => {
  it('carrega valores ao iniciar', () => {
    const { catalogService } = setup();
    expect(catalogService.listarValoresPorProcedimento).toHaveBeenCalledWith('proc-1');
  });

  it('renderiza uma linha por operadora ordenada por nome', () => {
    const { el } = setup();
    const linhas = el.querySelectorAll('.valores-operadora__linha');
    expect(linhas).toHaveLength(2);
    expect(linhas[0].textContent).toContain('Bradesco Saúde');
    expect(linhas[1].textContent).toContain('UNIMED João Pessoa');
  });

  it('formata valor como BRL quando presente', () => {
    const { el } = setup();
    const valorCell = el.querySelectorAll('.valores-operadora__valor')[1];
    const texto = valorCell.textContent.replace(/\s+/g, ' ').trim();
    expect(texto).toMatch(/R\$\s?526,50/);
  });

  it('exibe traço quando valor é nulo', () => {
    const { el } = setup();
    const valorCell = el.querySelectorAll('.valores-operadora__valor')[0];
    expect(valorCell.textContent.trim()).toBe('—');
  });

  it('mostra botão "Definir" para operadora sem valor', () => {
    const { el } = setup();
    const btn = el.querySelectorAll('.valores-operadora__linha')[0].querySelector('button');
    expect(btn?.textContent.trim()).toBe('Definir');
  });

  it('mostra botões "Editar" e "Excluir" para operadora com valor', () => {
    const { el } = setup();
    const linha = el.querySelectorAll('.valores-operadora__linha')[1];
    const botoes = linha.querySelectorAll('button');
    const textos = Array.from(botoes).map((b) => b.textContent.trim());
    expect(textos).toContain('Editar');
    expect(textos).toContain('Excluir');
  });

  it('ao clicar "Definir" e confirmar com valor 600.00, chama upsertValor com argumentos corretos', () => {
    const { component, catalogService } = setup();
    component.iniciarEdicao('op-1');
    component.valorEditando.set(600);
    component.confirmarEdicao('op-1');
    expect(catalogService.upsertValorPorProcedimento).toHaveBeenCalledWith('proc-1', 'op-1', {
      valor: 600,
    });
  });

  it('após upsert bem-sucedido, linha exibe valor formatado e botão "Editar"', () => {
    const { component, fixture, el, catalogService } = setup();
    catalogService.listarValoresPorProcedimento.mockReturnValueOnce(
      of([{ ...linhaSemValor, tabelaId: 'tab-new', valor: 600 }, linhaComValor]),
    );
    component.iniciarEdicao('op-1');
    component.valorEditando.set(600);
    component.confirmarEdicao('op-1');
    fixture.detectChanges();
    const linha = el.querySelectorAll('.valores-operadora__linha')[0];
    const valorCell = linha.querySelector('.valores-operadora__valor');
    const texto = (valorCell?.textContent ?? '').replace(/\s+/g, ' ').trim();
    expect(texto).toMatch(/R\$\s?600,00/);
    const botoes = linha.querySelectorAll('button');
    const textos = Array.from(botoes).map((b) => b.textContent.trim());
    expect(textos).toContain('Editar');
  });

  it('rejeita valor <= 0 com mensagem inline e não chama backend', () => {
    const { component, fixture, el, catalogService } = setup();
    component.iniciarEdicao('op-1');
    component.valorEditando.set(0);
    component.confirmarEdicao('op-1');
    fixture.detectChanges();
    expect(catalogService.upsertValorPorProcedimento).not.toHaveBeenCalled();
    const erro = el.querySelector('.valores-operadora__erro');
    expect(erro?.textContent ?? '').toContain('maior que zero');
  });

  it('clicar "Excluir" com confirmação chama excluirValor', () => {
    const { component, catalogService } = setup();
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    component.excluir('op-2');
    expect(catalogService.excluirValorPorProcedimento).toHaveBeenCalledWith('proc-1', 'op-2');
    confirmSpy.mockRestore();
  });

  it('clicar "Excluir" sem confirmação não chama excluirValor', () => {
    const { component, catalogService } = setup();
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
    component.excluir('op-2');
    expect(catalogService.excluirValorPorProcedimento).not.toHaveBeenCalled();
    confirmSpy.mockRestore();
  });

  it('exibe erro do backend quando upsert falha', () => {
    const { component, fixture, el, catalogService } = setup();
    catalogService.upsertValorPorProcedimento.mockReturnValueOnce(
      throwError(() => new Error('422')),
    );
    component.iniciarEdicao('op-1');
    component.valorEditando.set(50);
    component.confirmarEdicao('op-1');
    fixture.detectChanges();
    const erro = el.querySelector('.valores-operadora__erro');
    expect((erro?.textContent ?? '').trim()).toBeTruthy();
  });
});
