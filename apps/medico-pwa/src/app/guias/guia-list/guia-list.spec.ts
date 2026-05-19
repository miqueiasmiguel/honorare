import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { Router } from '@angular/router';
import { GuiaListComponent } from './guia-list';
import { MedicoGuiaService } from '../medico-guia.service';
import type { MedicoGuiaSummaryItem, MedicoListarGuiasResult } from '../medico-guia.types';

function makeGuia(overrides: Partial<MedicoGuiaSummaryItem> = {}): MedicoGuiaSummaryItem {
  return {
    id: 'guia-1',
    operadoraNome: 'UNIMED JP',
    beneficiarioNome: 'João Silva',
    beneficiarioCarteira: '12345',
    senha: 'SEN001',
    dataAtendimento: '2026-01-15',
    situacao: 'Apresentada',
    totalItens: 3,
    temObservacao: false,
    ...overrides,
  };
}

function makeResult(itens: MedicoGuiaSummaryItem[] = [makeGuia()]): MedicoListarGuiasResult {
  return { itens, total: itens.length, pagina: 1, itensPorPagina: 20 };
}

function setup(result: MedicoListarGuiasResult = makeResult()) {
  const serviceSpy = {
    listar: vi.fn().mockReturnValue(of(result)),
  };
  const routerSpy = {
    navigate: vi.fn().mockReturnValue(Promise.resolve(true)),
  };

  TestBed.configureTestingModule({
    imports: [GuiaListComponent],
    providers: [
      { provide: MedicoGuiaService, useValue: serviceSpy },
      { provide: Router, useValue: routerSpy },
    ],
  });

  const fixture = TestBed.createComponent(GuiaListComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    service: serviceSpy,
    router: routerSpy,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('GuiaListComponent', () => {
  it('exibe lista de guias do service', () => {
    const result = makeResult([makeGuia(), makeGuia({ id: 'guia-2', senha: 'SEN002' })]);
    const { el } = setup(result);
    const rows = el.querySelectorAll('.guia-list__row');
    expect(rows).toHaveLength(2);
  });

  it('linha com temObservacao exibe ícone de alerta', () => {
    const result = makeResult([makeGuia({ temObservacao: true })]);
    const { el } = setup(result);
    const icon = el.querySelector('.guia-list__obs-icon');
    expect(icon).not.toBeNull();
  });

  it('filtro de operadora com debounce dispara nova busca', () => {
    vi.useFakeTimers();
    try {
      const { component, service } = setup();
      service.listar.mockClear();

      component.onOperadoraChange('UNIMED');

      vi.advanceTimersByTime(399);
      expect(service.listar).not.toHaveBeenCalled();

      vi.advanceTimersByTime(1);
      expect(service.listar).toHaveBeenCalledOnce();
    } finally {
      vi.useRealTimers();
    }
  });

  it('guia Apresentada exibe badge âmbar', () => {
    const result = makeResult([makeGuia({ situacao: 'Apresentada' })]);
    const { el } = setup(result);
    const badge = el.querySelector('.badge--ambar');
    expect(badge).not.toBeNull();
  });

  it('guia EmRecurso exibe badge ferrugem', () => {
    const result = makeResult([makeGuia({ situacao: 'EmRecurso' })]);
    const { el } = setup(result);
    const badge = el.querySelector('.badge--ferrugem');
    expect(badge).not.toBeNull();
  });

  it('clique na linha navega para /guias/:id', () => {
    const result = makeResult([makeGuia({ id: 'guia-abc' })]);
    const { el, router } = setup(result);
    const row = el.querySelector<HTMLElement>('.guia-list__row');
    row?.click();
    expect(router.navigate).toHaveBeenCalledWith(['/guias', 'guia-abc']);
  });

  it('paginação exibe botão próximo quando total > itensPorPagina', () => {
    const itens = [makeGuia()];
    const result: MedicoListarGuiasResult = { itens, total: 21, pagina: 1, itensPorPagina: 20 };
    const { el } = setup(result);
    const btns = Array.from(el.querySelectorAll('button'));
    const proximo = btns.find((b) => b.textContent.includes('Próximo'));
    expect(proximo).toBeDefined();
  });
});
