import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { TabelaFormComponent } from './tabela-form.component';
import { CatalogService } from '../../catalog.service';
import type { OperadoraItem, TabelaItem } from '../../catalog.types';

const mockOperadora: OperadoraItem = {
  id: 'op-1',
  nome: 'UNIMED João Pessoa',
  registroAns: '012345',
  cnpj: '12345678000195',
  tipoRuleSet: 'Unimed',
  ativa: true,
  criadaEm: '2026-01-01T00:00:00Z',
};

const mockTabela: TabelaItem = {
  id: 'tab-1',
  operadoraId: 'op-1',
  procedimentoId: 'proc-1',
  codigoTuss: '30715013',
  descricao: 'Herniorrafia inguinal',
  valor: 150.5,
  atualizadoEm: '2026-01-01T00:00:00Z',
};

function setup(tabelaId: string | null = null) {
  const catalogServiceSpy = {
    listarOperadoras: vi
      .fn()
      .mockReturnValue(of({ itens: [mockOperadora], total: 1, pagina: 1, itensPorPagina: 200 })),
    listarProcedimentos: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 20 })),
    obterTabela: vi.fn().mockReturnValue(of(mockTabela)),
    criarTabela: vi.fn().mockReturnValue(of(mockTabela)),
    atualizarTabela: vi.fn().mockReturnValue(of(mockTabela)),
  };

  TestBed.configureTestingModule({
    imports: [TabelaFormComponent],
    providers: [{ provide: CatalogService, useValue: catalogServiceSpy }],
  });

  const fixture = TestBed.createComponent(TabelaFormComponent);
  if (tabelaId) {
    fixture.componentInstance.tabelaId = tabelaId;
  }
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    catalogService: catalogServiceSpy,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('TabelaFormComponent', () => {
  it('submit sem operadora exibe erro de validação', () => {
    const { component, fixture, el } = setup();
    component.form.controls.operadoraId.setValue('');
    component.salvar();
    fixture.detectChanges();
    const error = el.querySelector('.tabela-form__error--operadora');
    const text = error?.textContent ?? '';
    expect(text.trim()).toBeTruthy();
  });

  it('submit sem procedimento exibe erro de validação', () => {
    const { component, fixture, el } = setup();
    component.form.controls.operadoraId.setValue('op-1');
    component.form.controls.procedimentoId.setValue('');
    component.form.controls.valor.setValue(100);
    component.salvar();
    fixture.detectChanges();
    const error = el.querySelector('.tabela-form__error--procedimento');
    const text = error?.textContent ?? '';
    expect(text.trim()).toBeTruthy();
  });

  it('submit com valor 0 exibe erro de validação', () => {
    const { component, fixture, el } = setup();
    component.form.controls.operadoraId.setValue('op-1');
    component.form.controls.procedimentoId.setValue('proc-1');
    component.form.controls.valor.setValue(0);
    component.salvar();
    fixture.detectChanges();
    const error = el.querySelector('.tabela-form__error--valor');
    const text = error?.textContent ?? '';
    expect(text.trim()).toBeTruthy();
  });

  it('submit válido chama POST com payload correto', () => {
    const { component, catalogService } = setup();
    component.form.controls.operadoraId.setValue('op-1');
    component.form.controls.procedimentoId.setValue('proc-1');
    component.form.controls.valor.setValue(150.5);
    component.salvar();
    expect(catalogService.criarTabela).toHaveBeenCalledWith({
      operadoraId: 'op-1',
      procedimentoId: 'proc-1',
      valor: 150.5,
    });
  });
});
