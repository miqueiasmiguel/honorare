import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ActivatedRoute, Router } from '@angular/router';
import { OperadoraFormComponent } from './operadora-form.component';
import { CatalogService } from '../../catalog.service';
import type { OperadoraItem } from '../../catalog.types';

const mockOperadora: OperadoraItem = {
  id: 'op-1',
  nome: 'UNIMED João Pessoa',
  registroAns: '012345',
  cnpj: '12345678000195',
  tipoRuleSet: 'Unimed',
  ativa: true,
  criadaEm: '2026-01-01T00:00:00Z',
};

function setup(operadoraId: string | null = null, operadora: OperadoraItem = mockOperadora) {
  const catalogServiceSpy = {
    obterOperadora: vi.fn().mockReturnValue(of(operadora)),
    criarOperadora: vi.fn().mockReturnValue(of(mockOperadora)),
    atualizarOperadora: vi.fn().mockReturnValue(of(mockOperadora)),
    listarPortesAnestesico: vi.fn().mockReturnValue(of([])),
    excluirPorteAnestesico: vi.fn().mockReturnValue(of(undefined)),
    listarTabelaOrdem: vi.fn().mockReturnValue(of([])),
    salvarTabelaOrdem: vi.fn().mockReturnValue(of(undefined)),
    excluirTabelaOrdem: vi.fn().mockReturnValue(of(undefined)),
  };

  const routerSpy = {
    navigate: vi.fn().mockReturnValue(Promise.resolve(true)),
  };

  const activatedRouteSpy = {
    snapshot: { paramMap: { get: () => operadoraId } },
  };

  TestBed.configureTestingModule({
    imports: [OperadoraFormComponent],
    providers: [
      { provide: CatalogService, useValue: catalogServiceSpy },
      { provide: Router, useValue: routerSpy },
      { provide: ActivatedRoute, useValue: activatedRouteSpy },
    ],
  });

  const fixture = TestBed.createComponent(OperadoraFormComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    catalogService: catalogServiceSpy,
    router: routerSpy,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('OperadoraFormComponent', () => {
  describe('modo criação (sem id na rota)', () => {
    it('exibe título "Nova operadora"', () => {
      const { el } = setup(null);
      const title = el.querySelector('h1');
      const text = title?.textContent ?? '';
      expect(text.trim()).toBe('Nova operadora');
    });

    it('exibe erro de validação no campo nome ao tentar submeter vazio', () => {
      const { component, fixture, el } = setup(null);
      component.form.controls.nome.setValue('');
      component.salvar();
      fixture.detectChanges();
      const error = el.querySelector('.operadora-form__error--nome');
      const text = error?.textContent ?? '';
      expect(text.trim()).toBeTruthy();
    });

    it('exibe erro se registroAns não tiver 6 dígitos', () => {
      const { component, fixture, el } = setup(null);
      component.form.controls.nome.setValue('Teste');
      component.form.controls.registroAns.setValue('12');
      component.form.controls.registroAns.markAsTouched();
      fixture.detectChanges();
      const error = el.querySelector('.operadora-form__error--ans');
      const text = error?.textContent ?? '';
      expect(text.trim()).toBeTruthy();
    });

    it('exibe erro se cnpj não tiver 14 dígitos', () => {
      const { component, fixture, el } = setup(null);
      component.form.controls.nome.setValue('Teste');
      component.form.controls.cnpj.setValue('123');
      component.form.controls.cnpj.markAsTouched();
      fixture.detectChanges();
      const error = el.querySelector('.operadora-form__error--cnpj');
      const text = error?.textContent ?? '';
      expect(text.trim()).toBeTruthy();
    });

    it('chama service.criar e navega para a lista após salvar com sucesso', () => {
      const { component, catalogService, router } = setup(null);
      component.form.controls.nome.setValue('Nova Operadora');
      component.form.controls.tipoRuleSet.setValue('Unimed');
      component.salvar();
      expect(catalogService.criarOperadora).toHaveBeenCalledOnce();
      expect(router.navigate).toHaveBeenCalledWith(['/admin/catalog/operadoras']);
    });
  });

  describe('modo edição (id na rota)', () => {
    it('exibe título "Editar operadora"', () => {
      const { el } = setup('op-1');
      const title = el.querySelector('h1');
      const text = title?.textContent ?? '';
      expect(text.trim()).toBe('Editar operadora');
    });

    it('pré-preenche os campos com os dados carregados do service', () => {
      const { component } = setup('op-1');
      expect(component.form.controls.nome.value).toBe('UNIMED João Pessoa');
      expect(component.form.controls.registroAns.value).toBe('012345');
      expect(component.form.controls.cnpj.value).toBe('12345678000195');
      expect(component.form.controls.tipoRuleSet.value).toBe('Unimed');
      expect(component.form.controls.ativa.value).toBe(true);
    });

    it('chama service.atualizar ao salvar em modo edição', () => {
      const { component, catalogService, router } = setup('op-1');
      component.form.controls.nome.setValue('Nome Atualizado');
      component.salvar();
      expect(catalogService.atualizarOperadora).toHaveBeenCalledWith(
        'op-1',
        expect.objectContaining({ nome: 'Nome Atualizado' }),
      );
      expect(router.navigate).toHaveBeenCalledWith(['/admin/catalog/operadoras']);
    });

    it('operadora com tipoRuleSet Nulo não renderiza seção de portes anestésicos', () => {
      const operadoraNulo: OperadoraItem = { ...mockOperadora, tipoRuleSet: 'Nulo' };
      const { el } = setup('op-1', operadoraNulo);
      const secao = el.querySelector('app-portes-anestesicos');
      expect(secao).toBeNull();
    });

    it('operadora com tipoRuleSet Unimed renderiza seção de portes anestésicos', () => {
      const { el } = setup('op-1');
      const secao = el.querySelector('app-portes-anestesicos');
      expect(secao).not.toBeNull();
    });
  });

  describe('rótulo do tipo de rule set', () => {
    it('exibe opção "Sem cálculo" para o tipo Nulo no select', () => {
      const { el } = setup(null);
      const nulo = el.querySelector<HTMLOptionElement>('option[value="Nulo"]');
      const text = nulo?.textContent ?? '';
      expect(text.trim()).toBe('Sem cálculo');
    });

    it('não exibe "Sem apuração" em nenhum lugar do formulário', () => {
      const { el } = setup(null);
      expect(el.textContent).not.toContain('Sem apuração');
    });
  });
});
