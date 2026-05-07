import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { PrestadorListComponent } from './prestador-list.component';
import { CatalogService } from '../../catalog.service';
import type { ListarPrestadoresResult, PrestadorItem } from '../../catalog.types';

const mockPrestadores: PrestadorItem[] = [
  {
    id: 'prest-1',
    nome: 'Dr. José Silva',
    registroProfissional: 'CRM-12345',
    ativo: true,
    criadoEm: '2026-01-01T00:00:00Z',
  },
  {
    id: 'prest-2',
    nome: 'Dra. Maria Santos',
    registroProfissional: null,
    ativo: false,
    criadoEm: '2026-02-01T00:00:00Z',
  },
];

function makeResult(itens: PrestadorItem[] = mockPrestadores): ListarPrestadoresResult {
  return { itens, total: itens.length, pagina: 1, itensPorPagina: 20 };
}

function setup(itens: PrestadorItem[] = mockPrestadores) {
  const catalogServiceSpy = {
    listarPrestadores: vi.fn().mockReturnValue(of(makeResult(itens))),
    excluirPrestador: vi.fn().mockReturnValue(of(undefined)),
  };

  const routerSpy = {
    navigate: vi.fn().mockReturnValue(Promise.resolve(true)),
  };

  TestBed.configureTestingModule({
    imports: [PrestadorListComponent],
    providers: [
      { provide: CatalogService, useValue: catalogServiceSpy },
      { provide: Router, useValue: routerSpy },
    ],
  });

  const fixture = TestBed.createComponent(PrestadorListComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    catalogService: catalogServiceSpy,
    router: routerSpy,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('PrestadorListComponent', () => {
  it('renderiza tabela vazia com mensagem "Nenhum prestador cadastrado"', () => {
    const { el } = setup([]);
    const empty = el.querySelector('.prestador-list__empty');
    const text = empty?.textContent ?? '';
    expect(text.trim()).toBe('Nenhum prestador cadastrado.');
  });

  it('exibe prestadores retornados pela API', () => {
    const { el } = setup();
    const rows = el.querySelectorAll('.prestador-list__row');
    expect(rows).toHaveLength(2);
  });

  it('filtro por nome dispara request após debounce de 400ms', () => {
    vi.useFakeTimers();
    try {
      const { component, catalogService } = setup();
      catalogService.listarPrestadores.mockClear();

      component.onBuscaChange('José');

      vi.advanceTimersByTime(399);
      expect(catalogService.listarPrestadores).not.toHaveBeenCalled();

      vi.advanceTimersByTime(1);
      expect(catalogService.listarPrestadores).toHaveBeenCalledWith(
        expect.objectContaining({ busca: 'José' }),
      );
    } finally {
      vi.useRealTimers();
    }
  });

  it('clicar "Novo prestador" navega para /admin/catalog/prestadores/novo', () => {
    const { el, router } = setup();
    const btns = Array.from(el.querySelectorAll('button'));
    const btn = btns.find((b) => b.textContent.trim() === 'Novo prestador');
    btn?.click();
    expect(router.navigate).toHaveBeenCalledWith(['/admin/catalog/prestadores/novo']);
  });

  it('clicar "Editar" navega para /admin/catalog/prestadores/:id', () => {
    const { el, router } = setup();
    const btns = Array.from(el.querySelectorAll('button'));
    const editarBtn = btns.find((b) => b.textContent.trim() === 'Editar');
    editarBtn?.click();
    expect(router.navigate).toHaveBeenCalledWith(['/admin/catalog/prestadores', 'prest-1']);
  });

  it('clicar "Excluir" e confirmar chama DELETE e recarrega lista', () => {
    const { el, catalogService, fixture } = setup();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    catalogService.listarPrestadores.mockClear();

    const btns = Array.from(el.querySelectorAll('button'));
    const excluirBtn = btns.find((b) => b.textContent.trim() === 'Excluir');
    excluirBtn?.click();
    fixture.detectChanges();

    expect(catalogService.excluirPrestador).toHaveBeenCalledWith('prest-1');
    expect(catalogService.listarPrestadores).toHaveBeenCalledOnce();
  });

  it('409 na exclusão exibe mensagem de erro com "guias"', () => {
    const { el, catalogService, fixture } = setup();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    catalogService.excluirPrestador.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 409 })),
    );

    const btns = Array.from(el.querySelectorAll('button'));
    const excluirBtn = btns.find((b) => b.textContent.trim() === 'Excluir');
    excluirBtn?.click();
    fixture.detectChanges();

    const erro = el.querySelector('.prestador-list__erro');
    const text = erro?.textContent ?? '';
    expect(text.trim()).toContain('guias');
  });
});
