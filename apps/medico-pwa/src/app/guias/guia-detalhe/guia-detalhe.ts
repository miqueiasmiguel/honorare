import { DecimalPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MedicoGuiaService } from '../medico-guia.service';
import type { MedicoGuiaDetalheDto, SituacaoCalculo } from '../medico-guia.types';

@Component({
  selector: 'app-guia-detalhe',
  imports: [DecimalPipe],
  template: `
    @if (loading()) {
      <div class="guia-detalhe__loading">Carregando...</div>
    } @else if (detalhe(); as d) {
      <div class="guia-detalhe">
        <header class="guia-detalhe__header">
          <span class="guia-detalhe__operadora">{{ d.operadoraNome }}</span>
          <span class="guia-detalhe__beneficiario">{{ d.beneficiarioNome ?? '—' }}</span>
          <span class="guia-detalhe__carteira">{{ d.beneficiarioCarteira ?? '—' }}</span>
          <span class="guia-detalhe__data">{{ d.dataAtendimento }}</span>
          <span class="guia-detalhe__senha">{{ d.senha ?? '—' }}</span>
          <span [class]="badgeClassSituacao(d.situacao)">{{ d.situacao }}</span>
        </header>

        <div class="guia-detalhe__observacao">
          <span class="guia-detalhe__observacao-label">Observação do responsável</span>
          @if (d.observacao) {
            <span class="guia-detalhe__observacao-texto">{{ d.observacao }}</span>
          } @else {
            <span class="guia-detalhe__observacao-vazio">Nenhuma observação registrada.</span>
          }
        </div>

        <table class="guia-detalhe__tabela">
          <thead>
            <tr>
              <th>Cód. TUSS</th>
              <th>Descrição</th>
              <th>Posição</th>
              <th>VL Apurado</th>
              <th>VL Pago</th>
              <th>Situação</th>
            </tr>
          </thead>
          <tbody>
            @for (item of d.itens; track item.id) {
              <tr class="guia-detalhe__item-row">
                <td>{{ item.codigoTuss }}</td>
                <td>{{ item.descricao }}</td>
                <td>{{ item.posicao }}</td>
                <td class="guia-detalhe__valor-mono">{{ item.valorApurado | number: '1.2-2' }}</td>
                <td class="guia-detalhe__valor-mono">{{ item.valorPago | number: '1.2-2' }}</td>
                <td>
                  <span [class]="badgeClassCalculo(item.situacaoCalculo)">{{
                    item.situacaoCalculo
                  }}</span>
                </td>
              </tr>
            }
          </tbody>
        </table>

        <footer class="guia-detalhe__footer">
          <span class="guia-detalhe__total-label">Total VL Apurado:</span>
          <span class="guia-detalhe__total-valor">{{ totalApurado() | number: '1.2-2' }}</span>
          <span class="guia-detalhe__total-label">Total VL Pago:</span>
          <span class="guia-detalhe__total-valor">{{ totalPago() | number: '1.2-2' }}</span>
          <button class="guia-detalhe__btn-voltar" type="button" (click)="voltar()">Voltar</button>
        </footer>
      </div>
    }
  `,
  styles: [
    `
      .guia-detalhe {
        padding: 16px;
      }

      .guia-detalhe__loading {
        padding: 32px 16px;
        color: var(--color-tinta-secundaria);
        font-family: var(--font-sans);
        font-size: 15px;
        text-align: center;
      }

      .guia-detalhe__header {
        display: flex;
        flex-wrap: wrap;
        gap: 12px;
        align-items: center;
        padding-bottom: 16px;
        border-bottom: 1px solid var(--color-borda-discreta);
        margin-bottom: 16px;
      }

      .guia-detalhe__operadora,
      .guia-detalhe__beneficiario,
      .guia-detalhe__carteira,
      .guia-detalhe__data,
      .guia-detalhe__senha {
        font-family: var(--font-sans);
        font-size: 15px;
        color: var(--color-tinta);
      }

      .guia-detalhe__observacao {
        background-color: var(--color-ambar-claro);
        border: 1px solid var(--color-ambar);
        border-radius: 6px;
        padding: 12px 16px;
        margin-bottom: 16px;
        display: flex;
        flex-direction: column;
        gap: 4px;
      }

      .guia-detalhe__observacao-label {
        font-family: var(--font-sans);
        font-size: 12px;
        font-weight: 600;
        color: var(--color-ambar);
        text-transform: uppercase;
        letter-spacing: 0.05em;
      }

      .guia-detalhe__observacao-texto {
        font-family: var(--font-sans);
        font-size: 15px;
        color: var(--color-tinta);
      }

      .guia-detalhe__observacao-vazio {
        font-family: var(--font-sans);
        font-size: 15px;
        color: var(--color-tinta-secundaria);
      }

      .guia-detalhe__tabela {
        width: 100%;
        border-collapse: collapse;
        margin-bottom: 16px;
      }

      .guia-detalhe__tabela th {
        font-family: var(--font-sans);
        font-size: 12px;
        font-weight: 600;
        color: var(--color-tinta-secundaria);
        text-align: left;
        padding: 8px 12px;
        border-bottom: 1px solid var(--color-borda-media);
      }

      .guia-detalhe__item-row td {
        font-family: var(--font-sans);
        font-size: 14px;
        color: var(--color-tinta);
        padding: 10px 12px;
        border-bottom: 1px solid var(--color-borda-discreta);
      }

      .guia-detalhe__valor-mono {
        font-family: var(--font-mono);
        font-variant-numeric: tabular-nums;
      }

      .guia-detalhe__footer {
        display: flex;
        align-items: center;
        gap: 8px;
        flex-wrap: wrap;
        padding-top: 16px;
        border-top: 1px solid var(--color-borda-discreta);
      }

      .guia-detalhe__total-label {
        font-family: var(--font-sans);
        font-size: 14px;
        font-weight: 600;
        color: var(--color-tinta-secundaria);
      }

      .guia-detalhe__total-valor {
        font-family: var(--font-mono);
        font-size: 16px;
        font-variant-numeric: tabular-nums;
        color: var(--color-tinta);
        margin-right: 16px;
      }

      .guia-detalhe__btn-voltar {
        margin-left: auto;
        padding: 8px 16px;
        border: 1px solid var(--color-borda-media);
        border-radius: 6px;
        background: none;
        cursor: pointer;
        font-family: var(--font-sans);
        font-size: 15px;
        color: var(--color-tinta);
      }

      .badge {
        display: inline-block;
        padding: 2px 8px;
        border-radius: 4px;
        font-family: var(--font-sans);
        font-size: 12px;
        font-weight: 500;
      }

      .badge--verde {
        background-color: var(--color-verde-musgo-claro);
        color: var(--color-verde-musgo);
      }

      .badge--ferrugem {
        background-color: var(--color-ferrugem-claro);
        color: var(--color-ferrugem);
      }

      .badge--ambar {
        background-color: var(--color-ambar-claro);
        color: var(--color-ambar);
      }
    `,
  ],
})
export class GuiaDetalheComponent implements OnInit {
  private readonly _service = inject(MedicoGuiaService);
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);

  readonly detalhe = signal<MedicoGuiaDetalheDto | null>(null);
  readonly loading = signal(true);

  ngOnInit(): void {
    const id = this._route.snapshot.paramMap.get('id') ?? '';
    this._service.obterPorId(id).subscribe({
      next: (d) => {
        this.detalhe.set(d);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  voltar(): void {
    void this._router.navigate(['/guias']);
  }

  badgeClassSituacao(situacao: string): string {
    if (situacao === 'Apresentada') {
      return 'badge badge--ambar';
    }
    if (situacao === 'EmRecurso') {
      return 'badge badge--ferrugem';
    }
    return 'badge';
  }

  badgeClassCalculo(situacao: SituacaoCalculo): string {
    if (situacao === 'Calculado') {
      return 'badge badge--verde';
    }
    if (situacao === 'SemTabela' || situacao === 'SemDeflator' || situacao === 'Indeterminado') {
      return 'badge badge--ferrugem';
    }
    return 'badge badge--ambar';
  }

  totalApurado(): number {
    return this.detalhe()?.itens.reduce((s, i) => s + i.valorApurado, 0) ?? 0;
  }

  totalPago(): number {
    return this.detalhe()?.itens.reduce((s, i) => s + i.valorPago, 0) ?? 0;
  }
}
