import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ActivatedRoute, Router } from '@angular/router';
import { ProcedimentoFormComponent } from './procedimento-form.component';
import { CatalogService } from '../../catalog.service';
import type { ProcedimentoItem } from '../../catalog.types';

const mockProcedimento: ProcedimentoItem = {
  id: 'proc-1',
  codigoTuss: '30715013',
  descricao: 'Herniorrafia inguinal',
  porte: '6B',
  porteAnestesico: 4,
  ehSadt: false,
  temPorteProprioVideo: true,
  ativo: true,
  criadoEm: '2026-01-01T00:00:00Z',
};

function setup(
  procedimentoId: string | null = null,
  procedimento: ProcedimentoItem = mockProcedimento,
) {
  const catalogServiceSpy = {
    obterProcedimento: vi.fn().mockReturnValue(of(procedimento)),
    criarProcedimento: vi.fn().mockReturnValue(of(mockProcedimento)),
    atualizarProcedimento: vi.fn().mockReturnValue(of(mockProcedimento)),
  };

  const routerSpy = {
    navigate: vi.fn().mockReturnValue(Promise.resolve(true)),
  };

  const activatedRouteSpy = {
    snapshot: { paramMap: { get: () => procedimentoId } },
  };

  TestBed.configureTestingModule({
    imports: [ProcedimentoFormComponent],
    providers: [
      { provide: CatalogService, useValue: catalogServiceSpy },
      { provide: Router, useValue: routerSpy },
      { provide: ActivatedRoute, useValue: activatedRouteSpy },
    ],
  });

  const fixture = TestBed.createComponent(ProcedimentoFormComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    catalogService: catalogServiceSpy,
    router: routerSpy,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('ProcedimentoFormComponent', () => {
  describe('modo criação (sem id na rota)', () => {
    it('exibe erro se codigoTuss ultrapassar 10 caracteres', () => {
      const { component, fixture, el } = setup(null);
      component.form.controls.codigoTuss.setValue('12345678901');
      component.form.controls.codigoTuss.markAsTouched();
      fixture.detectChanges();
      const error = el.querySelector('.procedimento-form__error--codigo-tuss');
      const text = error?.textContent ?? '';
      expect(text.trim()).toBeTruthy();
    });

    it('exibe erro se codigoTuss estiver vazio ao submeter', () => {
      const { component, fixture, el } = setup(null);
      component.form.controls.codigoTuss.setValue('');
      component.salvar();
      fixture.detectChanges();
      const error = el.querySelector('.procedimento-form__error--codigo-tuss');
      const text = error?.textContent ?? '';
      expect(text.trim()).toBeTruthy();
    });

    it('exibe erro se descricao estiver vazia ao submeter', () => {
      const { component, fixture, el } = setup(null);
      component.form.controls.descricao.setValue('');
      component.salvar();
      fixture.detectChanges();
      const error = el.querySelector('.procedimento-form__error--descricao');
      const text = error?.textContent ?? '';
      expect(text.trim()).toBeTruthy();
    });

    it('exibe erro se porteAnestesico for maior que 8', () => {
      const { component, fixture, el } = setup(null);
      component.form.controls.porteAnestesico.setValue(9);
      component.form.controls.porteAnestesico.markAsTouched();
      expect(component.form.controls.porteAnestesico.hasError('max')).toBe(true);
      fixture.detectChanges();
      const error = el.querySelector('.procedimento-form__error--porte-anestesico');
      const text = error?.textContent ?? '';
      expect(text.trim()).toBeTruthy();
    });

    it('campo "SADT" possui atributo title com texto explicativo', () => {
      const { el } = setup(null);
      const input = el.querySelector<HTMLInputElement>('#ehSadt');
      expect(input?.title).toBeTruthy();
      expect(input?.title).toContain('urgência');
    });

    it('campo "Vídeo próprio" possui atributo title com texto explicativo', () => {
      const { el } = setup(null);
      const input = el.querySelector<HTMLInputElement>('#temPorteProprioVideo');
      expect(input?.title).toBeTruthy();
      expect(input?.title).toContain('videolaparoscopia');
    });

    it('toggle "Ativo" reflete o valor do formulário', () => {
      const { component, fixture, el } = setup(null);
      component.form.controls.ativo.setValue(false);
      fixture.detectChanges();
      const input = el.querySelector<HTMLInputElement>('#ativo');
      expect(input?.checked).toBe(false);
    });

    it('chama service.criar em modo criação e navega para lista', () => {
      const { component, catalogService, router } = setup(null);
      component.form.controls.codigoTuss.setValue('30715013');
      component.form.controls.descricao.setValue('Herniorrafia inguinal');
      component.salvar();
      expect(catalogService.criarProcedimento).toHaveBeenCalledOnce();
      expect(router.navigate).toHaveBeenCalledWith(['/admin/catalog/procedimentos']);
    });
  });

  describe('modo edição (id na rota)', () => {
    it('pré-preenche campos em modo edição', () => {
      const { component } = setup('proc-1');
      expect(component.form.controls.codigoTuss.value).toBe('30715013');
      expect(component.form.controls.descricao.value).toBe('Herniorrafia inguinal');
      expect(component.form.controls.porte.value).toBe('6B');
      expect(component.form.controls.porteAnestesico.value).toBe(4);
      expect(component.form.controls.ehSadt.value).toBe(false);
      expect(component.form.controls.temPorteProprioVideo.value).toBe(true);
      expect(component.form.controls.ativo.value).toBe(true);
    });

    it('chama service.atualizar em modo edição e navega para lista', () => {
      const { component, catalogService, router } = setup('proc-1');
      component.form.controls.descricao.setValue('Descrição atualizada');
      component.salvar();
      expect(catalogService.atualizarProcedimento).toHaveBeenCalledWith(
        'proc-1',
        expect.objectContaining({ descricao: 'Descrição atualizada' }),
      );
      expect(router.navigate).toHaveBeenCalledWith(['/admin/catalog/procedimentos']);
    });
  });
});
