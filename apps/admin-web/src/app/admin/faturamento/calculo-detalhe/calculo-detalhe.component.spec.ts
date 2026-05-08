import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { GuiaCalculoResult } from '../guia.types';
import { CalculoDetalheComponent } from './calculo-detalhe.component';

function makeCalculo(overrides: Partial<GuiaCalculoResult> = {}): GuiaCalculoResult {
  return {
    guiaId: 'g1',
    ehPacote: false,
    realizadoEm: '2025-01-01T00:00:00Z',
    itens: [],
    ...overrides,
  };
}

function setup(calculo: GuiaCalculoResult | null = null) {
  TestBed.configureTestingModule({
    imports: [CalculoDetalheComponent],
  });

  const fixture = TestBed.createComponent(CalculoDetalheComponent);
  Object.assign(fixture.componentInstance, { calculo: signal(calculo) });
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('CalculoDetalheComponent', () => {
  it('renderiza vazio sem erro quando calculo e null', () => {
    const { el } = setup(null);
    expect(el.querySelector('.calculo-detalhe')).toBeNull();
  });

  it('renderiza N itens quando calculo tem N itens', () => {
    const calc = makeCalculo({
      itens: [
        {
          itemGuiaId: 'i1',
          codigoTuss: '1111',
          descricaoProcedimento: 'Proc A',
          situacao: 'Calculado',
          valorApurado: 100,
          passos: [],
        },
        {
          itemGuiaId: 'i2',
          codigoTuss: '2222',
          descricaoProcedimento: 'Proc B',
          situacao: 'SemTabela',
          valorApurado: null,
          passos: [],
        },
      ],
    });
    const { el } = setup(calc);
    expect(el.querySelectorAll('.calculo-detalhe__header')).toHaveLength(2);
  });

  it('click no header expande o body', () => {
    const calc = makeCalculo({
      itens: [
        {
          itemGuiaId: 'i1',
          codigoTuss: '1111',
          descricaoProcedimento: 'Proc A',
          situacao: 'Calculado',
          valorApurado: 100,
          passos: [],
        },
      ],
    });
    const { el, fixture } = setup(calc);

    expect(el.querySelector('.calculo-detalhe__body')).toBeNull();

    el.querySelector<HTMLButtonElement>('.calculo-detalhe__header')?.click();
    fixture.detectChanges();

    expect(el.querySelector('.calculo-detalhe__body')).not.toBeNull();
  });

  it('click duplo colapsa o body', () => {
    const calc = makeCalculo({
      itens: [
        {
          itemGuiaId: 'i1',
          codigoTuss: '1111',
          descricaoProcedimento: 'Proc A',
          situacao: 'Calculado',
          valorApurado: 100,
          passos: [],
        },
      ],
    });
    const { el, fixture } = setup(calc);

    const header = el.querySelector<HTMLButtonElement>('.calculo-detalhe__header');
    header?.click();
    fixture.detectChanges();
    expect(el.querySelector('.calculo-detalhe__body')).not.toBeNull();

    header?.click();
    fixture.detectChanges();
    expect(el.querySelector('.calculo-detalhe__body')).toBeNull();
  });

  it('item Calculado com 2 passos renderiza 2 linhas na tabela', () => {
    const calc = makeCalculo({
      itens: [
        {
          itemGuiaId: 'i1',
          codigoTuss: '1111',
          descricaoProcedimento: 'Proc A',
          situacao: 'Calculado',
          valorApurado: 200,
          passos: [
            { regra: 'ValorBase', fator: 1.0, valorResultante: 150 },
            { regra: 'Urgencia', fator: 1.3, valorResultante: 200 },
          ],
        },
      ],
    });
    const { el, fixture } = setup(calc);

    el.querySelector<HTMLButtonElement>('.calculo-detalhe__header')?.click();
    fixture.detectChanges();

    expect(el.querySelectorAll('.calculo-detalhe__passo')).toHaveLength(2);
  });

  it('item sem passos exibe mensagem fallback', () => {
    const calc = makeCalculo({
      itens: [
        {
          itemGuiaId: 'i1',
          codigoTuss: '1111',
          descricaoProcedimento: 'Proc A',
          situacao: 'SemTabela',
          valorApurado: null,
          passos: [],
        },
      ],
    });
    const { el, fixture } = setup(calc);

    el.querySelector<HTMLButtonElement>('.calculo-detalhe__header')?.click();
    fixture.detectChanges();

    const fallback = el.querySelector('.calculo-detalhe__sem-passos');
    expect(fallback).not.toBeNull();
    const text = fallback?.textContent ?? '';
    expect(text.trim()).toBe('Sem detalhes de cálculo');
  });
});
