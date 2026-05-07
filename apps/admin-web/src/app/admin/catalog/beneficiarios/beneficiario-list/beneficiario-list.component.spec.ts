import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { BeneficiarioListComponent } from './beneficiario-list.component';
import { CatalogService } from '../../catalog.service';
import type { BeneficiarioItem, ListarBeneficiariosResult } from '../../catalog.types';

const mockBeneficiarios: BeneficiarioItem[] = [
  { id: 'ben-1', carteira: '0001234567', nome: 'JOÃO SILVA', criadoEm: '2026-01-15T00:00:00Z' },
  { id: 'ben-2', carteira: '0009876543', nome: 'MARIA SOUZA', criadoEm: '2026-02-20T00:00:00Z' },
];

function makeResult(itens: BeneficiarioItem[] = mockBeneficiarios): ListarBeneficiariosResult {
  return { itens, total: itens.length, pagina: 1, itensPorPagina: 20 };
}

function setup(itens: BeneficiarioItem[] = mockBeneficiarios) {
  const catalogServiceSpy = {
    listarBeneficiarios: vi.fn().mockReturnValue(of(makeResult(itens))),
    atualizarBeneficiario: vi.fn().mockReturnValue(of(itens[0])),
    excluirBeneficiario: vi.fn().mockReturnValue(of(undefined)),
  };

  TestBed.configureTestingModule({
    imports: [BeneficiarioListComponent],
    providers: [{ provide: CatalogService, useValue: catalogServiceSpy }],
  });

  const fixture = TestBed.createComponent(BeneficiarioListComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    catalogService: catalogServiceSpy,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('BeneficiarioListComponent', () => {
  it('exibeListaDeBeneficiariosCarregadosAsync', () => {
    const { el } = setup();
    const rows = el.querySelectorAll('.beneficiario-list__row');
    expect(rows).toHaveLength(2);
    const cells = el.querySelectorAll('.beneficiario-list__cell');
    const textos = Array.from(cells).map((c) => c.textContent.trim());
    expect(textos).toContain('0001234567');
    expect(textos).toContain('JOÃO SILVA');
  });

  it('exibeMensagemQuandoListaEstiverVaziaAsync', () => {
    const { el } = setup([]);
    const empty = el.querySelector('.beneficiario-list__empty');
    expect((empty?.textContent ?? '').trim()).toBe('Nenhum beneficiário encontrado.');
  });

  it('filtroAltera_disparaNovaConsultaAsync', () => {
    vi.useFakeTimers();
    try {
      const { component, catalogService } = setup();
      catalogService.listarBeneficiarios.mockClear();

      component.onFiltroNomeChange('João');

      vi.advanceTimersByTime(399);
      expect(catalogService.listarBeneficiarios).not.toHaveBeenCalled();

      vi.advanceTimersByTime(1);
      expect(catalogService.listarBeneficiarios).toHaveBeenCalledWith(
        expect.objectContaining({ nome: 'João' }),
      );
    } finally {
      vi.useRealTimers();
    }
  });

  it('clicarEditarExibeInputDeNomeAsync', () => {
    const { el, fixture } = setup();
    const btns = Array.from(el.querySelectorAll('button'));
    const editarBtn = btns.find((b) => b.textContent.trim() === 'Editar');
    editarBtn?.click();
    fixture.detectChanges();

    const input = el.querySelector<HTMLInputElement>('.beneficiario-list__input-nome');
    expect(input).not.toBeNull();
    expect(input?.value).toBe('JOÃO SILVA');
  });

  it('salvarEdicaoChama_PUT_ERecarregaListaAsync', () => {
    const { el, fixture, catalogService } = setup();

    const btns = Array.from(el.querySelectorAll('button'));
    const editarBtn = btns.find((b) => b.textContent.trim() === 'Editar');
    editarBtn?.click();
    fixture.detectChanges();

    catalogService.listarBeneficiarios.mockClear();

    const salvarBtn = Array.from(el.querySelectorAll('button')).find(
      (b) => b.textContent.trim() === 'Salvar',
    );
    salvarBtn?.click();
    fixture.detectChanges();

    expect(catalogService.atualizarBeneficiario).toHaveBeenCalledWith('ben-1', {
      nome: 'JOÃO SILVA',
    });
    expect(catalogService.listarBeneficiarios).toHaveBeenCalledOnce();
  });

  it('clicarExcluirComConfirmacaoChama_DELETE_ERecarregaListaAsync', () => {
    const { el, fixture, catalogService } = setup();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    catalogService.listarBeneficiarios.mockClear();

    const btns = Array.from(el.querySelectorAll('button'));
    const excluirBtn = btns.find((b) => b.textContent.trim() === 'Excluir');
    excluirBtn?.click();
    fixture.detectChanges();

    expect(catalogService.excluirBeneficiario).toHaveBeenCalledWith('ben-1');
    expect(catalogService.listarBeneficiarios).toHaveBeenCalledOnce();
  });

  it('409 na exclusao exibe mensagem com "guias"', () => {
    const { el, fixture, catalogService } = setup();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    catalogService.excluirBeneficiario.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 409 })),
    );

    const btns = Array.from(el.querySelectorAll('button'));
    const excluirBtn = btns.find((b) => b.textContent.trim() === 'Excluir');
    excluirBtn?.click();
    fixture.detectChanges();

    const erro = el.querySelector('.beneficiario-list__erro');
    expect((erro?.textContent ?? '').trim()).toContain('guias');
  });
});
