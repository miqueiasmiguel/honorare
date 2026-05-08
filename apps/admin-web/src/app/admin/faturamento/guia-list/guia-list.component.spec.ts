import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { GuiaService } from '../guia.service';
import type { GuiaItem, ListarGuiasResult } from '../guia.types';
import { GuiaListComponent } from './guia-list.component';

function makeGuia(overrides: Partial<GuiaItem> = {}): GuiaItem {
  return {
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
    ...overrides,
  };
}

function makeResult(itens: GuiaItem[]): ListarGuiasResult {
  return { itens, total: itens.length, pagina: 1, itensPorPagina: 20 };
}

function setup(guias: GuiaItem[] = [makeGuia()]) {
  const guiaService = {
    listar: vi.fn().mockReturnValue(of(makeResult(guias))),
    excluir: vi.fn().mockReturnValue(of(undefined)),
  };
  const router = { navigate: vi.fn().mockReturnValue(Promise.resolve(true)) };

  TestBed.configureTestingModule({
    imports: [GuiaListComponent],
    providers: [
      { provide: GuiaService, useValue: guiaService },
      { provide: Router, useValue: router },
    ],
  });

  const fixture = TestBed.createComponent(GuiaListComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    guiaService,
    router,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('GuiaListComponent', () => {
  it('renderiza tabela com guias', () => {
    const guias = [makeGuia({ id: 'g-1' }), makeGuia({ id: 'g-2' })];
    const { el } = setup(guias);
    const rows = el.querySelectorAll('.guia-list__row');
    expect(rows).toHaveLength(2);
  });

  it('linha Apresentada tem classe CSS correta', () => {
    const { el } = setup([makeGuia({ situacao: 'Apresentada' })]);
    const row = el.querySelector('.guia-list__row');
    expect(row?.classList.contains('guia-list__row--apresentada')).toBe(true);
  });

  it('linha Liquidada tem classe CSS correta', () => {
    const { el } = setup([makeGuia({ situacao: 'Liquidada' })]);
    const row = el.querySelector('.guia-list__row');
    expect(row?.classList.contains('guia-list__row--liquidada')).toBe(true);
  });

  it('linha EmRecurso tem classe CSS correta', () => {
    const { el } = setup([makeGuia({ situacao: 'EmRecurso' })]);
    const row = el.querySelector('.guia-list__row');
    expect(row?.classList.contains('guia-list__row--em-recurso')).toBe(true);
  });

  it('clicar Nova Guia navega para /admin/guias/nova', () => {
    const { el, router } = setup();
    el.querySelector<HTMLButtonElement>('.guia-list__btn-nova')?.click();
    expect(router.navigate).toHaveBeenCalledWith(['/admin/guias/nova']);
  });

  it('filtro por situacao recarrega lista com parametro correto', () => {
    const { component, guiaService } = setup();
    guiaService.listar.mockClear();

    component.onFiltroSituacaoChange('Liquidada');

    expect(guiaService.listar).toHaveBeenCalledWith(
      expect.objectContaining({ situacao: 'Liquidada' }),
    );
  });
});
