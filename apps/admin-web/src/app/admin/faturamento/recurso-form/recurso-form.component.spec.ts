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
  numero: '202512',
  dataEmissao: '2026-01-15',
  observacao: 'obs teste',
  totalGuias: 0,
  criadoEm: '2026-01-15T00:00:00Z',
  tipo: 'GlosaParcial',
};

const DETALHE: RecursoDetalheDto = { header: RECURSO, guias: [] };

function fakeInputEvent(value: string): Event {
  return { target: { value } } as unknown as Event;
}

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
  it('form cria recurso e navega para gerenciamento de guias', () => {
    const { component, el, recursoService, router } = setup();

    component.operadoraId.set('op-1');
    component.prestadorId.set('prest-1');
    component.onDataEmissaoInput('2026-01-15');

    el.querySelector<HTMLButtonElement>('.recurso-form__btn-salvar')?.click();

    expect(recursoService.criar).toHaveBeenCalledWith(
      expect.objectContaining({ numero: '202512' }),
    );
    expect(router.navigate).toHaveBeenCalledWith(['/admin/recursos', 'rec-1', 'guias']);
  });

  it('form bloqueia criação sem número', () => {
    const { component, el, recursoService } = setup();

    component.operadoraId.set('op-1');
    component.prestadorId.set('prest-1');
    component.dataEmissao.set('2026-01-15');
    // número permanece vazio (dataEmissao setada direto, sem disparar a sugestão)

    el.querySelector<HTMLButtonElement>('.recurso-form__btn-salvar')?.click();

    expect(recursoService.criar).not.toHaveBeenCalled();
    expect(component.erroValidacao()).toBe('Informe o número do recurso.');
  });

  it('form carrega recurso existente e preenche campos', () => {
    const { component } = setup({ id: 'rec-1' });

    expect(component.modoEditar()).toBe(true);
    expect(component.operadoraId()).toBe('op-1');
    expect(component.prestadorId()).toBe('prest-1');
    expect(component.dataEmissao()).toBe('2026-01-15');
    expect(component.numero()).toBe('202512');
    expect(component.observacao()).toBe('obs teste');
  });

  it('pré-preenche o número com o mês anterior à data de emissão', () => {
    const { component } = setup();

    component.onDataEmissaoInput('2026-01-15');

    expect(component.numero()).toBe('202512');
  });

  it('número aceita apenas dígitos, preservando zeros à esquerda', () => {
    const { component } = setup();

    component.onNumeroInput(fakeInputEvent('00a12-3b'));

    expect(component.numero()).toBe('00123');
  });

  it('input do número descarta caracteres não numéricos no DOM', () => {
    const { el } = setup();

    const input = el.querySelector<HTMLInputElement>('.recurso-form__input--numero');
    if (!input) {
      throw new Error('campo de número não encontrado');
    }
    input.value = '12a3';
    input.dispatchEvent(new Event('input'));

    expect(input.value).toBe('123');
  });

  it('número editado manualmente não é sobrescrito pela data', () => {
    const { component } = setup();

    component.onNumeroInput(fakeInputEvent('007'));
    component.onDataEmissaoInput('2026-01-15');

    expect(component.numero()).toBe('007');
  });

  it('Deve usar GlosaParcial como padrão', () => {
    const { component } = setup();

    expect(component.tipo()).toBe('GlosaParcial');
  });

  it('Deve enviar tipo GlosaBranca no payload quando selecionado', () => {
    const { component, el, recursoService } = setup();

    component.tipo.set('GlosaBranca');
    component.operadoraId.set('op-1');
    component.prestadorId.set('prest-1');
    component.onDataEmissaoInput('2026-01-15');

    el.querySelector<HTMLButtonElement>('.recurso-form__btn-salvar')?.click();

    expect(recursoService.criar).toHaveBeenCalledWith(
      expect.objectContaining({ tipo: 'GlosaBranca' }),
    );
  });
});
