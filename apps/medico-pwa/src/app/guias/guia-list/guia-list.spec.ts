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
    const cards = el.querySelectorAll('.guia-card');
    expect(cards).toHaveLength(2);
  });

  it('guia com temObservacao exibe ícone de alerta', () => {
    const result = makeResult([makeGuia({ temObservacao: true })]);
    const { el } = setup(result);
    const icon = el.querySelector('.guia-card__obs');
    expect(icon).not.toBeNull();
  });

  it('filtro de operadora local oculta guias que não correspondem', () => {
    const result = makeResult([
      makeGuia({ id: 'guia-1', operadoraNome: 'UNIMED JP' }),
      makeGuia({ id: 'guia-2', operadoraNome: 'AMIL' }),
    ]);
    const { component } = setup(result);

    component.operadoraFiltro.set('amil');

    expect(component.guiasFiltradas()).toHaveLength(1);
    expect(component.guiasFiltradas()[0].operadoraNome).toBe('AMIL');
  });

  it('filtro de beneficiário oculta guias que não correspondem', () => {
    const result = makeResult([
      makeGuia({ id: 'guia-1', beneficiarioNome: 'João Silva' }),
      makeGuia({ id: 'guia-2', beneficiarioNome: 'Maria Souza' }),
    ]);
    const { component } = setup(result);

    component.beneficiarioFiltro.set('maria');

    expect(component.guiasFiltradas()).toHaveLength(1);
    expect(component.guiasFiltradas()[0].beneficiarioNome).toBe('Maria Souza');
  });

  it('filtro de beneficiário trata beneficiarioNome null como string vazia', () => {
    const result = makeResult([
      makeGuia({ id: 'guia-1', beneficiarioNome: null }),
      makeGuia({ id: 'guia-2', beneficiarioNome: 'João Silva' }),
    ]);
    const { component } = setup(result);

    component.beneficiarioFiltro.set('silva');

    expect(component.guiasFiltradas()).toHaveLength(1);
  });

  it('filtro de situação oculta guias de outras situações', () => {
    const result = makeResult([
      makeGuia({ id: 'guia-1', situacao: 'Apresentada' }),
      makeGuia({ id: 'guia-2', situacao: 'EmRecurso' }),
      makeGuia({ id: 'guia-3', situacao: 'Apresentada' }),
    ]);
    const { component } = setup(result);

    component.situacaoFiltro.set('EmRecurso');

    expect(component.guiasFiltradas()).toHaveLength(1);
    expect(component.guiasFiltradas()[0].situacao).toBe('EmRecurso');
  });

  it('toggleSituacao desativa filtro ao clicar na mesma situação', () => {
    const { component } = setup();

    component.toggleSituacao('Apresentada');
    expect(component.situacaoFiltro()).toBe('Apresentada');

    component.toggleSituacao('Apresentada');
    expect(component.situacaoFiltro()).toBe('');
  });

  it('filtro apenasComObservacao exibe somente guias com observação', () => {
    const result = makeResult([
      makeGuia({ id: 'guia-1', temObservacao: false }),
      makeGuia({ id: 'guia-2', temObservacao: true }),
    ]);
    const { component } = setup(result);

    component.apenasComObservacao.set(true);

    expect(component.guiasFiltradas()).toHaveLength(1);
    expect(component.guiasFiltradas()[0].id).toBe('guia-2');
  });

  it('filtrosAtivos conta cada filtro ativo', () => {
    const { component } = setup();

    expect(component.filtrosAtivos()).toBe(0);

    component.operadoraFiltro.set('unimed');
    expect(component.filtrosAtivos()).toBe(1);

    component.beneficiarioFiltro.set('joao');
    expect(component.filtrosAtivos()).toBe(2);

    component.situacaoFiltro.set('Apresentada');
    expect(component.filtrosAtivos()).toBe(3);

    component.apenasComObservacao.set(true);
    expect(component.filtrosAtivos()).toBe(4);
  });

  it('limparFiltros reseta todos os filtros client-side', () => {
    const { component } = setup();

    component.operadoraFiltro.set('unimed');
    component.beneficiarioFiltro.set('joao');
    component.situacaoFiltro.set('Apresentada');
    component.apenasComObservacao.set(true);

    component.limparFiltros();

    expect(component.operadoraFiltro()).toBe('');
    expect(component.beneficiarioFiltro()).toBe('');
    expect(component.situacaoFiltro()).toBe('');
    expect(component.apenasComObservacao()).toBe(false);
    expect(component.filtrosAtivos()).toBe(0);
  });

  it('limparFiltros dispara _carregar quando há filtro de data', () => {
    const { component, service } = setup();

    service.listar.mockClear();
    component.dataInicio.set('2026-01-01');
    component.limparFiltros();

    expect(component.dataInicio()).toBe('');
    expect(service.listar).toHaveBeenCalledTimes(1);
  });

  it('filtrosAbertos começa fechado', () => {
    const { component } = setup();
    expect(component.filtrosAbertos()).toBe(false);
  });

  it('botão de toggle alterna filtrosAbertos', () => {
    const { el, component } = setup();
    const toggle = el.querySelector<HTMLButtonElement>('.guia-list__toggle');

    toggle?.click();
    expect(component.filtrosAbertos()).toBe(true);

    toggle?.click();
    expect(component.filtrosAbertos()).toBe(false);
  });

  it('contagem reflete filtro client-side quando ativo', () => {
    const result = makeResult([
      makeGuia({ id: 'guia-1', situacao: 'Apresentada' }),
      makeGuia({ id: 'guia-2', situacao: 'EmRecurso' }),
    ]);
    const { component } = setup(result);

    expect(component.contagem()).toBe(2);

    component.situacaoFiltro.set('EmRecurso');
    expect(component.contagem()).toBe(1);
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

  it('guia Liquidada exibe badge verde', () => {
    const result = makeResult([makeGuia({ situacao: 'Liquidada' })]);
    const { el } = setup(result);
    const badge = el.querySelector('.badge--verde');
    expect(badge).not.toBeNull();
  });

  it('clique no card navega para /guias/:id', () => {
    const result = makeResult([makeGuia({ id: 'guia-abc' })]);
    const { el, router } = setup(result);
    const card = el.querySelector<HTMLElement>('.guia-card');
    card?.click();
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

  it('formatarData converte ISO para DD/MM/AAAA', () => {
    const { component } = setup();
    expect(component.formatarData('2026-01-15')).toBe('15/01/2026');
  });

  it('situacaoLabel exibe "Em Recurso" para EmRecurso', () => {
    const { component } = setup();
    expect(component.situacaoLabel('EmRecurso')).toBe('Em Recurso');
    expect(component.situacaoLabel('Apresentada')).toBe('Apresentada');
  });
});
