import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ProfilePage } from './profile-page';
import { AdminService } from '../admin.service';
import type { ProfileSummary } from '../admin.types';

const mockProfile: ProfileSummary = {
  id: 'user-1',
  email: 'admin@test.com',
  nome: 'Dr. Admin',
  role: 'TenantAdmin',
};

function setup(profile: ProfileSummary = mockProfile) {
  const adminServiceSpy = {
    getProfile: vi.fn().mockReturnValue(of(profile)),
    updateProfile: vi.fn().mockReturnValue(of({ ...profile, nome: 'Novo Nome' })),
  };

  TestBed.configureTestingModule({
    imports: [ProfilePage],
    providers: [{ provide: AdminService, useValue: adminServiceSpy }],
  });

  const fixture = TestBed.createComponent(ProfilePage);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, adminService: adminServiceSpy };
}

describe('ProfilePage', () => {
  it('carrega perfil na inicialização e preenche formulário', () => {
    const { component } = setup();
    expect(component.profile()).toMatchObject({ email: 'admin@test.com' });
    expect(component.form.controls.nome.value).toBe('Dr. Admin');
  });

  it('preenche nome vazio quando perfil tem nome null', () => {
    const { component } = setup({ ...mockProfile, nome: null });
    expect(component.form.controls.nome.value).toBe('');
  });

  it('exibe e-mail como somente leitura', () => {
    const { fixture } = setup();
    const el = fixture.nativeElement as HTMLElement;
    const firstValue = el.querySelector('.profile__value');
    const text = firstValue?.textContent ?? '';
    expect(text.trim()).toBe('admin@test.com');
  });

  it('submit chama updateProfile com o nome do formulário', () => {
    const { component, adminService } = setup();
    component.form.controls.nome.setValue('Novo Nome');
    component.submit();
    expect(adminService.updateProfile).toHaveBeenCalledWith({ nome: 'Novo Nome' });
  });

  it('submit não é chamado quando formulário é inválido', () => {
    const { component, adminService } = setup();
    component.form.controls.nome.setValue('');
    component.submit();
    expect(adminService.updateProfile).not.toHaveBeenCalled();
  });

  it('saved é true após submit bem-sucedido', () => {
    const { component } = setup();
    component.form.controls.nome.setValue('Novo Nome');
    component.submit();
    expect(component.saved()).toBe(true);
  });

  it('atualiza sinal profile após submit bem-sucedido', () => {
    const { component } = setup();
    component.form.controls.nome.setValue('Novo Nome');
    component.submit();
    expect(component.profile()?.nome).toBe('Novo Nome');
  });
});
