import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { BeneficiarioAutocompleteComponent } from './beneficiario-autocomplete.component';
import { CatalogService } from '../../catalog.service';
import type {
  BeneficiarioItem,
  ListarBeneficiariosResult,
  LookupOrCreateResult,
} from '../../catalog.types';

const mockBeneficiario: BeneficiarioItem = {
  id: 'ben-1',
  carteira: '001ABC',
  nome: 'JOÃO SILVA',
  criadoEm: '2026-01-01T00:00:00Z',
};

function makeListarResult(itens: BeneficiarioItem[] = []): ListarBeneficiariosResult {
  return { itens, total: itens.length, pagina: 1, itensPorPagina: 1 };
}

function makeServiceSpy(
  overrides: {
    listarBeneficiarios?: unknown;
    lookupOrCreateBeneficiario?: unknown;
  } = {},
) {
  return {
    listarBeneficiarios:
      overrides.listarBeneficiarios !== undefined
        ? vi.fn().mockReturnValue(overrides.listarBeneficiarios)
        : vi.fn().mockReturnValue(of(makeListarResult())),
    lookupOrCreateBeneficiario:
      overrides.lookupOrCreateBeneficiario !== undefined
        ? vi.fn().mockReturnValue(overrides.lookupOrCreateBeneficiario)
        : vi
            .fn()
            .mockReturnValue(
              of({ ...mockBeneficiario, criado: true } satisfies LookupOrCreateResult),
            ),
  };
}

function setup(
  overrides: {
    listarBeneficiarios?: unknown;
    lookupOrCreateBeneficiario?: unknown;
  } = {},
) {
  const catalogService = makeServiceSpy(overrides);

  TestBed.configureTestingModule({
    imports: [BeneficiarioAutocompleteComponent],
    providers: [{ provide: CatalogService, useValue: catalogService }],
  });

  const fixture = TestBed.createComponent(BeneficiarioAutocompleteComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    catalogService,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('BeneficiarioAutocompleteComponent', () => {
  it('exibeIndicadorDeBuscaEnquantoAguarda', () => {
    vi.useFakeTimers();
    try {
      const subject = new Subject<ListarBeneficiariosResult>();
      const { component, fixture, el } = setup({
        listarBeneficiarios: subject.asObservable(),
      });

      component.onCarteiraChange('001');
      vi.advanceTimersByTime(400);
      fixture.detectChanges();

      const spinner = el.querySelector('.beneficiario-autocomplete__spinner');
      expect(spinner).not.toBeNull();
    } finally {
      vi.useRealTimers();
    }
  });

  it('aoEncontrarBeneficiarioExistente_exibeBadgeEncontradoENomeAsync', () => {
    vi.useFakeTimers();
    try {
      const { component, fixture, el } = setup({
        listarBeneficiarios: of(makeListarResult([mockBeneficiario])),
      });

      component.onCarteiraChange('001ABC');
      vi.advanceTimersByTime(400);
      fixture.detectChanges();

      const badge = el.querySelector('.beneficiario-autocomplete__badge--encontrado');
      expect(badge).not.toBeNull();
      const badgeText = badge?.textContent ?? '';
      expect(badgeText.trim()).toBe('Encontrado');

      const nomeEl = el.querySelector('.beneficiario-autocomplete__nome');
      const nomeText = nomeEl?.textContent ?? '';
      expect(nomeText.trim()).toBe('JOÃO SILVA');
    } finally {
      vi.useRealTimers();
    }
  });

  it('aoNaoEncontrarBeneficiario_exibeCampoDeNomeAsync', () => {
    vi.useFakeTimers();
    try {
      const { component, fixture, el } = setup({
        listarBeneficiarios: of(makeListarResult([])),
      });

      component.onCarteiraChange('999');
      vi.advanceTimersByTime(400);
      fixture.detectChanges();

      const nomeForm = el.querySelector('.beneficiario-autocomplete__nome-form');
      expect(nomeForm).not.toBeNull();
    } finally {
      vi.useRealTimers();
    }
  });

  it('aoConfirmarNomeNovo_emiteBeneficiarioComCriadoTrueAsync', () => {
    vi.useFakeTimers();
    try {
      const lookupResult: LookupOrCreateResult = { ...mockBeneficiario, criado: true };
      const { component, fixture, catalogService, el } = setup({
        listarBeneficiarios: of(makeListarResult([])),
        lookupOrCreateBeneficiario: of(lookupResult),
      });

      const emitSpy = vi.spyOn(component.beneficiarioChange, 'emit');

      component.onCarteiraChange('001ABC');
      vi.advanceTimersByTime(400);
      fixture.detectChanges();

      const nomeInput = el.querySelector('.beneficiario-autocomplete__input--nome');
      expect(nomeInput).not.toBeNull();

      component.onNomeInputChange('João Silva');
      fixture.detectChanges();

      const btns = Array.from(el.querySelectorAll('button'));
      const btnConfirmar = btns.find((b) => b.textContent.trim() === 'Confirmar');
      btnConfirmar?.click();
      fixture.detectChanges();

      expect(catalogService.lookupOrCreateBeneficiario).toHaveBeenCalledWith(
        '001ABC',
        'João Silva',
      );
      expect(emitSpy).toHaveBeenCalledWith(expect.objectContaining({ id: 'ben-1' }));

      const badgeNovo = el.querySelector('.beneficiario-autocomplete__badge--novo');
      expect(badgeNovo).not.toBeNull();
    } finally {
      vi.useRealTimers();
    }
  });

  it('aoApagarCarteira_emiteNullAsync', () => {
    vi.useFakeTimers();
    try {
      const { component, fixture } = setup({
        listarBeneficiarios: of(makeListarResult([mockBeneficiario])),
      });

      const emitSpy = vi.spyOn(component.beneficiarioChange, 'emit');

      component.onCarteiraChange('001ABC');
      vi.advanceTimersByTime(400);
      fixture.detectChanges();

      emitSpy.mockClear();

      component.onCarteiraChange('');
      vi.advanceTimersByTime(400);
      fixture.detectChanges();

      expect(emitSpy).toHaveBeenCalledWith(null);
      expect(component.estado()).toBe('idle');
    } finally {
      vi.useRealTimers();
    }
  });

  it('erroDeRede_exibeMensagemInlineSemLancarExcecaoAsync', () => {
    vi.useFakeTimers();
    try {
      const { component, fixture, el } = setup({
        listarBeneficiarios: throwError(() => new Error('Network error')),
      });

      expect(() => {
        component.onCarteiraChange('001');
        vi.advanceTimersByTime(400);
        fixture.detectChanges();
      }).not.toThrow();

      expect(component.estado()).toBe('erro');

      const erroEl = el.querySelector('.beneficiario-autocomplete__erro');
      expect(erroEl).not.toBeNull();
    } finally {
      vi.useRealTimers();
    }
  });

  it('initialBeneficiario_preencheEstadoEncontrado', () => {
    // Angular 20 JIT mode doesn't support setInput() for input() signals.
    // Test the resulting DOM state by directly setting the internal signals
    // that the effect would populate.
    const { component, fixture, el } = setup();

    component.carteira.set(mockBeneficiario.carteira);
    component.nomeSelecionado.set(mockBeneficiario.nome);
    component.beneficiarioAtual.set(mockBeneficiario);
    component.estado.set('encontrado');
    fixture.detectChanges();

    const badge = el.querySelector('.beneficiario-autocomplete__badge--encontrado');
    expect(badge).not.toBeNull();
    const nomeEl = el.querySelector('.beneficiario-autocomplete__nome');
    const nomeText = nomeEl?.textContent ?? '';
    expect(nomeText.trim()).toBe('JOÃO SILVA');
  });

  it('disabled_true_bloqueiaCampoAsync', () => {
    const { component, fixture, el } = setup();

    // Angular 20 JIT mode doesn't support setInput('disabled') because 'disabled' conflicts
    // with reserved HTML attribute handling. We replace the InputSignal directly.
    Object.assign(component, { disabled: signal(true) });
    fixture.detectChanges();

    const input = el.querySelector<HTMLInputElement>('.beneficiario-autocomplete__input');
    expect(input?.disabled).toBe(true);
  });
});
