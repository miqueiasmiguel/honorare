import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { Router } from '@angular/router';
import { ProcedimentoListComponent } from './procedimento-list.component';
import { CatalogService } from '../../catalog.service';
import type {
  ListarProcedimentosResult,
  OperadoraItem,
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

function setup(itens: ProcedimentoItem[] = mockProcedimentos) {
  const catalogServiceSpy = {
    listarProcedimentos: vi.fn().mockReturnValue(of(makeResult(itens))),
    excluirProcedimento: vi.fn().mockReturnValue(of(undefined)),
    listarOperadoras: vi
      .fn()
      .mockReturnValue(
        of({ itens: [] as OperadoraItem[], total: 0, pagina: 1, itensPorPagina: 200 }),
      ),
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

  it('botão "Importar dados" está presente no header', () => {
    const { el } = setup();
    const btns = Array.from(el.querySelectorAll('button'));
    const btn = btns.find((b) => b.textContent.trim() === 'Importar dados');
    expect(btn).toBeDefined();
  });

  it('botão "Importar dados" abre o modal (mostrarImportarModal = true)', () => {
    const { component, el, fixture } = setup();
    expect(component.mostrarImportarModal()).toBe(false);

    const btns = Array.from(el.querySelectorAll('button'));
    const btn = btns.find((b) => b.textContent.trim() === 'Importar dados');
    expect(btn).toBeDefined();
    btn?.click();
    fixture.detectChanges();

    expect(component.mostrarImportarModal()).toBe(true);
  });

  it('onImportConcluido fecha o modal e recarrega procedimentos', () => {
    const { component, catalogService, fixture } = setup();
    component.mostrarImportarModal.set(true);
    fixture.detectChanges();
    catalogService.listarProcedimentos.mockClear();

    component.onImportConcluido();
    fixture.detectChanges();

    expect(component.mostrarImportarModal()).toBe(false);
    expect(catalogService.listarProcedimentos).toHaveBeenCalledOnce();
  });

  it('botão "Importar CSV" e "Download template" não estão mais presentes', () => {
    const { el } = setup();
    const btns = Array.from(el.querySelectorAll('button'));
    expect(btns.find((b) => b.textContent.trim() === 'Importar CSV')).toBeUndefined();
    expect(btns.find((b) => b.textContent.trim() === 'Download template')).toBeUndefined();
  });
});
