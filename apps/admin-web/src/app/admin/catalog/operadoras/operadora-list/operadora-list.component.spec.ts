import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { Router } from '@angular/router';
import { OperadoraListComponent } from './operadora-list.component';
import { CatalogService } from '../../catalog.service';
import type { ListarOperadorasResult, OperadoraItem } from '../../catalog.types';

const mockOperadoras: OperadoraItem[] = [
  {
    id: 'op-1',
    nome: 'UNIMED João Pessoa',
    registroAns: '012345',
    cnpj: '12345678000195',
    tipoRuleSet: 'Unimed',
    ativa: true,
    criadaEm: '2026-01-01T00:00:00Z',
  },
  {
    id: 'op-2',
    nome: 'Bradesco Saúde',
    registroAns: null,
    cnpj: null,
    tipoRuleSet: 'Nulo',
    ativa: false,
    criadaEm: '2026-02-01T00:00:00Z',
  },
];

function makeResult(itens: OperadoraItem[] = mockOperadoras): ListarOperadorasResult {
  return { itens, total: itens.length, pagina: 1, itensPorPagina: 20 };
}

function setup(itens: OperadoraItem[] = mockOperadoras) {
  const catalogServiceSpy = {
    listarOperadoras: vi.fn().mockReturnValue(of(makeResult(itens))),
    excluirOperadora: vi.fn().mockReturnValue(of(undefined)),
  };

  const routerSpy = {
    navigate: vi.fn().mockReturnValue(Promise.resolve(true)),
  };

  TestBed.configureTestingModule({
    imports: [OperadoraListComponent],
    providers: [
      { provide: CatalogService, useValue: catalogServiceSpy },
      { provide: Router, useValue: routerSpy },
    ],
  });

  const fixture = TestBed.createComponent(OperadoraListComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    catalogService: catalogServiceSpy,
    router: routerSpy,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('OperadoraListComponent', () => {
  it('exibe uma linha por operadora retornada', () => {
    const { el } = setup();
    const rows = el.querySelectorAll('.operadora-list__row');
    expect(rows).toHaveLength(2);
  });

  it('exibe mensagem "Nenhuma operadora cadastrada" quando lista vazia', () => {
    const { el } = setup([]);
    const empty = el.querySelector('.operadora-list__empty');
    const text = empty?.textContent ?? '';
    expect(text.trim()).toBe('Nenhuma operadora cadastrada.');
  });

  it('navega para /admin/catalog/operadoras/:id ao clicar na linha', () => {
    const { el, router } = setup();
    const row = el.querySelector<HTMLElement>('.operadora-list__row');
    row?.click();
    expect(router.navigate).toHaveBeenCalledWith(['/admin/catalog/operadoras', 'op-1']);
  });

  it('navega para /admin/catalog/operadoras/nova ao clicar em "Nova operadora"', () => {
    const { el, router } = setup();
    const btns = Array.from(el.querySelectorAll('button'));
    const btn = btns.find((b) => b.textContent.trim() === 'Nova operadora');
    btn?.click();
    expect(router.navigate).toHaveBeenCalledWith(['/admin/catalog/operadoras/nova']);
  });

  it('chama o service com filtro de nome após debounce de 300ms', () => {
    vi.useFakeTimers();
    try {
      const { component, catalogService } = setup();
      catalogService.listarOperadoras.mockClear();

      component.onBuscaChange('UNIMED');

      vi.advanceTimersByTime(299);
      expect(catalogService.listarOperadoras).not.toHaveBeenCalled();

      vi.advanceTimersByTime(1);
      expect(catalogService.listarOperadoras).toHaveBeenCalledWith(
        expect.objectContaining({ nome: 'UNIMED' }),
      );
    } finally {
      vi.useRealTimers();
    }
  });

  it('chama service.excluir e recarrega a lista após confirmação', () => {
    const { el, catalogService, fixture } = setup();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    catalogService.listarOperadoras.mockClear();

    const btns = Array.from(el.querySelectorAll('button'));
    const excluirBtn = btns.find((b) => b.textContent.trim() === 'Excluir');
    excluirBtn?.click();
    fixture.detectChanges();

    expect(catalogService.excluirOperadora).toHaveBeenCalledWith('op-1');
    expect(catalogService.listarOperadoras).toHaveBeenCalledOnce();
  });

  it('não chama service.excluir se o usuário cancelar o confirm()', () => {
    const { el, catalogService, fixture } = setup();
    vi.spyOn(window, 'confirm').mockReturnValue(false);

    const btns = Array.from(el.querySelectorAll('button'));
    const excluirBtn = btns.find((b) => b.textContent.trim() === 'Excluir');
    excluirBtn?.click();
    fixture.detectChanges();

    expect(catalogService.excluirOperadora).not.toHaveBeenCalled();
  });

  it('exibe "Sem cálculo" no badge da operadora Nulo', () => {
    const { el } = setup();
    const badge = el.querySelector('.badge--nulo');
    const text = badge?.textContent ?? '';
    expect(text.trim()).toBe('Sem cálculo');
  });

  it('não exibe "Sem apuração" para nenhuma operadora', () => {
    const { el } = setup();
    expect(el.textContent).not.toContain('Sem apuração');
  });
});
