import { Component, input, signal } from '@angular/core';
import type { GuiaCalculoResult, ItemCalculoItem } from '../guia.types';

@Component({
  selector: 'app-calculo-detalhe',
  template: `
    @if (calculo(); as calc) {
      <div class="calculo-detalhe">
        @for (item of calc.itens; track item.itemGuiaId) {
          <div class="calculo-detalhe__item">
            <button type="button" class="calculo-detalhe__header" (click)="toggle(item.itemGuiaId)">
              {{ item.codigoTuss }} — {{ item.descricaoProcedimento }}
              <span [class]="'badge ' + badgeClass(item.situacao)">{{ item.situacao }}</span>
              @if (item.valorApurado !== null) {
                <span class="calculo-detalhe__valor">{{ item.valorApurado }}</span>
              } @else {
                <span class="calculo-detalhe__valor">—</span>
              }
            </button>
            @if (aberto() === item.itemGuiaId) {
              <div class="calculo-detalhe__body">
                @if (item.passos.length > 0) {
                  <table class="calculo-detalhe__tabela">
                    <thead>
                      <tr>
                        <th>Regra</th>
                        <th>Fator</th>
                        <th>Valor Resultante</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (passo of item.passos; track $index) {
                        <tr class="calculo-detalhe__passo">
                          <td>{{ passo.regra }}</td>
                          <td>{{ passo.fator }}</td>
                          <td>{{ passo.valorResultante }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                } @else {
                  <p class="calculo-detalhe__sem-passos">Sem detalhes de cálculo</p>
                }
              </div>
            }
          </div>
        }
      </div>
    }
  `,
  styleUrl: './calculo-detalhe.component.scss',
})
export class CalculoDetalheComponent {
  readonly calculo = input<GuiaCalculoResult | null>(null);
  readonly aberto = signal<string | null>(null);

  toggle(id: string): void {
    this.aberto.set(this.aberto() === id ? null : id);
  }

  badgeClass(situacao: ItemCalculoItem['situacao']): string {
    const map: Record<ItemCalculoItem['situacao'], string> = {
      Calculado: 'badge--calculado',
      SemTabela: 'badge--sem-tabela',
      Indeterminado: 'badge--indeterminado',
      Pacote: 'badge--pacote',
    };
    return map[situacao];
  }
}
