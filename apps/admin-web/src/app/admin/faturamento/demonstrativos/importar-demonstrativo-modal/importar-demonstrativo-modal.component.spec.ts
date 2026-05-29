import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { DemonstrativoService } from '../../demonstrativo.service';
import { CatalogService } from '../../../catalog/catalog.service';
import type { ResultadoImportacaoDto } from '../../demonstrativo.types';
import type { OperadoraItem, PrestadorItem } from '../../../catalog/catalog.types';
import { ImportarDemonstrativoModalComponent } from './importar-demonstrativo-modal.component';

const mockPrestadores: PrestadorItem[] = [
  {
    id: 'prest-1',
    nome: 'Dr. João',
    registroProfissional: null,
    ativo: true,
    criadoEm: '2026-01-01',
    emailAcesso: null,
    temUsuario: false,
  },
];

const mockOperadoras: OperadoraItem[] = [
  {
    id: 'op-1',
    nome: 'UNIMED JPA',
    registroAns: null,
    cnpj: null,
    tipoRuleSet: 'Unimed',
    ativa: true,
    criadaEm: '2026-01-01',
  },
];

const mockPreviewSemErros: ResultadoImportacaoDto = {
  identificadorPagamento: 'PAG-2026-04',
  somenteValidar: true,
  demonstrativoId: null,
  guiasCriadas: 0,
  guiasAtualizadas: 0,
  itensCriados: 0,
  itensAtualizados: 0,
  itensIgnorados: 0,
  beneficiariosCriados: 0,
  guiasPrevistas: 3,
  itensPrevistas: 5,
  erros: [],
  alertas: [],
};

const mockPreviewComErros: ResultadoImportacaoDto = {
  ...mockPreviewSemErros,
  erros: [{ linha: 10, mensagem: 'Procedimento TUSS não encontrado' }],
  alertas: [{ linha: 7, mensagem: 'SemTabela para procedimento' }],
};

const mockPreviewSoAlertas: ResultadoImportacaoDto = {
  ...mockPreviewSemErros,
  alertas: [{ linha: 7, mensagem: 'SemTabela para procedimento' }],
};

const mockImportacaoFinal: ResultadoImportacaoDto = {
  identificadorPagamento: 'PAG-2026-04',
  somenteValidar: false,
  demonstrativoId: 'demo-uuid',
  guiasCriadas: 3,
  guiasAtualizadas: 0,
  itensCriados: 5,
  itensAtualizados: 0,
  itensIgnorados: 1,
  beneficiariosCriados: 2,
  guiasPrevistas: 0,
  itensPrevistas: 0,
  erros: [],
  alertas: [],
};

function setup(previewResult: ResultadoImportacaoDto = mockPreviewSemErros) {
  const demoService = {
    importarCsv: vi
      .fn()
      .mockImplementation((_f: File, _p: string, _o: string, somenteValidar: boolean) =>
        of(somenteValidar ? previewResult : mockImportacaoFinal),
      ),
  };
  const catalogService = {
    listarPrestadores: vi
      .fn()
      .mockReturnValue(of({ itens: mockPrestadores, total: 1, pagina: 1, itensPorPagina: 200 })),
    listarOperadoras: vi
      .fn()
      .mockReturnValue(of({ itens: mockOperadoras, total: 1, pagina: 1, itensPorPagina: 200 })),
  };

  TestBed.configureTestingModule({
    imports: [ImportarDemonstrativoModalComponent],
    providers: [
      { provide: DemonstrativoService, useValue: demoService },
      { provide: CatalogService, useValue: catalogService },
    ],
  });

  const fixture = TestBed.createComponent(ImportarDemonstrativoModalComponent);
  fixture.componentRef.setInput('open', true);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    demoService,
    el: fixture.nativeElement as HTMLElement,
  };
}

function makeFile(name = 'demo.csv'): File {
  return new File(['GUIA;CODIGO PROCEDIMENTO\n001;30715013'], name, { type: 'text/csv' });
}

describe('ImportarDemonstrativoModalComponent', () => {
  it('Exibe_BotaoImportar_AbrirModal', () => {
    const { el } = setup();
    expect(el.querySelector('.importar-demonstrativo-modal__input-arquivo')).not.toBeNull();
    expect(el.querySelector('.importar-demonstrativo-modal__btn-validar')).not.toBeNull();
  });

  it('Selecionar_Arquivo_HabilitaBotaoEnviar', () => {
    const { component, fixture, el } = setup();

    const btn = el.querySelector<HTMLButtonElement>('.importar-demonstrativo-modal__btn-validar');
    expect(btn?.disabled).toBe(true);

    component.onArquivoSelecionado(makeFile());
    component.prestadorId.set('prest-1');
    component.operadoraId.set('op-1');
    fixture.detectChanges();

    expect(btn?.disabled).toBe(false);
  });

  it('SomenteValidar_True_ExibePreview_NaoFechaModal', () => {
    const { component, fixture, el, demoService } = setup();

    component.onArquivoSelecionado(makeFile());
    component.prestadorId.set('prest-1');
    component.operadoraId.set('op-1');
    component.validar();
    fixture.detectChanges();

    expect(demoService.importarCsv).toHaveBeenCalledWith(expect.any(File), 'prest-1', 'op-1', true);
    expect(el.querySelector('.importar-demonstrativo-modal__preview')).not.toBeNull();
    expect(el.querySelector('.importar-demonstrativo-modal__resumo-final')).toBeNull();
  });

  it('Importacao_Completa_ExibeResumoEFechaModal', () => {
    const importacaoConcluidaSpy = vi.fn();
    const { component, fixture, el } = setup();

    component.importacaoConcluida.subscribe(importacaoConcluidaSpy);

    component.onArquivoSelecionado(makeFile());
    component.prestadorId.set('prest-1');
    component.operadoraId.set('op-1');
    component.validar();
    fixture.detectChanges();

    component.confirmar();
    fixture.detectChanges();

    expect(importacaoConcluidaSpy).toHaveBeenCalled();
    expect(el.querySelector('.importar-demonstrativo-modal__resumo-final')).not.toBeNull();
  });

  it('Erros_Exibidos_PorLinha_ComMensagem', () => {
    const { component, fixture, el } = setup(mockPreviewComErros);

    component.onArquivoSelecionado(makeFile());
    component.prestadorId.set('prest-1');
    component.operadoraId.set('op-1');
    component.validar();
    fixture.detectChanges();

    const erros = el.querySelectorAll('.importar-demonstrativo-modal__erro-item');
    expect(erros.length).toBeGreaterThan(0);
    expect(erros[0].textContent).toContain('10');
    expect(erros[0].textContent).toContain('Procedimento TUSS não encontrado');
  });

  it('Alertas_Exibidos_ComCorAmbar', () => {
    const { component, fixture, el } = setup(mockPreviewSoAlertas);

    component.onArquivoSelecionado(makeFile());
    component.prestadorId.set('prest-1');
    component.operadoraId.set('op-1');
    component.validar();
    fixture.detectChanges();

    const alertas = el.querySelectorAll('.importar-demonstrativo-modal__alerta-item');
    expect(alertas.length).toBeGreaterThan(0);
    expect(alertas[0].textContent).toContain('7');
    expect(alertas[0].textContent).toContain('SemTabela para procedimento');
  });
});
