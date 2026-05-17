import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { CatalogService } from '../../catalog/catalog.service';
import { RecursoService } from '../recurso.service';
import type { RecursoDetalheDto, RecursoDto } from '../recurso.types';
import { RecursoFormComponent } from './recurso-form.component';

const RECURSO: RecursoDto = {
  id: 'rec-1',
  operadoraId: 'op-1',
  operadoraNome: 'UNIMED',
  prestadorId: 'prest-1',
  prestadorNome: 'Dr. João',
  prestadorRegistroProfissional: null,
  numero: '202601-001',
  dataEmissao: '2026-01-15',
  observacao: 'obs teste',
  totalGuias: 0,
  criadoEm: '2026-01-15T00:00:00Z',
};

const DETALHE: RecursoDetalheDto = { header: RECURSO, guias: [] };

function makeRecursoServiceSpy() {
  return {
    criar: vi.fn().mockReturnValue(of(RECURSO)),
    atualizar: vi.fn().mockReturnValue(of(RECURSO)),
    obterPorId: vi.fn().mockReturnValue(of(DETALHE)),
  };
}

function makeCatalogServiceSpy() {
  return {
    listarOperadoras: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 200 })),
    listarPrestadores: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 200 })),
  };
}

function setup(options: { id?: string } = {}) {
  const recursoService = makeRecursoServiceSpy();
  const catalogService = makeCatalogServiceSpy();
  const router = { navigate: vi.fn().mockReturnValue(Promise.resolve(true)) };
  const activatedRoute = {
    snapshot: {
      paramMap: { get: (key: string) => (key === 'id' ? (options.id ?? null) : null) },
    },
  };

  TestBed.configureTestingModule({
    imports: [RecursoFormComponent],
    providers: [
      { provide: RecursoService, useValue: recursoService },
      { provide: CatalogService, useValue: catalogService },
      { provide: Router, useValue: router },
      { provide: ActivatedRoute, useValue: activatedRoute },
    ],
  });

  const fixture = TestBed.createComponent(RecursoFormComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    recursoService,
    catalogService,
    router,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('RecursoFormComponent', () => {
  it('form cria recurso e navega para lista', () => {
    const { component, el, recursoService, router } = setup();

    component.operadoraId.set('op-1');
    component.prestadorId.set('prest-1');
    component.dataEmissao.set('2026-01-15');

    el.querySelector<HTMLButtonElement>('.recurso-form__btn-salvar')?.click();

    expect(recursoService.criar).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/admin/recursos']);
  });

  it('form carrega recurso existente e preenche campos', () => {
    const { component } = setup({ id: 'rec-1' });

    expect(component.modoEditar()).toBe(true);
    expect(component.operadoraId()).toBe('op-1');
    expect(component.prestadorId()).toBe('prest-1');
    expect(component.dataEmissao()).toBe('2026-01-15');
    expect(component.observacao()).toBe('obs teste');
  });

  it('form exibe numero calculado da dataEmissao', () => {
    const { component, fixture, el } = setup();

    component.dataEmissao.set('2026-01-15');
    fixture.detectChanges();

    const numeroEl = el.querySelector('.recurso-form__numero');
    expect(numeroEl).not.toBeNull();
    expect(numeroEl?.textContent).toContain('202601');
  });
});
