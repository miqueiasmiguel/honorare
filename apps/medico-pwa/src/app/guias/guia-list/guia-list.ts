import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MedicoGuiaService } from '../medico-guia.service';
import type { MedicoGuiaSummaryItem, SituacaoGuia } from '../medico-guia.types';

@Component({
  selector: 'app-guia-list',
  imports: [],
  template: `
    <div class="guia-list">
      <div class="guia-list__filter-container">
        <button
          class="guia-list__toggle"
          type="button"
          [attr.aria-expanded]="filtrosAbertos()"
          aria-controls="guia-filtros"
          (click)="filtrosAbertos.set(!filtrosAbertos())"
        >
          <span class="guia-list__toggle-left">
            <svg
              width="15"
              height="15"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2.5"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3" />
            </svg>
            Filtros
            @if (filtrosAtivos() > 0) {
              <span class="guia-list__badge">{{ filtrosAtivos() }}</span>
            }
          </span>
          <svg
            class="guia-list__chevron"
            [class.guia-list__chevron--aberto]="filtrosAbertos()"
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
            <polyline points="6 9 12 15 18 9" />
          </svg>
        </button>

        <div
          class="guia-list__filters-wrapper"
          [class.guia-list__filters-wrapper--aberto]="filtrosAbertos()"
          id="guia-filtros"
          role="region"
          aria-label="Filtros de guias"
        >
          <div class="guia-list__filters-inner">
            <div class="guia-list__filters">
              <div class="guia-list__filter-group">
                <span class="guia-list__label">Período</span>
                <div class="guia-list__date-row">
                  <input
                    class="guia-list__input"
                    type="date"
                    aria-label="Data início"
                    [value]="dataInicio()"
                    (input)="onDataInicioInput($event)"
                  />
                  <input
                    class="guia-list__input"
                    type="date"
                    aria-label="Data fim"
                    [value]="dataFim()"
                    (input)="onDataFimInput($event)"
                  />
                </div>
              </div>

              <div class="guia-list__search-row">
                <div class="guia-list__filter-group">
                  <span class="guia-list__label">Operadora</span>
                  <input
                    class="guia-list__input"
                    type="search"
                    placeholder="Buscar…"
                    [value]="operadoraFiltro()"
                    (input)="operadoraFiltro.set($any($event.target).value)"
                  />
                </div>
                <div class="guia-list__filter-group">
                  <span class="guia-list__label">Beneficiário</span>
                  <input
                    class="guia-list__input"
                    type="search"
                    placeholder="Buscar…"
                    [value]="beneficiarioFiltro()"
                    (input)="beneficiarioFiltro.set($any($event.target).value)"
                  />
                </div>
              </div>

              <div class="guia-list__filter-group">
                <span class="guia-list__label">Situação</span>
                <div class="guia-list__chips" role="group" aria-label="Filtrar por situação">
                  <button
                    type="button"
                    class="guia-list__chip"
                    [class.guia-list__chip--ativo]="!situacaoFiltro()"
                    (click)="situacaoFiltro.set('')"
                  >
                    Todas
                  </button>
                  <button
                    type="button"
                    class="guia-list__chip"
                    [class.guia-list__chip--ativo]="situacaoFiltro() === 'Apresentada'"
                    (click)="toggleSituacao('Apresentada')"
                  >
                    Apresentada
                  </button>
                  <button
                    type="button"
                    class="guia-list__chip"
                    [class.guia-list__chip--ativo]="situacaoFiltro() === 'EmRecurso'"
                    (click)="toggleSituacao('EmRecurso')"
                  >
                    Em Recurso
                  </button>
                </div>
              </div>

              <div class="guia-list__chips">
                <button
                  type="button"
                  class="guia-list__chip"
                  [class.guia-list__chip--ativo]="apenasComObservacao()"
                  (click)="apenasComObservacao.set(!apenasComObservacao())"
                >
                  <svg
                    width="12"
                    height="12"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="2.5"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    aria-hidden="true"
                  >
                    <path
                      d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"
                    />
                    <line x1="12" y1="9" x2="12" y2="13" />
                    <line x1="12" y1="17" x2="12.01" y2="17" />
                  </svg>
                  Com observação
                </button>
              </div>

              @if (filtrosAtivos() > 0) {
                <button type="button" class="guia-list__limpar" (click)="limparFiltros()">
                  Limpar filtros
                </button>
              }
            </div>
          </div>
        </div>
      </div>

      @if (loading()) {
        <div class="guia-list__loading">
          <div class="guia-list__spinner"></div>
          <span>Carregando guias…</span>
        </div>
      } @else {
        @if (total() > 0) {
          <p class="guia-list__count">{{ contagem() }} {{ contagem() === 1 ? 'guia' : 'guias' }}</p>
        }

        <div class="guia-list__cards">
          @for (guia of guiasFiltradas(); track guia.id) {
            <div
              class="guia-card"
              role="button"
              tabindex="0"
              (click)="navegar(guia.id)"
              (keydown.enter)="navegar(guia.id)"
              (keydown.space)="$event.preventDefault(); navegar(guia.id)"
            >
              <div class="guia-card__top">
                <span [class]="badgeClass(guia.situacao)">{{ situacaoLabel(guia.situacao) }}</span>
                <div class="guia-card__meta">
                  @if (guia.temObservacao) {
                    <span class="guia-card__obs" title="Tem observação" aria-label="Tem observação">
                      <svg
                        width="14"
                        height="14"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        stroke-width="2"
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        aria-hidden="true"
                      >
                        <path
                          d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"
                        />
                        <line x1="12" y1="9" x2="12" y2="13" />
                        <line x1="12" y1="17" x2="12.01" y2="17" />
                      </svg>
                    </span>
                  }
                  <span class="guia-card__data">{{ formatarData(guia.dataAtendimento) }}</span>
                </div>
              </div>
              <span class="guia-card__beneficiario">{{ guia.beneficiarioNome ?? '—' }}</span>
              <div class="guia-card__bottom">
                <span class="guia-card__operadora">{{ guia.operadoraNome }}</span>
                @if (guia.totalItens > 0) {
                  <span class="guia-card__itens">{{ guia.totalItens }} proc.</span>
                }
              </div>
              <span class="guia-card__chevron" aria-hidden="true">
                <svg
                  width="16"
                  height="16"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                >
                  <polyline points="9 18 15 12 9 6" />
                </svg>
              </span>
            </div>
          }

          @if (guiasFiltradas().length === 0) {
            <div class="guia-list__empty">
              <svg
                class="guia-list__empty-icon"
                width="40"
                height="40"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="1.5"
                stroke-linecap="round"
                stroke-linejoin="round"
                aria-hidden="true"
              >
                <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                <polyline points="14 2 14 8 20 8" />
                <line x1="16" y1="13" x2="8" y2="13" />
                <line x1="16" y1="17" x2="8" y2="17" />
              </svg>
              <p>Nenhuma guia encontrada.</p>
              @if (filtrosAtivos() > 0) {
                <p class="guia-list__empty-sub">Tente ajustar os filtros.</p>
              }
            </div>
          }
        </div>

        @if (pagina() > 1 || hasNextPage()) {
          <div class="guia-list__pagination">
            @if (pagina() > 1) {
              <button class="guia-list__btn" type="button" (click)="paginaAnterior()">
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
                Anterior
              </button>
            } @else {
              <span></span>
            }
            <span class="guia-list__pagina">{{ pagina() }} / {{ totalPaginas() }}</span>
            @if (hasNextPage()) {
              <button class="guia-list__btn" type="button" (click)="proximaPagina()">
                Próximo
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
                  <polyline points="9 18 15 12 9 6" />
                </svg>
              </button>
            } @else {
              <span></span>
            }
          </div>
        }
      }
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .guia-list {
        padding: 16px;
        display: flex;
        flex-direction: column;
        gap: 12px;
      }

      /* ── Filter container ────────────────────────────────── */

      .guia-list__filter-container {
        display: flex;
        flex-direction: column;
        gap: 8px;
      }

      /* ── Toggle button ───────────────────────────────────── */

      .guia-list__toggle {
        display: flex;
        align-items: center;
        justify-content: space-between;
        width: 100%;
        padding: 12px 16px;
        background-color: var(--color-pergaminho-claro);
        border: 1px solid var(--color-borda-discreta);
        border-radius: 12px;
        cursor: pointer;
        font-family: var(--font-sans);
        font-size: 14px;
        font-weight: 500;
        color: var(--color-tinta);
        -webkit-tap-highlight-color: transparent;
        transition:
          background-color var(--duration-fast) var(--easing-default),
          border-color var(--duration-fast) var(--easing-default);
      }

      .guia-list__toggle:hover {
        background-color: var(--color-pergaminho);
        border-color: var(--color-borda-media);
      }

      .guia-list__toggle:active {
        background-color: var(--color-borda-discreta);
      }

      .guia-list__toggle:focus-visible {
        outline: 2px solid var(--color-terracota);
        outline-offset: 2px;
      }

      .guia-list__toggle-left {
        display: flex;
        align-items: center;
        gap: 8px;
      }

      .guia-list__badge {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        min-width: 20px;
        height: 20px;
        padding: 0 6px;
        border-radius: 10px;
        background-color: var(--color-terracota);
        color: white;
        font-family: var(--font-sans);
        font-size: 11px;
        font-weight: 700;
      }

      .guia-list__chevron {
        color: var(--color-tinta-terciaria);
        flex-shrink: 0;
        transition: transform var(--duration-base) var(--easing-default);
      }

      .guia-list__chevron--aberto {
        transform: rotate(180deg);
      }

      /* ── Collapsible wrapper ─────────────────────────────── */

      .guia-list__filters-wrapper {
        display: grid;
        grid-template-rows: 0fr;
        transition: grid-template-rows var(--duration-base) var(--easing-default);
      }

      .guia-list__filters-wrapper--aberto {
        grid-template-rows: 1fr;
      }

      .guia-list__filters-inner {
        overflow: hidden;
      }

      /* ── Filter panel ────────────────────────────────────── */

      .guia-list__filters {
        background-color: var(--color-pergaminho-claro);
        border: 1px solid var(--color-borda-discreta);
        border-radius: 12px;
        padding: 14px;
        display: flex;
        flex-direction: column;
        gap: 14px;
      }

      .guia-list__filter-group {
        display: flex;
        flex-direction: column;
        gap: 6px;
      }

      .guia-list__label {
        font-family: var(--font-sans);
        font-size: 11px;
        font-weight: 600;
        color: var(--color-tinta-secundaria);
        text-transform: uppercase;
        letter-spacing: 0.06em;
      }

      .guia-list__date-row {
        display: grid;
        grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
        gap: 8px;
      }

      .guia-list__search-row {
        display: grid;
        grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
        gap: 8px;
      }

      .guia-list__input {
        min-width: 0;
        width: 100%;
        padding: 10px 12px;
        border: 1px solid var(--color-borda-media);
        border-radius: 8px;
        font-family: var(--font-sans);
        font-size: 15px;
        color: var(--color-tinta);
        background-color: white;
        box-sizing: border-box;
        -webkit-appearance: none;
        appearance: none;
        transition: border-color var(--duration-fast) var(--easing-default);
      }

      .guia-list__input:focus-visible {
        outline: 2px solid var(--color-terracota);
        outline-offset: -1px;
        border-color: var(--color-terracota);
      }

      /* ── Chips ───────────────────────────────────────────── */

      .guia-list__chips {
        display: flex;
        flex-wrap: wrap;
        gap: 8px;
      }

      .guia-list__chip {
        display: inline-flex;
        align-items: center;
        gap: 5px;
        padding: 7px 14px;
        border-radius: 20px;
        border: 1px solid var(--color-borda-media);
        background: white;
        font-family: var(--font-sans);
        font-size: 13px;
        font-weight: 500;
        color: var(--color-tinta-secundaria);
        cursor: pointer;
        -webkit-tap-highlight-color: transparent;
        transition:
          background-color var(--duration-fast) var(--easing-default),
          border-color var(--duration-fast) var(--easing-default),
          color var(--duration-fast) var(--easing-default);
      }

      .guia-list__chip:hover {
        border-color: var(--color-tinta-secundaria);
        color: var(--color-tinta);
      }

      .guia-list__chip:active {
        background-color: var(--color-borda-discreta);
      }

      .guia-list__chip:focus-visible {
        outline: 2px solid var(--color-terracota);
        outline-offset: 2px;
      }

      .guia-list__chip--ativo {
        background-color: var(--color-terracota);
        border-color: var(--color-terracota);
        color: white;
      }

      .guia-list__chip--ativo:hover {
        background-color: var(--color-terracota-escuro);
        border-color: var(--color-terracota-escuro);
        color: white;
      }

      /* ── Limpar filtros ──────────────────────────────────── */

      .guia-list__limpar {
        align-self: flex-start;
        border: none;
        background: none;
        padding: 0;
        font-family: var(--font-sans);
        font-size: 13px;
        font-weight: 500;
        color: var(--color-terracota);
        cursor: pointer;
        -webkit-tap-highlight-color: transparent;
        text-decoration: underline;
        text-decoration-color: transparent;
        transition: text-decoration-color var(--duration-fast) var(--easing-default);
      }

      .guia-list__limpar:hover {
        text-decoration-color: var(--color-terracota);
      }

      /* ── Count ───────────────────────────────────────────── */

      .guia-list__count {
        font-family: var(--font-sans);
        font-size: 13px;
        color: var(--color-tinta-terciaria);
        margin: 0;
        padding: 0 2px;
      }

      /* ── Loading ─────────────────────────────────────────── */

      .guia-list__loading {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 14px;
        padding: 56px 16px;
        color: var(--color-tinta-secundaria);
        font-family: var(--font-sans);
        font-size: 15px;
      }

      .guia-list__spinner {
        width: 28px;
        height: 28px;
        border: 2.5px solid var(--color-borda-discreta);
        border-top-color: var(--color-terracota);
        border-radius: 50%;
        animation: guia-spin 0.75s linear infinite;
        flex-shrink: 0;
      }

      @keyframes guia-spin {
        to {
          transform: rotate(360deg);
        }
      }

      /* ── Cards ───────────────────────────────────────────── */

      .guia-list__cards {
        display: flex;
        flex-direction: column;
        gap: 8px;
      }

      .guia-card {
        position: relative;
        display: flex;
        flex-direction: column;
        gap: 5px;
        padding: 14px 44px 14px 16px;
        background-color: var(--color-pergaminho-claro);
        border: 1px solid var(--color-borda-discreta);
        border-radius: 12px;
        cursor: pointer;
        -webkit-tap-highlight-color: transparent;
        transition:
          background-color var(--duration-fast) var(--easing-default),
          border-color var(--duration-fast) var(--easing-default);
      }

      .guia-card:hover {
        background-color: var(--color-pergaminho);
        border-color: var(--color-borda-media);
      }

      .guia-card:focus-visible {
        outline: 2px solid var(--color-terracota);
        outline-offset: 2px;
      }

      .guia-card:active {
        background-color: var(--color-borda-discreta);
      }

      .guia-card__top {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 8px;
      }

      .guia-card__meta {
        display: flex;
        align-items: center;
        gap: 6px;
      }

      .guia-card__obs {
        color: var(--color-ambar);
        display: flex;
        align-items: center;
      }

      .guia-card__data {
        font-family: var(--font-sans);
        font-size: 13px;
        color: var(--color-tinta-terciaria);
        white-space: nowrap;
      }

      .guia-card__beneficiario {
        font-family: var(--font-sans);
        font-size: 16px;
        font-weight: 500;
        color: var(--color-tinta);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .guia-card__bottom {
        display: flex;
        align-items: center;
        gap: 4px;
        min-width: 0;
      }

      .guia-card__operadora {
        font-family: var(--font-sans);
        font-size: 13px;
        color: var(--color-tinta-secundaria);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
        flex: 1;
      }

      .guia-card__itens {
        font-family: var(--font-sans);
        font-size: 12px;
        color: var(--color-tinta-terciaria);
        flex-shrink: 0;
        padding-left: 8px;
        border-left: 1px solid var(--color-borda-discreta);
      }

      .guia-card__chevron {
        position: absolute;
        right: 14px;
        top: 50%;
        transform: translateY(-50%);
        color: var(--color-borda-media);
        display: flex;
        align-items: center;
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

      .badge--ambar {
        background-color: var(--color-ambar-claro);
        color: var(--color-ambar);
      }

      .badge--ferrugem {
        background-color: var(--color-ferrugem-claro);
        color: var(--color-ferrugem);
      }

      .badge--verde {
        background-color: var(--color-verde-musgo-claro);
        color: var(--color-verde-musgo);
      }

      /* ── Empty state ─────────────────────────────────────── */

      .guia-list__empty {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 10px;
        padding: 56px 16px;
        color: var(--color-tinta-secundaria);
        font-family: var(--font-sans);
        font-size: 15px;
        text-align: center;
      }

      .guia-list__empty-icon {
        color: var(--color-borda-media);
      }

      .guia-list__empty p {
        margin: 0;
      }

      .guia-list__empty-sub {
        font-size: 13px;
        color: var(--color-tinta-terciaria);
      }

      /* ── Pagination ──────────────────────────────────────── */

      .guia-list__pagination {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 8px;
        padding: 4px 0;
      }

      .guia-list__pagina {
        font-family: var(--font-sans);
        font-size: 13px;
        color: var(--color-tinta-terciaria);
      }

      .guia-list__btn {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        padding: 9px 16px;
        border: 1px solid var(--color-borda-media);
        border-radius: 8px;
        background: none;
        cursor: pointer;
        font-family: var(--font-sans);
        font-size: 14px;
        color: var(--color-tinta);
        -webkit-tap-highlight-color: transparent;
        transition: background-color var(--duration-fast) var(--easing-default);
      }

      .guia-list__btn:hover {
        background-color: var(--color-pergaminho-claro);
      }

      .guia-list__btn:active {
        background-color: var(--color-borda-discreta);
      }

      .guia-list__btn:focus-visible {
        outline: 2px solid var(--color-terracota);
        outline-offset: 2px;
      }
    `,
  ],
})
export class GuiaListComponent implements OnInit {
  private readonly _service = inject(MedicoGuiaService);
  private readonly _router = inject(Router);

  readonly guias = signal<MedicoGuiaSummaryItem[]>([]);
  readonly total = signal(0);
  readonly pagina = signal(1);
  readonly itensPorPagina = signal(20);
  readonly loading = signal(false);
  readonly dataInicio = signal('');
  readonly dataFim = signal('');
  readonly operadoraFiltro = signal('');
  readonly beneficiarioFiltro = signal('');
  readonly situacaoFiltro = signal<'' | SituacaoGuia>('');
  readonly apenasComObservacao = signal(false);
  readonly filtrosAbertos = signal(false);

  readonly filtrosAtivos = computed(() => {
    let count = 0;
    if (this.dataInicio()) {
      count++;
    }
    if (this.dataFim()) {
      count++;
    }
    if (this.operadoraFiltro()) {
      count++;
    }
    if (this.beneficiarioFiltro()) {
      count++;
    }
    if (this.situacaoFiltro()) {
      count++;
    }
    if (this.apenasComObservacao()) {
      count++;
    }
    return count;
  });

  readonly hasNextPage = computed(
    () => this.pagina() < Math.ceil(this.total() / this.itensPorPagina()),
  );

  readonly totalPaginas = computed(() =>
    Math.max(1, Math.ceil(this.total() / this.itensPorPagina())),
  );

  readonly guiasFiltradas = computed(() => {
    let guias = this.guias();
    const operadora = this.operadoraFiltro().toLowerCase().trim();
    const beneficiario = this.beneficiarioFiltro().toLowerCase().trim();
    const situacao = this.situacaoFiltro();
    const apenasObs = this.apenasComObservacao();
    if (operadora) {
      guias = guias.filter((g) => g.operadoraNome.toLowerCase().includes(operadora));
    }
    if (beneficiario) {
      guias = guias.filter((g) => (g.beneficiarioNome ?? '').toLowerCase().includes(beneficiario));
    }
    if (situacao) {
      guias = guias.filter((g) => g.situacao === situacao);
    }
    if (apenasObs) {
      guias = guias.filter((g) => g.temObservacao);
    }
    return guias;
  });

  readonly contagem = computed(() => {
    const hasClientFilter = this.guiasFiltradas().length !== this.guias().length;
    return hasClientFilter ? this.guiasFiltradas().length : this.total();
  });

  ngOnInit(): void {
    this._carregar();
  }

  onDataInicioInput(event: Event): void {
    this.dataInicio.set((event.target as HTMLInputElement).value);
    this.pagina.set(1);
    this._carregar();
  }

  onDataFimInput(event: Event): void {
    this.dataFim.set((event.target as HTMLInputElement).value);
    this.pagina.set(1);
    this._carregar();
  }

  toggleSituacao(s: SituacaoGuia): void {
    this.situacaoFiltro.set(this.situacaoFiltro() === s ? '' : s);
  }

  limparFiltros(): void {
    const hasDateFilter = !!(this.dataInicio() || this.dataFim());
    this.operadoraFiltro.set('');
    this.beneficiarioFiltro.set('');
    this.situacaoFiltro.set('');
    this.apenasComObservacao.set(false);
    if (hasDateFilter) {
      this.dataInicio.set('');
      this.dataFim.set('');
      this.pagina.set(1);
      this._carregar();
    }
  }

  navegar(id: string): void {
    void this._router.navigate(['/guias', id]);
  }

  proximaPagina(): void {
    this.pagina.set(this.pagina() + 1);
    this._carregar();
  }

  paginaAnterior(): void {
    this.pagina.set(this.pagina() - 1);
    this._carregar();
  }

  badgeClass(situacao: SituacaoGuia): string {
    if (situacao === 'Apresentada') {
      return 'badge badge--ambar';
    }
    if (situacao === 'EmRecurso') {
      return 'badge badge--ferrugem';
    }
    return 'badge badge--verde';
  }

  situacaoLabel(situacao: SituacaoGuia): string {
    if (situacao === 'EmRecurso') {
      return 'Em Recurso';
    }
    return situacao;
  }

  formatarData(iso: string): string {
    if (!iso) {
      return '—';
    }
    const [year, month, day] = iso.split('-');
    return `${day}/${month}/${year}`;
  }

  private _carregar(): void {
    this.loading.set(true);
    this._service
      .listar({
        pagina: this.pagina(),
        itensPorPagina: this.itensPorPagina(),
        dataInicio: this.dataInicio() || undefined,
        dataFim: this.dataFim() || undefined,
      })
      .subscribe({
        next: (result) => {
          this.guias.set(result.itens);
          this.total.set(result.total);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }
}
