import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ImportarModalComponent } from './importar-modal.component';
import { CatalogService } from '../../catalog.service';
import type {
  ImportarCsvResult,
  ImportarTabelaPorteResult,
  OperadoraItem,
} from '../../catalog.types';

const mockOperadoras: OperadoraItem[] = [
  {
    id: 'op-1',
    nome: 'UNIMED JPA',
    registroAns: null,
    cnpj: null,
    tipoRuleSet: 'Unimed',
    ativa: true,
    criadaEm: '2026-01-01',
  },
  {
    id: 'op-2',
    nome: 'Bradesco',
    registroAns: null,
    cnpj: null,
    tipoRuleSet: 'Nulo',
    ativa: true,
    criadaEm: '2026-01-01',
  },
];

const mockResultadoCsv: ImportarCsvResult = {
  inseridos: 4,
  atualizados: 2,
  ignorados: 1,
  erros: [{ linha: 7, mensagem: 'Código TUSS não encontrado: 99999999' }],
};

const mockResultadoPorte: ImportarTabelaPorteResult = {
  portesAtualizados: 6,
  procedimentosAtualizados: 12,
  procedimentosNaoEncontrados: ['30101050'],
  erros: [],
};

function setup() {
  const catalogServiceSpy = {
    listarOperadoras: vi.fn().mockReturnValue(
      of({
        itens: mockOperadoras,
        total: mockOperadoras.length,
        pagina: 1,
        itensPorPagina: 200,
      }),
    ),
    importarCsv: vi.fn().mockReturnValue(of(mockResultadoCsv)),
    importarTabelaCsv: vi.fn().mockReturnValue(of(mockResultadoCsv)),
    importarTabelaPorteAnestesico: vi.fn().mockReturnValue(of(mockResultadoPorte)),
  };

  TestBed.configureTestingModule({
    imports: [ImportarModalComponent],
    providers: [{ provide: CatalogService, useValue: catalogServiceSpy }],
  });

  const fixture = TestBed.createComponent(ImportarModalComponent);
  fixture.componentRef.setInput('open', true);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    catalogService: catalogServiceSpy,
    el: fixture.nativeElement as HTMLElement,
  };
}

function makeFile(name = 'arquivo.csv'): File {
  return new File(['CodigoTuss;Descricao\n30715013;teste'], name, { type: 'text/csv' });
}

describe('ImportarModalComponent', () => {
  it('ao trocar tipo para "valoresOperadora", select de operadora aparece', () => {
    const { component, fixture, el } = setup();

    expect(el.querySelector('.importar-modal__select-operadora')).toBeNull();

    component.onTipoChange('valoresOperadora');
    fixture.detectChanges();

    expect(el.querySelector('.importar-modal__select-operadora')).not.toBeNull();
  });

  it('botão Importar desabilitado sem arquivo', () => {
    const { fixture, el } = setup();
    const btn = el.querySelector<HTMLButtonElement>('.importar-modal__btn-importar');
    fixture.detectChanges();
    expect(btn?.disabled).toBe(true);
  });

  it('botão Importar desabilitado quando tipo exige operadora e nenhuma foi selecionada', () => {
    const { component, fixture, el } = setup();
    component.onTipoChange('valoresOperadora');
    component.onArquivoSelecionado(makeFile());
    fixture.detectChanges();

    const btn = el.querySelector<HTMLButtonElement>('.importar-modal__btn-importar');
    expect(btn?.disabled).toBe(true);
  });

  it('procedimentos: selecionar arquivo + Importar chama importarCsv', () => {
    const { component, catalogService } = setup();
    const file = makeFile('procs.csv');
    component.onArquivoSelecionado(file);
    component.importar();

    expect(catalogService.importarCsv).toHaveBeenCalledWith(file);
    expect(catalogService.importarTabelaCsv).not.toHaveBeenCalled();
  });

  it('valoresOperadora: Importar chama importarTabelaCsv com operadora correta', () => {
    const { component, catalogService } = setup();
    component.onTipoChange('valoresOperadora');
    component.onOperadoraChange('op-1');
    const file = makeFile('valores.csv');
    component.onArquivoSelecionado(file);
    component.importar();

    expect(catalogService.importarTabelaCsv).toHaveBeenCalledWith('op-1', file);
  });

  it('porteAnestesico: Importar chama importarTabelaPorteAnestesico com operadora correta', () => {
    const { component, catalogService } = setup();
    component.onTipoChange('porteAnestesico');
    component.onOperadoraChange('op-1');
    const file = makeFile('porte.csv');
    component.onArquivoSelecionado(file);
    component.importar();

    expect(catalogService.importarTabelaPorteAnestesico).toHaveBeenCalledWith('op-1', file);
  });

  it('após resposta de sucesso (procedimentos), renderiza contadores', () => {
    const { component, fixture, el } = setup();
    component.onArquivoSelecionado(makeFile());
    component.importar();
    fixture.detectChanges();

    const inseridos = el.querySelector('.importar-modal__inseridos');
    const atualizados = el.querySelector('.importar-modal__atualizados');
    const ignorados = el.querySelector('.importar-modal__ignorados');
    expect(inseridos?.textContent ?? '').toContain('4');
    expect(atualizados?.textContent ?? '').toContain('2');
    expect(ignorados?.textContent ?? '').toContain('1');
  });

  it('após resposta de sucesso (porte anestésico), renderiza portesAtualizados e procedimentosAtualizados', () => {
    const { component, fixture, el } = setup();
    component.onTipoChange('porteAnestesico');
    component.onOperadoraChange('op-1');
    component.onArquivoSelecionado(makeFile());
    component.importar();
    fixture.detectChanges();

    const portes = el.querySelector('.importar-modal__portes-atualizados');
    const procs = el.querySelector('.importar-modal__procedimentos-atualizados');
    expect(portes?.textContent ?? '').toContain('6');
    expect(procs?.textContent ?? '').toContain('12');
  });

  it('baixar exemplo gera blob com nome correto', () => {
    const { component } = setup();
    const createObjectURL = vi.fn().mockReturnValue('blob:fake');
    const revokeObjectURL = vi.fn();
    (URL as unknown as { createObjectURL: typeof createObjectURL }).createObjectURL =
      createObjectURL;
    (URL as unknown as { revokeObjectURL: typeof revokeObjectURL }).revokeObjectURL =
      revokeObjectURL;

    // eslint-disable-next-line @typescript-eslint/no-deprecated
    const realCreateElement = document.createElement.bind(document);
    let anchor: HTMLAnchorElement | null = null;
    vi.spyOn(document, 'createElement').mockImplementation((tag: string) => {
      const node = realCreateElement(tag);
      if (tag === 'a') {
        anchor = node as HTMLAnchorElement;
        anchor.click = vi.fn();
      }
      return node;
    });

    component.baixarExemplo();

    expect(createObjectURL).toHaveBeenCalled();
    expect(anchor?.download).toBe('template-procedimentos.csv');
  });
});
