import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ConfiguracoesPage } from './configuracoes-page';
import { TenantService } from './tenant.service';
import type { TenantSettings } from './tenant.types';

beforeAll(() => {
  vi.stubGlobal('URL', {
    createObjectURL: vi.fn().mockReturnValue('blob:fake-url'),
    revokeObjectURL: vi.fn(),
  });
});

afterAll(() => {
  vi.unstubAllGlobals();
});

const SETTINGS: TenantSettings = {
  id: 'tenant-1',
  name: 'Clínica Alpha',
  hasLogo: false,
};

function setup(settings = SETTINGS) {
  const tenantServiceSpy = {
    getSettings: vi.fn().mockReturnValue(of(settings)),
    rename: vi.fn().mockReturnValue(of({ ...settings })),
    uploadLogo: vi.fn().mockReturnValue(of({ ...settings, hasLogo: true })),
    downloadLogo: vi.fn().mockReturnValue(of(new Blob(['img']))),
    deleteLogo: vi.fn().mockReturnValue(of(null)),
  };

  TestBed.configureTestingModule({
    imports: [ConfiguracoesPage],
    providers: [{ provide: TenantService, useValue: tenantServiceSpy }],
  });

  const fixture = TestBed.createComponent(ConfiguracoesPage);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, tenantService: tenantServiceSpy };
}

describe('ConfiguracoesPage', () => {
  it('carrega settings na inicialização e preenche formulário', () => {
    const { component } = setup();
    expect(component.settings()).toMatchObject({ name: 'Clínica Alpha' });
    expect(component.form.controls.nome.value).toBe('Clínica Alpha');
  });

  it('carrega logo quando hasLogo é true', () => {
    const { tenantService } = setup({ ...SETTINGS, hasLogo: true });
    expect(tenantService.downloadLogo).toHaveBeenCalled();
  });

  it('submit chama rename com o nome do formulário', () => {
    const { component, tenantService } = setup();
    component.form.controls.nome.setValue('Novo Nome');
    component.submit();
    expect(tenantService.rename).toHaveBeenCalledWith('Novo Nome');
  });

  it('submit não chamado quando formulário inválido', () => {
    const { component, tenantService } = setup();
    component.form.controls.nome.setValue('');
    component.submit();
    expect(tenantService.rename).not.toHaveBeenCalled();
  });

  it('saved é true após submit bem-sucedido', () => {
    const { component } = setup();
    component.form.controls.nome.setValue('Novo Nome');
    component.submit();
    expect(component.saved()).toBe(true);
  });

  it('selecionarArquivo chama uploadLogo com arquivo válido', () => {
    const { component, tenantService } = setup();
    const file = new File(['img'], 'logo.png', { type: 'image/png' });
    const event = { target: { files: [file] } } as unknown as Event;
    component.selecionarArquivo(event);
    expect(tenantService.uploadLogo).toHaveBeenCalledWith(file);
  });

  it('selecionarArquivo não chama uploadLogo para tipo inválido', () => {
    const { component, tenantService } = setup();
    const file = new File(['data'], 'logo.gif', { type: 'image/gif' });
    const event = { target: { files: [file] } } as unknown as Event;
    component.selecionarArquivo(event);
    expect(tenantService.uploadLogo).not.toHaveBeenCalled();
    expect(component.erroValidacao()).toContain('PNG ou JPEG');
  });

  it('removerLogo chama deleteLogo', () => {
    const { component, tenantService } = setup();
    component.removerLogo();
    expect(tenantService.deleteLogo).toHaveBeenCalled();
  });

  it('removerLogo limpa logoUrl e hasLogo após sucesso', () => {
    const { component } = setup({ ...SETTINGS, hasLogo: true });
    component.removerLogo();
    expect(component.logoUrl()).toBeNull();
    expect(component.settings()?.hasLogo).toBe(false);
  });
});
