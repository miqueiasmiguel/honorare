import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AdicionarItemModalComponent } from './adicionar-item-modal.component';
import { GuiaService } from '../../guia.service';
import type { ItemGuiaDisplay } from '../../guia.types';

const mockItem: ItemGuiaDisplay = {
  procedimentoId: 'proc-1',
  posicaoExecutor: 'Cirurgiao',
  viaAcesso: 'Convencional',
  acomodacao: 'Enfermaria',
  ehUrgencia: false,
  valorApurado: null,
};

function setup() {
  const guiaServiceSpy = {
    adicionarItem: vi.fn().mockReturnValue(of({ id: 'item-novo' })),
  };

  TestBed.configureTestingModule({
    imports: [AdicionarItemModalComponent],
    providers: [
      { provide: GuiaService, useValue: guiaServiceSpy },
      provideHttpClient(),
      provideHttpClientTesting(),
    ],
  });

  const fixture = TestBed.createComponent(AdicionarItemModalComponent);
  // Angular 20 JIT: setInput fails for signal inputs — replace the signals directly
  Object.assign(fixture.componentInstance, {
    guiaId: signal('guia-1'),
    operadoraId: signal('op-1'),
  });
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    guiaService: guiaServiceSpy,
    el: fixture.nativeElement as HTMLElement,
  };
}

describe('AdicionarItemModalComponent', () => {
  it('não renderiza conteúdo quando open=false', () => {
    const { el } = setup();

    expect(el.querySelector('.adicionar-item-modal__backdrop')).toBeNull();
  });

  it('renderiza conteúdo quando open=true', () => {
    const { fixture, el } = setup();
    Object.assign(fixture.componentInstance, { open: signal(true) });
    fixture.detectChanges();

    expect(el.querySelector('.adicionar-item-modal__backdrop')).not.toBeNull();
  });

  it('onItemChange(null) emite cancelado', () => {
    const { component } = setup();
    const cancelado = vi.fn();
    component.cancelado.subscribe(cancelado);

    component.onItemChange(null);

    expect(cancelado).toHaveBeenCalled();
  });

  it('onItemChange(item) chama adicionarItem e emite concluido no sucesso', () => {
    const { component, guiaService } = setup();
    const concluido = vi.fn();
    component.concluido.subscribe(concluido);

    component.onItemChange(mockItem);

    expect(guiaService.adicionarItem).toHaveBeenCalledWith('guia-1', {
      procedimentoId: 'proc-1',
      posicaoExecutor: 'Cirurgiao',
      viaAcesso: 'Convencional',
      acomodacao: 'Enfermaria',
      ehUrgencia: false,
      valorApurado: null,
      tempoAnestesicoMin: null,
    });
    expect(concluido).toHaveBeenCalled();
  });

  it('em erro do adicionarItem, seta a mensagem de erro', () => {
    const { component, guiaService } = setup();
    guiaService.adicionarItem.mockReturnValue(
      throwError(() => new HttpErrorResponse({ error: { detail: 'Item sem tabela.' } })),
    );

    component.onItemChange(mockItem);

    expect(component.erro()).toBe('Item sem tabela.');
  });

  it('em erro sem detail, usa mensagem padrão', () => {
    const { component, guiaService } = setup();
    guiaService.adicionarItem.mockReturnValue(
      throwError(() => new HttpErrorResponse({ error: null })),
    );

    component.onItemChange(mockItem);

    expect(component.erro()).toBe('Erro ao adicionar item. Verifique os dados e tente novamente.');
  });
});
