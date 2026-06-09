import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ConfiguracoesPage } from './configuracoes-page';
import { TenantService } from './tenant.service';
import { CatalogService } from '../catalog/catalog.service';
import type { TenantSettings } from './tenant.types';
import type { ProcedimentoItem } from '../catalog/catalog.types';

beforeAll(() => {
  vi.stubGlobal('URL', {
    createObjectURL: vi.fn().mockReturnValue('blob:fake-url'),
    revokeObjectObject: vi.fn(),
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
  codigosNaoRecorriveis: [],
};

function setup(settings: TenantSettings = SETTINGS, catalogItens: ProcedimentoItem[] = []) {
  const tenantServiceSpy = {
    getSettings: vi.fn().mockReturnValue(of(settings)),
    rename: vi.fn().mockReturnValue(of({ ...settings })),
    uploadLogo: vi.fn().mockReturnValue(of({ ...settings, hasLogo: true })),
    downloadLogo: vi.fn().mockReturnValue(of(new Blob(['img']))),
    deleteLogo: vi.fn().mockReturnValue(of(null)),
    atualizarCodigosNaoRecorriveis: vi.fn().mockReturnValue(of({ ...settings })),
  };

  const catalogServiceSpy = {
    listarProcedimentos: vi
      .fn()
      .mockReturnValue(
        of({ itens: catalogItens, total: catalogItens.length, pagina: 1, itensPorPagina: 200 }),
      ),
  };

  TestBed.configureTestingModule({
    imports: [ConfiguracoesPage],
    providers: [
      { provide: TenantService, useValue: tenantServiceSpy },
      { provide: CatalogService, useValue: catalogServiceSpy },
    ],
  });

  const fixture = TestBed.createComponent(ConfiguracoesPage);
  fixture.detectChanges();
  return {
    fixture,
    component: fixture.componentInstance,
    tenantService: tenantServiceSpy,
    catalogService: catalogServiceSpy,
  };
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

  it('selecionarArquivo abre o modal de recorte com arquivo válido (sem enviar)', () => {
    const { component, tenantService } = setup();
    const file = new File(['img'], 'logo.png', { type: 'image/png' });
    const event = { target: { files: [file] } } as unknown as Event;
    component.selecionarArquivo(event);
    expect(component.arquivoParaRecorte()).toBe(file);
    expect(tenantService.uploadLogo).not.toHaveBeenCalled();
  });

  it('selecionarArquivo não abre o modal para tipo inválido', () => {
    const { component, tenantService } = setup();
    const file = new File(['data'], 'logo.gif', { type: 'image/gif' });
    const event = { target: { files: [file] } } as unknown as Event;
    component.selecionarArquivo(event);
    expect(component.arquivoParaRecorte()).toBeNull();
    expect(tenantService.uploadLogo).not.toHaveBeenCalled();
    expect(component.erroValidacao()).toContain('PNG ou JPEG');
  });

  it('aoRecortar fecha o modal e envia a logo recortada', () => {
    const { component, tenantService } = setup();
    const file = new File(['img'], 'logo.png', { type: 'image/png' });
    component.arquivoParaRecorte.set(file);
    component.aoRecortar(file);
    expect(component.arquivoParaRecorte()).toBeNull();
    expect(tenantService.uploadLogo).toHaveBeenCalledWith(file);
  });

  it('aoCancelarRecorte fecha o modal sem enviar', () => {
    const { component, tenantService } = setup();
    component.arquivoParaRecorte.set(new File(['img'], 'logo.png', { type: 'image/png' }));
    component.aoCancelarRecorte();
    expect(component.arquivoParaRecorte()).toBeNull();
    expect(tenantService.uploadLogo).not.toHaveBeenCalled();
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

describe('procedimentos não recorríveis', () => {
  it('deve carregar códigos não recorríveis e resolver descrições no init', () => {
    const proc: ProcedimentoItem = {
      id: 'p1',
      codigoTuss: '10101012',
      descricao: 'Consulta Médica',
      porte: null,
      porteAnestesico: null,
      ehSadt: false,
      temPorteProprioVideo: false,
      ativo: true,
      criadoEm: '2026-01-01',
    };
    const { component, catalogService } = setup(
      { ...SETTINGS, codigosNaoRecorriveis: ['10101012'] },
      [proc],
    );
    expect(catalogService.listarProcedimentos).toHaveBeenCalled();
    expect(component.naoRecorriveis()).toEqual([
      { codigoTuss: '10101012', descricao: 'Consulta Médica' },
    ]);
  });

  it('deve adicionar um procedimento selecionado na busca à lista', () => {
    const { component } = setup();
    component.adicionarNaoRecorrivel({ codigoTuss: '10101234', descricao: 'Procedimento X' });
    expect(component.naoRecorriveis()).toEqual([
      { codigoTuss: '10101234', descricao: 'Procedimento X' },
    ]);
  });

  it('deve remover um procedimento da lista', () => {
    const { component } = setup();
    component.adicionarNaoRecorrivel({ codigoTuss: '10101012', descricao: 'Consulta' });
    component.removerNaoRecorrivel('10101012');
    expect(component.naoRecorriveis()).toEqual([]);
  });

  it('deve salvar enviando apenas os códigos TUSS', () => {
    const { component, tenantService } = setup();
    component.adicionarNaoRecorrivel({ codigoTuss: '10101012', descricao: 'Consulta' });
    component.salvarNaoRecorriveis();
    expect(tenantService.atualizarCodigosNaoRecorriveis).toHaveBeenCalledWith(['10101012']);
  });
});
