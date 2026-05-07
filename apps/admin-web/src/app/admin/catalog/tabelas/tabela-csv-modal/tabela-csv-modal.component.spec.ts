import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { TabelaCsvModalComponent } from './tabela-csv-modal.component';
import { CatalogService } from '../../catalog.service';
import type { ImportarCsvResult } from '../../catalog.types';

const mockResultado: ImportarCsvResult = {
  inseridos: 3,
  atualizados: 1,
  ignorados: 0,
  erros: [{ linha: 5, mensagem: 'Código TUSS não encontrado: 99999999' }],
};

function setup() {
  const catalogServiceSpy = {
    importarTabelaCsv: vi.fn().mockReturnValue(of(mockResultado)),
  };

  TestBed.configureTestingModule({
    imports: [TabelaCsvModalComponent],
    providers: [{ provide: CatalogService, useValue: catalogServiceSpy }],
  });

  const fixture = TestBed.createComponent(TabelaCsvModalComponent);
  fixture.componentInstance.operadoraId = 'op-1';
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    catalogService: catalogServiceSpy,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('TabelaCsvModalComponent', () => {
  it('upload de arquivo válido chama POST importar-csv com operadoraId correto', () => {
    const { component, catalogService } = setup();
    const file = new File(['CodigoTuss;Valor\n30715013;150.50'], 'tabela.csv', {
      type: 'text/csv',
    });
    component.onArquivoSelecionado(file);
    expect(catalogService.importarTabelaCsv).toHaveBeenCalledWith('op-1', file);
  });

  it('exibe contadores inseridos/atualizados/erros', () => {
    const { component, fixture, el } = setup();
    const file = new File(['CodigoTuss;Valor'], 'tabela.csv', { type: 'text/csv' });
    component.onArquivoSelecionado(file);
    fixture.detectChanges();

    const inseridos = el.querySelector('.tabela-csv-modal__inseridos');
    const atualizados = el.querySelector('.tabela-csv-modal__atualizados');
    expect(inseridos?.textContent ?? '').toContain('3');
    expect(atualizados?.textContent ?? '').toContain('1');
  });

  it('erros de linha são listados', () => {
    const { component, fixture, el } = setup();
    const file = new File(['CodigoTuss;Valor'], 'tabela.csv', { type: 'text/csv' });
    component.onArquivoSelecionado(file);
    fixture.detectChanges();

    const erros = el.querySelectorAll('.tabela-csv-modal__erro-item');
    expect(erros).toHaveLength(1);
    expect(erros[0].textContent).toContain('99999999');
  });
});
