import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MedicoGuiaService } from '../medico-guia.service';
import type { MedicoGuiaDetalheDto, SituacaoCalculo, SituacaoGuia } from '../medico-guia.types';

@Component({
  selector: 'app-guia-detalhe',
  imports: [],
  template: `
    @if (loading()) {
      <div class="guia-detalhe__loading">
        <div class="guia-detalhe__spinner"></div>
        <span>Carregando…</span>
      </div>
    } @else if (detalhe(); as d) {
      <div class="guia-detalhe">
        <button class="guia-detalhe__voltar" type="button" (click)="voltar()">
          <svg
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <polyline points="15 18 9 12 15 6" />
          </svg>
          Voltar
        </button>

        <div class="guia-detalhe__header">
          <div class="guia-detalhe__header-field guia-detalhe__header-field--full">
            <span class="guia-detalhe__field-label">Operadora</span>
            <span class="guia-detalhe__field-value guia-detalhe__field-value--strong">{{
              d.operadoraNome
            }}</span>
          </div>
          <div class="guia-detalhe__header-field guia-detalhe__header-field--full">
            <span class="guia-detalhe__field-label">Beneficiário</span>
            <span class="guia-detalhe__field-value">{{ d.beneficiarioNome ?? '—' }}</span>
          </div>
          <div class="guia-detalhe__header-grid">
            <div class="guia-detalhe__header-field">
              <span class="guia-detalhe__field-label">Data de atendimento</span>
              <span class="guia-detalhe__field-value">{{ formatarData(d.dataAtendimento) }}</span>
            </div>
            @if (d.beneficiarioCarteira) {
              <div class="guia-detalhe__header-field">
                <span class="guia-detalhe__field-label">Carteira</span>
                <span class="guia-detalhe__field-value">{{ d.beneficiarioCarteira }}</span>
              </div>
            }
            @if (d.senha) {
              <div class="guia-detalhe__header-field">
                <span class="guia-detalhe__field-label">Senha</span>
                <span class="guia-detalhe__field-value">{{ d.senha }}</span>
              </div>
            }
            <div class="guia-detalhe__header-field">
              <span class="guia-detalhe__field-label">Situação</span>
              <span [class]="badgeClassSituacao(d.situacao)">{{ situacaoLabel(d.situacao) }}</span>
            </div>
          </div>
        </div>

        @if (d.observacao) {
          <div class="guia-detalhe__observacao">
            <span class="guia-detalhe__observacao-label">Observação do responsável</span>
            <span class="guia-detalhe__observacao-texto">{{ d.observacao }}</span>
          </div>
        }

        <div class="guia-detalhe__itens">
          <span class="guia-detalhe__itens-titulo">Procedimentos ({{ d.itens.length }})</span>

          @for (item of d.itens; track item.id) {
            <div class="guia-item">
              <div class="guia-item__top">
                <div class="guia-item__codes">
                  <span class="guia-item__tuss">{{ item.codigoTuss }}</span>
                  <span class="guia-item__pos">{{ posicaoLabel(item.posicaoExecutor) }}</span>
                </div>
                <span [class]="badgeClassCalculo(item.situacaoCalculo)">{{
                  situacaoCalculoLabel(item.situacaoCalculo)
                }}</span>
              </div>
              <span class="guia-item__descricao">{{ item.descricaoProcedimento }}</span>
              <div class="guia-item__valores">
                <div class="guia-item__valor">
                  <span class="guia-item__valor-label">VL Apurado</span>
                  <span class="guia-item__valor-num">{{ formatarMoeda(item.valorApurado) }}</span>
                </div>
                <div class="guia-item__valor">
                  <span class="guia-item__valor-label">VL Pago</span>
                  <span class="guia-item__valor-num guia-item__valor-num--pago">{{
                    formatarMoeda(item.valorLiquidado)
                  }}</span>
                </div>
              </div>
            </div>
          }
        </div>

        <div class="guia-detalhe__totais">
          <div class="guia-detalhe__total-item">
            <span class="guia-detalhe__total-label">Total apurado</span>
            <span class="guia-detalhe__total-valor">{{ formatarMoeda(totalApurado()) }}</span>
          </div>
          <div class="guia-detalhe__total-item">
            <span class="guia-detalhe__total-label">Total pago</span>
            <span class="guia-detalhe__total-valor guia-detalhe__total-valor--pago">{{
              formatarMoeda(totalPago())
            }}</span>
          </div>
        </div>
      </div>
    }
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .guia-detalhe {
        padding: 16px;
        display: flex;
        flex-direction: column;
        gap: 16px;
      }

      /* ── Loading ─────────────────────────────────────────── */

      .guia-detalhe__loading {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 14px;
        padding: 56px 16px;
        color: var(--color-tinta-secundaria);
        font-family: var(--font-sans);
        font-size: 15px;
      }

      .guia-detalhe__spinner {
        width: 28px;
        height: 28px;
        border: 2.5px solid var(--color-borda-discreta);
        border-top-color: var(--color-terracota);
        border-radius: 50%;
        animation: detalhe-spin 0.75s linear infinite;
      }

      @keyframes detalhe-spin {
        to {
          transform: rotate(360deg);
        }
      }

      /* ── Back button ─────────────────────────────────────── */

      .guia-detalhe__voltar {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        padding: 0;
        border: none;
        background: none;
        cursor: pointer;
        font-family: var(--font-sans);
        font-size: 14px;
        color: var(--color-tinta-secundaria);
        -webkit-tap-highlight-color: transparent;
        transition: color var(--duration-fast) var(--easing-default);
      }

      .guia-detalhe__voltar:hover {
        color: var(--color-terracota);
      }

      .guia-detalhe__voltar:focus-visible {
        outline: 2px solid var(--color-terracota);
        outline-offset: 3px;
        border-radius: 4px;
      }

      /* ── Header ──────────────────────────────────────────── */

      .guia-detalhe__header {
        background-color: var(--color-pergaminho-claro);
        border: 1px solid var(--color-borda-discreta);
        border-radius: 12px;
        padding: 16px;
        display: flex;
        flex-direction: column;
        gap: 12px;
      }

      .guia-detalhe__header-grid {
        display: grid;
        grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
        gap: 12px;
      }

      .guia-detalhe__header-field {
        display: flex;
        flex-direction: column;
        gap: 3px;
        min-width: 0;
      }

      .guia-detalhe__header-field--full {
        grid-column: 1 / -1;
      }

      .guia-detalhe__field-label {
        font-family: var(--font-sans);
        font-size: 11px;
        font-weight: 600;
        color: var(--color-tinta-terciaria);
        text-transform: uppercase;
        letter-spacing: 0.06em;
      }

      .guia-detalhe__field-value {
        font-family: var(--font-sans);
        font-size: 15px;
        color: var(--color-tinta);
        overflow-wrap: break-word;
      }

      .guia-detalhe__field-value--strong {
        font-weight: 600;
      }

      /* ── Observação ──────────────────────────────────────── */

      .guia-detalhe__observacao {
        background-color: var(--color-ambar-claro);
        border: 1px solid var(--color-ambar);
        border-radius: 10px;
        padding: 14px 16px;
        display: flex;
        flex-direction: column;
        gap: 6px;
      }

      .guia-detalhe__observacao-label {
        font-family: var(--font-sans);
        font-size: 11px;
        font-weight: 700;
        color: var(--color-ambar);
        text-transform: uppercase;
        letter-spacing: 0.06em;
      }

      .guia-detalhe__observacao-texto {
        font-family: var(--font-sans);
        font-size: 15px;
        color: var(--color-tinta);
        line-height: 1.5;
        overflow-wrap: break-word;
      }

      /* ── Itens (cards) ───────────────────────────────────── */

      .guia-detalhe__itens {
        display: flex;
        flex-direction: column;
        gap: 8px;
      }

      .guia-detalhe__itens-titulo {
        font-family: var(--font-sans);
        font-size: 11px;
        font-weight: 600;
        color: var(--color-tinta-secundaria);
        text-transform: uppercase;
        letter-spacing: 0.06em;
        padding: 0 2px;
      }

      .guia-item {
        background-color: var(--color-pergaminho-claro);
        border: 1px solid var(--color-borda-discreta);
        border-radius: 10px;
        padding: 14px;
        display: flex;
        flex-direction: column;
        gap: 8px;
      }

      .guia-item__top {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 8px;
        min-width: 0;
      }

      .guia-item__codes {
        display: flex;
        align-items: center;
        gap: 10px;
        min-width: 0;
      }

      .guia-item__tuss {
        font-family: var(--font-mono);
        font-size: 13px;
        font-variant-numeric: tabular-nums;
        color: var(--color-tinta);
        flex-shrink: 0;
      }

      .guia-item__pos {
        font-family: var(--font-sans);
        font-size: 12px;
        color: var(--color-tinta-terciaria);
        flex-shrink: 0;
      }

      .guia-item__descricao {
        font-family: var(--font-sans);
        font-size: 14px;
        color: var(--color-tinta);
        line-height: 1.4;
        overflow-wrap: break-word;
      }

      .guia-item__valores {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 8px;
        padding-top: 8px;
        border-top: 1px solid var(--color-borda-discreta);
      }

      .guia-item__valor {
        display: flex;
        flex-direction: column;
        gap: 2px;
      }

      .guia-item__valor-label {
        font-family: var(--font-sans);
        font-size: 11px;
        font-weight: 600;
        color: var(--color-tinta-terciaria);
        text-transform: uppercase;
        letter-spacing: 0.05em;
      }

      .guia-item__valor-num {
        font-family: var(--font-mono);
        font-size: 15px;
        font-weight: 600;
        font-variant-numeric: tabular-nums;
        color: var(--color-tinta);
      }

      .guia-item__valor-num--pago {
        color: var(--color-verde-musgo);
      }

      /* ── Totals ──────────────────────────────────────────── */

      .guia-detalhe__totais {
        background-color: var(--color-pergaminho-claro);
        border: 1px solid var(--color-borda-discreta);
        border-radius: 12px;
        padding: 16px;
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 16px;
      }

      .guia-detalhe__total-item {
        display: flex;
        flex-direction: column;
        gap: 4px;
        min-width: 0;
      }

      .guia-detalhe__total-label {
        font-family: var(--font-sans);
        font-size: 11px;
        font-weight: 600;
        color: var(--color-tinta-terciaria);
        text-transform: uppercase;
        letter-spacing: 0.06em;
      }

      .guia-detalhe__total-valor {
        font-family: var(--font-mono);
        font-size: 18px;
        font-weight: 600;
        font-variant-numeric: tabular-nums;
        color: var(--color-tinta);
        overflow-wrap: break-word;
      }

      .guia-detalhe__total-valor--pago {
        color: var(--color-verde-musgo);
      }

      /* ── Badges ──────────────────────────────────────────── */

      .badge {
        display: inline-flex;
        align-items: center;
        padding: 3px 10px;
        border-radius: 20px;
        font-family: var(--font-sans);
        font-size: 12px;
        font-weight: 600;
        flex-shrink: 0;
        white-space: nowrap;
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

  readonly totalApurado = computed(
    () => this.detalhe()?.itens.reduce((s, i) => s + (i.valorApurado ?? 0), 0) ?? 0,
  );

  readonly totalPago = computed(
    () => this.detalhe()?.itens.reduce((s, i) => s + (i.valorLiquidado ?? 0), 0) ?? 0,
  );

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

  formatarMoeda(value: number | null): string {
    if (value === null) {
      return '—';
    }
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(value);
  }

  posicaoLabel(posicao: string): string {
    const labels: Record<string, string> = {
      Cirurgiao: 'Cirurgião',
      PrimeiroAuxiliar: '1º Aux.',
      SegundoAuxiliar: '2º Aux.',
      TerceiroAuxiliar: '3º Aux.',
      Anestesista: 'Anestesista',
      ClinicoAssistente: 'Clínico',
    };
    return labels[posicao] ?? posicao;
  }

  formatarData(iso: string | null): string {
    if (!iso) return '—';
    const [year, month, day] = iso.split('-');
    return `${day}/${month}/${year}`;
  }

  badgeClassSituacao(situacao: SituacaoGuia): string {
    if (situacao === 'Apresentada') return 'badge badge--ambar';
    if (situacao === 'EmRecurso') return 'badge badge--ferrugem';
    return 'badge badge--verde';
  }

  situacaoLabel(situacao: SituacaoGuia): string {
    if (situacao === 'EmRecurso') return 'Em Recurso';
    return situacao;
  }

  badgeClassCalculo(situacao: SituacaoCalculo): string {
    if (situacao === 'Calculado') return 'badge badge--verde';
    if (situacao === 'SemTabela' || situacao === 'SemDeflator' || situacao === 'Indeterminado') {
      return 'badge badge--ferrugem';
    }
    return 'badge badge--ambar';
  }

  situacaoCalculoLabel(situacao: SituacaoCalculo): string {
    const labels: Record<SituacaoCalculo, string> = {
      Calculado: 'Calculado',
      SemTabela: 'Sem Tabela',
      SemDeflator: 'Sem Deflator',
      Indeterminado: 'Indeterminado',
      Pacote: 'Pacote',
      NaoCalculado: 'Não Calculado',
    };
    return labels[situacao];
  }
}
