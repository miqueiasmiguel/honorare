import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { Router } from '@angular/router';
import { ProcedimentoListComponent } from './procedimento-list.component';
import { CatalogService } from '../../catalog.service';
import type {
  ImportarCsvResult,
  ListarProcedimentosResult,
  ProcedimentoItem,
} from '../../catalog.types';

const mockProcedimentos: ProcedimentoItem[] = [
  {
    id: 'proc-1',
    codigoTuss: '30715013',
    descricao: 'Herniorrafia inguinal',
    porte: '6B',
    porteAnestesico: 4,
    ehSadt: false,
    temPorteProprioVideo: false,
    ativo: true,
    criadoEm: '2026-01-01T00:00:00Z',
  },
  {
    id: 'proc-2',
    codigoTuss: '40314340',
    descricao: 'Eletroencefalograma',
    porte: null,
    porteAnestesico: null,
    ehSadt: true,
    temPorteProprioVideo: true,
    ativo: true,
    criadoEm: '2026-02-01T00:00:00Z',
  },
];

function makeResult(itens: ProcedimentoItem[] = mockProcedimentos): ListarProcedimentosResult {
  return { itens, total: itens.length, pagina: 1, itensPorPagina: 20 };
}

const defaultImportResult: ImportarCsvResult = {
  inseridos: 0,
  atualizados: 0,
  ignorados: 0,
  erros: [],
};

function setup(itens: ProcedimentoItem[] = mockProcedimentos) {
  const catalogServiceSpy = {
    listarProcedimentos: vi.fn().mockReturnValue(of(makeResult(itens))),
    excluirProcedimento: vi.fn().mockReturnValue(of(undefined)),
    importarCsv: vi.fn().mockReturnValue(of(defaultImportResult)),
  };

  const routerSpy = {
    navigate: vi.fn().mockReturnValue(Promise.resolve(true)),
  };

  TestBed.configureTestingModule({
    imports: [ProcedimentoListComponent],
    providers: [
      { provide: CatalogService, useValue: catalogServiceSpy },
      { provide: Router, useValue: routerSpy },
    ],
  });

  const fixture = TestBed.createComponent(ProcedimentoListComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    catalogService: catalogServiceSpy,
    router: routerSpy,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('ProcedimentoListComponent', () => {
  it('exibe uma linha por procedimento retornado', () => {
    const { el } = setup();
    const rows = el.querySelectorAll('.procedimento-list__row');
    expect(rows).toHaveLength(2);
  });

  it('exibe chip "SADT" apenas quando ehSadt é true', () => {
    const { el } = setup();
    const rows = Array.from(el.querySelectorAll('.procedimento-list__row'));
    expect(rows[0].querySelectorAll('.chip--sadt').length).toBe(0);
    expect(rows[1].querySelectorAll('.chip--sadt').length).toBe(1);
  });

  it('não exibe chip "SADT" quando ehSadt é false', () => {
    const { el } = setup();
    const rows = Array.from(el.querySelectorAll('.procedimento-list__row'));
    expect(rows[0].querySelectorAll('.chip--sadt').length).toBe(0);
  });

  it('exibe chip "Vídeo próprio" apenas quando temPorteProprioVideo é true', () => {
    const { el } = setup();
    const rows = Array.from(el.querySelectorAll('.procedimento-list__row'));
    expect(rows[0].querySelectorAll('.chip--video').length).toBe(0);
    expect(rows[1].querySelectorAll('.chip--video').length).toBe(1);
  });

  it('campo de busca chama service com termo após debounce de 300ms', () => {
    vi.useFakeTimers();
    try {
      const { component, catalogService } = setup();
      catalogService.listarProcedimentos.mockClear();

      component.onBuscaChange('hernio');

      vi.advanceTimersByTime(299);
      expect(catalogService.listarProcedimentos).not.toHaveBeenCalled();

      vi.advanceTimersByTime(1);
      expect(catalogService.listarProcedimentos).toHaveBeenCalledWith(
        expect.objectContaining({ busca: 'hernio' }),
      );
    } finally {
      vi.useRealTimers();
    }
  });

  it('botão "Importar CSV" está presente no DOM', () => {
    const { el } = setup();
    const btns = Array.from(el.querySelectorAll('button'));
    const btn = btns.find((b) => b.textContent.trim() === 'Importar CSV');
    expect(btn).toBeDefined();
  });

  it('exibe resumo de importação após upload bem-sucedido', () => {
    const { component, catalogService, fixture, el } = setup();
    const importResult: ImportarCsvResult = {
      inseridos: 5,
      atualizados: 2,
      ignorados: 1,
      erros: [],
    };
    catalogService.importarCsv.mockReturnValue(of(importResult));

    const file = new File(['content'], 'test.csv', { type: 'text/csv' });
    component.onArquivoSelecionado(file);
    fixture.detectChanges();

    const resumo = el.querySelector('.procedimento-list__import-resumo');
    const text = resumo?.textContent ?? '';
    expect(text).toContain('5');
    expect(text).toContain('2');
  });

  it('exibe erros de linha quando importação retorna erros', () => {
    const { component, catalogService, fixture, el } = setup();
    const importResult: ImportarCsvResult = {
      inseridos: 0,
      atualizados: 0,
      ignorados: 0,
      erros: [{ linha: 2, mensagem: 'Código TUSS inválido' }],
    };
    catalogService.importarCsv.mockReturnValue(of(importResult));

    const file = new File(['content'], 'test.csv', { type: 'text/csv' });
    component.onArquivoSelecionado(file);
    fixture.detectChanges();

    const erros = Array.from(el.querySelectorAll('.procedimento-list__import-erro'));
    expect(erros.length).toBeGreaterThan(0);
    const text = erros[0]?.textContent ?? '';
    expect(text).toContain('Código TUSS inválido');
  });

  it('botão "Download template" dispara download de arquivo .csv', () => {
    const { component, el } = setup();
    const spy = vi.spyOn(component, 'downloadTemplate').mockImplementation(() => {
      return;
    });

    const btns = Array.from(el.querySelectorAll('button'));
    const btn = btns.find((b) => b.textContent.trim() === 'Download template');
    btn?.click();

    expect(spy).toHaveBeenCalledOnce();
  });
});
