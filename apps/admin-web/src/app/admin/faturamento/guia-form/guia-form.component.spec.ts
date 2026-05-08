import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { CatalogService } from '../../catalog/catalog.service';
import { GuiaService } from '../guia.service';
import type { GuiaDetalheItem } from '../guia.types';
import { GuiaFormComponent } from './guia-form.component';

const mockGuia: GuiaDetalheItem = {
  id: 'guia-1',
  prestadorId: 'prest-1',
  prestadorNome: 'Dr. João',
  operadoraId: 'op-1',
  operadoraNome: 'UNIMED',
  beneficiarioId: 'bene-1',
  beneficiarioNome: 'Maria',
  beneficiarioCarteira: '123456',
  senha: 'SENHA01',
  dataAtendimento: '2024-03-15',
  situacao: 'Apresentada',
  ehPacote: false,
  observacao: '',
  totalItens: 1,
  criadoEm: '2024-03-15T10:00:00Z',
  atualizadoEm: '2024-03-15T10:00:00Z',
  itens: [
    {
      id: 'item-1',
      procedimentoId: 'proc-1',
      codigoTuss: '4030501',
      descricaoProcedimento: 'Colecistectomia',
      posicaoExecutor: 'Cirurgiao',
      ordemProcedimento: 'Unico',
      viaAcesso: 'Convencional',
      acomodacao: 'Enfermaria',
      ehUrgencia: false,
      valorApurado: null,
      valorLiquidado: null,
    },
  ],
};

function makeGuiaServiceSpy(guia: GuiaDetalheItem | null = null) {
  return {
    criar: vi.fn().mockReturnValue(of(guia ?? mockGuia)),
    atualizar: vi.fn().mockReturnValue(of(guia ?? mockGuia)),
    obterPorId: vi.fn().mockReturnValue(of(guia ?? mockGuia)),
  };
}

function makeCatalogServiceSpy() {
  return {
    listarPrestadores: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 200 })),
    listarOperadoras: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 200 })),
    listarBeneficiarios: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 1 })),
    lookupOrCreateBeneficiario: vi.fn().mockReturnValue(of(null)),
    listarProcedimentos: vi
      .fn()
      .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 10 })),
  };
}

function makeRouterSpy() {
  return { navigate: vi.fn().mockReturnValue(Promise.resolve(true)) };
}

function setup(options: { id?: string; guia?: GuiaDetalheItem } = {}) {
  const guiaService = makeGuiaServiceSpy(options.guia ?? null);
  const catalogService = makeCatalogServiceSpy();
  const router = makeRouterSpy();
  const activatedRoute = {
    snapshot: {
      paramMap: { get: (key: string) => (key === 'id' ? (options.id ?? null) : null) },
    },
  };

  TestBed.configureTestingModule({
    imports: [GuiaFormComponent],
    providers: [
      { provide: GuiaService, useValue: guiaService },
      { provide: CatalogService, useValue: catalogService },
      { provide: Router, useValue: router },
      { provide: ActivatedRoute, useValue: activatedRoute },
    ],
  });

  const fixture = TestBed.createComponent(GuiaFormComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    guiaService,
    catalogService,
    router,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('GuiaFormComponent', () => {
  it('renderiza campos obrigatorios', () => {
    const { el } = setup();

    expect(el.querySelector('.guia-form__select--prestador')).not.toBeNull();
    expect(el.querySelector('.guia-form__select--operadora')).not.toBeNull();
    expect(el.querySelector('.guia-form__input--senha')).not.toBeNull();
    expect(el.querySelector('.guia-form__input--data-atendimento')).not.toBeNull();
    expect(el.querySelector('.guia-form__checkbox--eh-pacote')).not.toBeNull();
    expect(el.querySelector('.guia-form__textarea--observacao')).not.toBeNull();
    expect(el.querySelector('.guia-form__btn-adicionar-item')).not.toBeNull();
  });

  it('submit sem itens mostra erro de validacao e nao chama service', () => {
    const { component, fixture, el, guiaService } = setup();

    component.prestadorId.set('p1');
    component.operadoraId.set('o1');
    component.beneficiarioId.set('b1');
    component.senha.set('12345');
    component.dataAtendimento.set('2024-01-01');
    fixture.detectChanges();

    el.querySelector<HTMLButtonElement>('.guia-form__btn-salvar')?.click();
    fixture.detectChanges();

    expect(el.querySelector('.guia-form__erro-validacao')).not.toBeNull();
    expect(guiaService.criar).not.toHaveBeenCalled();
  });

  it('em modo editar carrega dados da guia no form', () => {
    const { component } = setup({ id: 'guia-1', guia: mockGuia });

    expect(component.modoEditar()).toBe(true);
    expect(component.prestadorId()).toBe('prest-1');
    expect(component.operadoraId()).toBe('op-1');
    expect(component.senha()).toBe('SENHA01');
    expect(component.dataAtendimento()).toBe('2024-03-15');
    expect(component.itens()).toHaveLength(1);
  });

  it('submit com dados validos chama GuiaService criar e navega para guias', () => {
    const { component, fixture, el, guiaService, router } = setup();

    component.prestadorId.set('p1');
    component.operadoraId.set('o1');
    component.beneficiarioId.set('b1');
    component.senha.set('12345');
    component.dataAtendimento.set('2024-01-01');
    component.itens.set([
      {
        procedimentoId: 'proc-1',
        posicaoExecutor: 'Cirurgiao',
        ordemProcedimento: 'Unico',
        viaAcesso: 'Convencional',
        acomodacao: 'Enfermaria',
        ehUrgencia: false,
        valorApurado: null,
      },
    ]);
    fixture.detectChanges();

    el.querySelector<HTMLButtonElement>('.guia-form__btn-salvar')?.click();

    expect(guiaService.criar).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/admin/guias']);
  });
});
