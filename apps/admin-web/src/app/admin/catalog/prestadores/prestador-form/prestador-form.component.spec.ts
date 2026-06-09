import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ActivatedRoute, Router } from '@angular/router';
import { PrestadorFormComponent } from './prestador-form.component';
import { CatalogService } from '../../catalog.service';
import type { PrestadorItem } from '../../catalog.types';

const mockPrestador: PrestadorItem = {
  id: 'prest-1',
  nome: 'Dr. José Silva',
  registroProfissional: 'CRM-12345',
  ativo: true,
  criadoEm: '2026-01-01T00:00:00Z',
  emailAcesso: 'jose@example.com',
  temUsuario: true,
};

function setup(prestadorId: string | null = null, prestador: PrestadorItem = mockPrestador) {
  const catalogServiceSpy = {
    obterPrestador: vi.fn().mockReturnValue(of(prestador)),
    criarPrestador: vi.fn().mockReturnValue(of(prestador)),
    atualizarPrestador: vi.fn().mockReturnValue(of(prestador)),
    definirEmailAcesso: vi.fn().mockReturnValue(of(prestador)),
  };

  const routerSpy = {
    navigate: vi.fn().mockReturnValue(Promise.resolve(true)),
  };

  const activatedRouteSpy = {
    snapshot: { paramMap: { get: () => prestadorId } },
  };

  TestBed.configureTestingModule({
    imports: [PrestadorFormComponent],
    providers: [
      { provide: CatalogService, useValue: catalogServiceSpy },
      { provide: Router, useValue: routerSpy },
      { provide: ActivatedRoute, useValue: activatedRouteSpy },
    ],
  });

  const fixture = TestBed.createComponent(PrestadorFormComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    catalogService: catalogServiceSpy,
    router: routerSpy,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('PrestadorFormComponent', () => {
  describe('modo criação (sem id na rota)', () => {
    it('submit sem nome exibe erro de validação', () => {
      const { component, fixture, el } = setup(null);
      component.form.controls.nome.setValue('');
      component.salvar();
      fixture.detectChanges();
      const error = el.querySelector('.prestador-form__error--nome');
      const text = error?.textContent ?? '';
      expect(text.trim()).toBeTruthy();
    });

    it('submit válido chama POST com payload correto', () => {
      const { component, catalogService, router } = setup(null);
      component.form.controls.nome.setValue('Dr. José Silva');
      component.form.controls.registroProfissional.setValue('CRM-12345');
      component.salvar();
      expect(catalogService.criarPrestador).toHaveBeenCalledWith({
        nome: 'Dr. José Silva',
        registroProfissional: 'CRM-12345',
        emailAcesso: null,
      });
      expect(router.navigate).toHaveBeenCalledWith(['/admin/catalog/prestadores']);
    });

    it('modo criação exibe campo emailAcesso editável', () => {
      const { el } = setup(null);
      const input = el.querySelector('#emailAcesso');
      expect(input).not.toBeNull();
    });

    it('modo criação envia emailAcesso preenchido no payload', () => {
      const { component, catalogService } = setup(null);
      component.form.controls.nome.setValue('Dr. José Silva');
      component.form.controls.emailAcesso.setValue('jose@example.com');
      component.salvar();
      expect(catalogService.criarPrestador).toHaveBeenCalledWith(
        expect.objectContaining({ emailAcesso: 'jose@example.com' }),
      );
    });

    it('modo criação envia null quando emailAcesso vazio', () => {
      const { component, catalogService } = setup(null);
      component.form.controls.nome.setValue('Dr. José Silva');
      component.salvar();
      expect(catalogService.criarPrestador).toHaveBeenCalledWith(
        expect.objectContaining({ emailAcesso: null }),
      );
    });
  });

  describe('modo edição (id na rota)', () => {
    it('edição carrega dados e chama PUT', () => {
      const { component, catalogService, router } = setup('prest-1');
      expect(component.form.controls.nome.value).toBe('Dr. José Silva');
      component.salvar();
      expect(catalogService.atualizarPrestador).toHaveBeenCalledWith(
        'prest-1',
        expect.objectContaining({ nome: 'Dr. José Silva' }),
      );
      expect(router.navigate).toHaveBeenCalledWith(['/admin/catalog/prestadores']);
    });

    it('modo edição não exibe campo emailAcesso editável', () => {
      const { el } = setup('prest-1');
      const input = el.querySelector('#emailAcesso');
      expect(input).toBeNull();
    });

    it('modo edição exibe emailAcesso como texto somente-leitura', () => {
      const { el } = setup('prest-1');
      const valor = el.querySelector('.prestador-form__email-acesso-valor');
      const text = valor?.textContent ?? '';
      expect(text.trim()).toBe('jose@example.com');
    });

    it('modo edição exibe badge "Com acesso" quando temUsuario = true', () => {
      const { el } = setup('prest-1');
      const badge = el.querySelector('.prestador-form__badge-acesso');
      expect(badge).not.toBeNull();
      const text = badge?.textContent ?? '';
      expect(text.trim()).toBe('Com acesso ao portal');
    });

    it('modo edição exibe formulário de e-mail de acesso quando emailAcesso é null', () => {
      const mockSemAcesso: PrestadorItem = {
        ...mockPrestador,
        emailAcesso: null,
        temUsuario: false,
      };
      const { el } = setup('prest-1', mockSemAcesso);
      const badge = el.querySelector('.prestador-form__badge-acesso');
      expect(badge).toBeNull();
      const valor = el.querySelector('.prestador-form__email-acesso-valor');
      expect(valor).toBeNull();
      const emailForm = el.querySelector('.prestador-form__email-acesso-form');
      expect(emailForm).not.toBeNull();
    });
  });
});
