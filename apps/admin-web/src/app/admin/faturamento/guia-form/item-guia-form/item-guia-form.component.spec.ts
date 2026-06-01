import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CatalogService } from '../../../catalog/catalog.service';
import type { ListarProcedimentosResult, ProcedimentoItem } from '../../../catalog/catalog.types';
import { ItemGuiaFormComponent } from './item-guia-form.component';

const mockProcedimento: ProcedimentoItem = {
  id: 'p1',
  codigoTuss: '4030501',
  descricao: 'Colecistectomia',
  porte: null,
  porteAnestesico: null,
  ehSadt: false,
  temPorteProprioVideo: false,
  ativo: true,
  criadoEm: '2025-01-01',
};

function makeServiceSpy(procs: ProcedimentoItem[] = []) {
  const result: ListarProcedimentosResult = {
    itens: procs,
    total: procs.length,
    pagina: 1,
    itensPorPagina: 10,
  };
  return {
    listarProcedimentos: vi.fn().mockReturnValue(of(result)),
    listarTabelaOrdem: vi.fn().mockReturnValue(of([])),
  };
}

function setup(ehPacote = false, procs: ProcedimentoItem[] = []) {
  const catalogService = makeServiceSpy(procs);

  TestBed.configureTestingModule({
    imports: [ItemGuiaFormComponent],
    providers: [{ provide: CatalogService, useValue: catalogService }],
  });

  const fixture = TestBed.createComponent(ItemGuiaFormComponent);
  // Angular 20 JIT: setInput fails for signal inputs — use Object.assign to replace the signal directly
  Object.assign(fixture.componentInstance, { ehPacote: signal(ehPacote) });
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    catalogService,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('ItemGuiaFormComponent', () => {
  it('renderiza campos basicos posicao ordem via acomodacao urgencia', () => {
    const { el } = setup();

    expect(el.querySelector('.item-guia-form__select--posicao')).not.toBeNull();
    expect(el.querySelector('.item-guia-form__select--ordem')).not.toBeNull();
    expect(el.querySelector('.item-guia-form__select--via')).not.toBeNull();
    expect(el.querySelector('.item-guia-form__select--acomodacao')).not.toBeNull();
    expect(el.querySelector('.item-guia-form__checkbox--urgencia')).not.toBeNull();
  });

  it('valorApurado oculto quando ehPacote false', () => {
    const { el } = setup(false);

    const input = el.querySelector('.item-guia-form__input--valor-apurado');
    expect(input).toBeNull();
  });

  it('valorApurado visivel e required quando ehPacote true', () => {
    const { el } = setup(true);

    const input = el.querySelector<HTMLInputElement>('.item-guia-form__input--valor-apurado');
    expect(input).not.toBeNull();
    expect(input?.required).toBe(true);
  });

  it('emite itemChange com percentualOrdem ao submeter', () => {
    const { component, fixture, el } = setup();

    const emitSpy = vi.spyOn(component.itemChange, 'emit');

    component.procedimentoId.set('proc-abc');
    component.posicaoExecutor.set('Anestesista');
    component.percentualOrdem.set(0.5);
    component.viaAcesso.set('Videolaparoscopia');
    component.acomodacao.set('Apartamento');
    component.ehUrgencia.set(true);
    fixture.detectChanges();

    el.querySelector<HTMLButtonElement>('.item-guia-form__btn-salvar')?.click();

    expect(emitSpy).toHaveBeenCalledWith(
      expect.objectContaining({
        procedimentoId: 'proc-abc',
        posicaoExecutor: 'Anestesista',
        percentualOrdem: 0.5,
        viaAcesso: 'Videolaparoscopia',
        acomodacao: 'Apartamento',
        ehUrgencia: true,
        valorApurado: null,
      }),
    );
  });

  it('onCancelar emite null', () => {
    const { component } = setup();
    const emitSpy = vi.spyOn(component.itemChange, 'emit');
    component.onCancelar();
    expect(emitSpy).toHaveBeenCalledWith(null);
  });

  it('inicializa campos a partir do input item quando nao nulo', () => {
    const catalogService = makeServiceSpy();
    TestBed.configureTestingModule({
      imports: [ItemGuiaFormComponent],
      providers: [{ provide: CatalogService, useValue: catalogService }],
    });
    const fixture = TestBed.createComponent(ItemGuiaFormComponent);
    Object.assign(fixture.componentInstance, {
      item: signal({
        procedimentoId: 'proc-x',
        posicaoExecutor: 'Anestesista' as const,
        percentualOrdem: 0.7,
        viaAcesso: 'Videolaparoscopia' as const,
        acomodacao: 'Apartamento' as const,
        ehUrgencia: true,
        valorApurado: 150.5,
      }),
    });
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component.procedimentoId()).toBe('proc-x');
    expect(component.posicaoExecutor()).toBe('Anestesista');
    expect(component.percentualOrdem()).toBe(0.7);
    expect(component.valorApurado()).toBe('150.5');
    expect(component.ehUrgencia()).toBe(true);
  });

  it('onBuscaChange dispara busca apos debounce', () => {
    vi.useFakeTimers();
    try {
      const { component, fixture, catalogService } = setup(false, [mockProcedimento]);

      component.onBuscaChange('colec');
      vi.advanceTimersByTime(300);
      fixture.detectChanges();

      expect(catalogService.listarProcedimentos).toHaveBeenCalledWith(
        expect.objectContaining({ busca: 'colec' }),
      );
      expect(component.procedimentosSugestoes()).toHaveLength(1);
    } finally {
      vi.useRealTimers();
    }
  });

  it('selecionarProcedimento preenche procedimentoId e limpa sugestoes', () => {
    const { component, fixture } = setup(false, [mockProcedimento]);

    component.procedimentosSugestoes.set([mockProcedimento]);
    component.selecionarProcedimento(mockProcedimento);
    fixture.detectChanges();

    expect(component.procedimentoId()).toBe('p1');
    expect(component.procedimentoBusca()).toContain('4030501');
    expect(component.procedimentosSugestoes()).toHaveLength(0);
  });

  it('emite valorApurado numerico quando ehPacote true', () => {
    const { component, fixture, el } = setup(true);
    const emitSpy = vi.spyOn(component.itemChange, 'emit');

    component.procedimentoId.set('proc-y');
    component.valorApurado.set('250.75');
    fixture.detectChanges();

    el.querySelector<HTMLButtonElement>('.item-guia-form__btn-salvar')?.click();

    expect(emitSpy).toHaveBeenCalledWith(expect.objectContaining({ valorApurado: 250.75 }));
  });

  it('onBuscaChange com string vazia limpa sugestoes', () => {
    vi.useFakeTimers();
    try {
      const { component, fixture } = setup(false, [mockProcedimento]);

      component.procedimentosSugestoes.set([mockProcedimento]);
      component.onBuscaChange('');
      vi.advanceTimersByTime(300);
      fixture.detectChanges();

      expect(component.procedimentosSugestoes()).toHaveLength(0);
    } finally {
      vi.useRealTimers();
    }
  });

  it('campo tempo oculto para Cirurgiao', () => {
    const { component, fixture, el } = setup();
    component.posicaoExecutor.set('Cirurgiao');
    fixture.detectChanges();
    expect(el.querySelector('[data-testid="tempo-anestesico"]')).toBeNull();
  });

  it('campo tempo visivel para Anestesista', () => {
    const { component, fixture, el } = setup();
    component.posicaoExecutor.set('Anestesista');
    fixture.detectChanges();
    expect(el.querySelector('[data-testid="tempo-anestesico"]')).not.toBeNull();
  });

  it('emite tempoAnestesicoMin ao mudar campo', () => {
    const { component, fixture, el } = setup();
    const emitSpy = vi.spyOn(component.itemChange, 'emit');

    component.procedimentoId.set('proc-an');
    component.posicaoExecutor.set('Anestesista');
    fixture.detectChanges();

    const input = el.querySelector<HTMLInputElement>('[data-testid="tempo-anestesico"]');
    if (input) {
      input.value = '180';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    }

    el.querySelector<HTMLButtonElement>('.item-guia-form__btn-salvar')?.click();

    expect(emitSpy).toHaveBeenCalledWith(expect.objectContaining({ tempoAnestesicoMin: 180 }));
  });

  it('emite null quando campo tempo vazio', () => {
    const { component, fixture, el } = setup();
    const emitSpy = vi.spyOn(component.itemChange, 'emit');

    component.procedimentoId.set('proc-an');
    component.posicaoExecutor.set('Anestesista');
    fixture.detectChanges();

    el.querySelector<HTMLButtonElement>('.item-guia-form__btn-salvar')?.click();

    expect(emitSpy).toHaveBeenCalledWith(expect.objectContaining({ tempoAnestesicoMin: null }));
  });

  it('popula campo ao carregar item existente com tempo', () => {
    const catalogService = makeServiceSpy();
    TestBed.configureTestingModule({
      imports: [ItemGuiaFormComponent],
      providers: [{ provide: CatalogService, useValue: catalogService }],
    });
    const fixture = TestBed.createComponent(ItemGuiaFormComponent);
    Object.assign(fixture.componentInstance, {
      item: signal({
        procedimentoId: 'proc-an',
        posicaoExecutor: 'Anestesista' as const,
        percentualOrdem: 1.0,
        viaAcesso: 'Convencional' as const,
        acomodacao: 'Enfermaria' as const,
        ehUrgencia: false,
        valorApurado: null,
        tempoAnestesicoMin: 120,
      }),
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.tempoAnestesicoMin()).toBe(120);
  });

  it('carrega opcoes de tabela-ordem quando operadoraId fornecido', () => {
    const catalogService = makeServiceSpy();
    catalogService.listarTabelaOrdem = vi.fn().mockReturnValue(
      of([
        { numeroProcedimento: 1, tipoVia: 'MesmaVia', percentual: 1.0 },
        { numeroProcedimento: 1, tipoVia: 'ViaDiferente', percentual: 1.0 },
      ]),
    );

    TestBed.configureTestingModule({
      imports: [ItemGuiaFormComponent],
      providers: [{ provide: CatalogService, useValue: catalogService }],
    });
    const fixture = TestBed.createComponent(ItemGuiaFormComponent);
    Object.assign(fixture.componentInstance, { operadoraId: signal('op-x') });
    fixture.detectChanges();

    expect(catalogService.listarTabelaOrdem).toHaveBeenCalledWith('op-x');
  });
});
