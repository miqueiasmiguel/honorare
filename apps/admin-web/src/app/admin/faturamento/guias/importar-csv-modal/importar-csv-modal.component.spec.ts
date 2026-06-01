import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CatalogService } from '../../../catalog/catalog.service';
import { GuiaService } from '../../guia.service';
import type { ResultadoImportacaoGuiaDto } from '../../guia.types';
import { ImportarCsvModalComponent } from './importar-csv-modal.component';

const mockResultado: ResultadoImportacaoGuiaDto = {
  identificadorPagamento: 'PAG-001',
  somenteValidar: false,
  guiasCriadas: 2,
  guiasAtualizadas: 0,
  itensCriados: 5,
  itensAtualizados: 0,
  itensIgnorados: 0,
  beneficiariosCriados: 1,
  guiasPrevistas: 2,
  itensPrevistas: 5,
  erros: [],
  alertas: [],
};

function setup(open = true) {
  const guiaService = {
    importarCsv: vi.fn().mockReturnValue(of(mockResultado)),
  };
  const catalogService = {
    listarPrestadores: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 500 })),
    listarOperadoras: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 500 })),
  };

  TestBed.configureTestingModule({
    imports: [ImportarCsvModalComponent],
    providers: [
      { provide: GuiaService, useValue: guiaService },
      { provide: CatalogService, useValue: catalogService },
    ],
  });

  const fixture = TestBed.createComponent(ImportarCsvModalComponent);
  fixture.componentInstance.open = open;
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    guiaService,
    catalogService,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('ImportarCsvModalComponent', () => {
  it('renderiza formulário com selects e inputs quando open é true', () => {
    const { el } = setup(true);

    expect(el.querySelector('.importar-csv-modal__select')).not.toBeNull();
    expect(el.querySelector('.importar-csv-modal__input-arquivo')).not.toBeNull();
    expect(el.querySelector('.importar-csv-modal__checkbox')).not.toBeNull();
    expect(el.querySelector('.importar-csv-modal__btn-importar')).not.toBeNull();
  });

  it('não renderiza conteúdo quando open é false', () => {
    const { el } = setup(false);

    expect(el.querySelector('.importar-csv-modal')).toBeNull();
  });

  it('ao submeter chama importarCsv com os valores corretos', () => {
    const { component, guiaService } = setup(true);
    const mockFile = new File(['content'], 'test.csv', { type: 'text/csv' });

    component.prestadorId.set('prest-1');
    component.operadoraId.set('op-1');
    component.arquivo.set(mockFile);
    component.somenteValidar.set(false);

    component.importar();

    expect(guiaService.importarCsv).toHaveBeenCalledWith(mockFile, 'prest-1', 'op-1', false);
  });

  it('exibe resultado após importação bem-sucedida', () => {
    const { component, fixture, el } = setup(true);
    const mockFile = new File(['content'], 'test.csv', { type: 'text/csv' });

    component.prestadorId.set('prest-1');
    component.operadoraId.set('op-1');
    component.arquivo.set(mockFile);

    component.importar();
    fixture.detectChanges();

    expect(el.querySelector('.importar-csv-modal__resultado')).not.toBeNull();
    expect(el.querySelector('.importar-csv-modal__btn-concluir')).not.toBeNull();
  });

  it('exibe erro de validacao quando campos obrigatorios estao vazios', () => {
    const { component, fixture, el } = setup(true);

    component.importar();
    fixture.detectChanges();

    expect(el.querySelector('.importar-csv-modal__erro')).not.toBeNull();
  });

  it('emite concluido ao clicar Concluir', () => {
    const { component } = setup(true);
    const concluido = vi.fn();
    component.concluido.subscribe(concluido);

    component.concluir();

    expect(concluido).toHaveBeenCalled();
  });

  it('emite cancelado ao clicar Cancelar', () => {
    const { component, fixture, el } = setup(true);
    const cancelado = vi.fn();
    component.cancelado.subscribe(cancelado);

    el.querySelector<HTMLButtonElement>('.importar-csv-modal__btn-cancelar')?.click();
    fixture.detectChanges();

    expect(cancelado).toHaveBeenCalled();
  });
});
