import { Component, inject, input, output, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { GuiaService } from '../../guia.service';
import { ItemGuiaFormComponent } from '../../guia-form/item-guia-form/item-guia-form.component';
import type { ItemGuiaDisplay } from '../../guia.types';

@Component({
  selector: 'app-adicionar-item-modal',
  imports: [ItemGuiaFormComponent],
  template: `
    @if (open()) {
      <div class="adicionar-item-modal__backdrop">
        <div class="adicionar-item-modal">
          <header class="adicionar-item-modal__header">
            <h2 class="adicionar-item-modal__title">Adicionar item</h2>
          </header>
          @if (erro()) {
            <p class="adicionar-item-modal__erro">{{ erro() }}</p>
          }
          <app-item-guia-form
            [ehPacote]="ehPacote()"
            [operadoraId]="operadoraId()"
            (itemChange)="onItemChange($event)"
          />
        </div>
      </div>
    }
  `,
  styleUrl: './adicionar-item-modal.component.scss',
})
export class AdicionarItemModalComponent {
  readonly open = input(false);
  readonly guiaId = input('');
  readonly operadoraId = input('');
  readonly ehPacote = input(false);

  readonly concluido = output();
  readonly cancelado = output();

  private readonly _guiaService = inject(GuiaService);
  readonly erro = signal('');

  onItemChange(item: ItemGuiaDisplay | null): void {
    if (item === null) {
      this.cancelado.emit();
      return;
    }
    this.erro.set('');
    this._guiaService
      .adicionarItem(this.guiaId(), {
        procedimentoId: item.procedimentoId,
        posicaoExecutor: item.posicaoExecutor,
        viaAcesso: item.viaAcesso,
        acomodacao: item.acomodacao,
        ehUrgencia: item.ehUrgencia,
        valorApurado: item.valorApurado,
        tempoAnestesicoMin: item.tempoAnestesicoMin ?? null,
      })
      .subscribe({
        next: () => {
          this.concluido.emit();
        },
        error: (err: HttpErrorResponse) => {
          this.erro.set(
            (err.error as { detail?: string } | null)?.detail ??
              'Erro ao adicionar item. Verifique os dados e tente novamente.',
          );
        },
      });
  }
}
