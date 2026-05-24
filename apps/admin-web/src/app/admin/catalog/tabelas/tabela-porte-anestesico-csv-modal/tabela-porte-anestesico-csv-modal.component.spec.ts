import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { TabelaPorteAnestesicoCsvModalComponent } from './tabela-porte-anestesico-csv-modal.component';
import { CatalogService } from '../../catalog.service';
import type { ImportarTabelaPorteResult } from '../../catalog.types';

const mockResultado: ImportarTabelaPorteResult = {
  portesAtualizados: 5,
  procedimentosAtualizados: 10,
  procedimentosNaoEncontrados: ['30101050', '30102039'],
  erros: [],
};

function setup() {
  const catalogServiceSpy = {
    importarTabelaPorteAnestesico: vi.fn().mockReturnValue(of(mockResultado)),
  };

  TestBed.configureTestingModule({
    imports: [TabelaPorteAnestesicoCsvModalComponent],
    providers: [{ provide: CatalogService, useValue: catalogServiceSpy }],
  });

  const fixture = TestBed.createComponent(TabelaPorteAnestesicoCsvModalComponent);
  fixture.componentInstance.operadoraId = 'op-1';
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    catalogService: catalogServiceSpy,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('TabelaPorteAnestesicoCsvModalComponent', () => {
  it('upload de arquivo válido chama importarTabelaPorteAnestesico com operadoraId correto', () => {
    const { component, catalogService } = setup();
    const file = new File(['Procedimento,Porte\n30101050,E'], 'porte.csv', { type: 'text/csv' });
    component.onArquivoSelecionado(file);
    expect(catalogService.importarTabelaPorteAnestesico).toHaveBeenCalledWith('op-1', file);
  });

  it('exibe portesAtualizados e procedimentosAtualizados após upload', () => {
    const { component, fixture, el } = setup();
    const file = new File(['Procedimento,Porte'], 'porte.csv', { type: 'text/csv' });
    component.onArquivoSelecionado(file);
    fixture.detectChanges();

    const portes = el.querySelector('.tabela-porte-csv-modal__portes-atualizados');
    const procedimentos = el.querySelector('.tabela-porte-csv-modal__procedimentos-atualizados');
    expect(portes?.textContent ?? '').toContain('5');
    expect(procedimentos?.textContent ?? '').toContain('10');
  });

  it('lista procedimentosNaoEncontrados quando existirem', () => {
    const { component, fixture, el } = setup();
    const file = new File(['Procedimento,Porte'], 'porte.csv', { type: 'text/csv' });
    component.onArquivoSelecionado(file);
    fixture.detectChanges();

    const itens = el.querySelectorAll('.tabela-porte-csv-modal__nao-encontrado-item');
    expect(itens).toHaveLength(2);
    expect(itens[0].textContent).toContain('30101050');
  });
});
