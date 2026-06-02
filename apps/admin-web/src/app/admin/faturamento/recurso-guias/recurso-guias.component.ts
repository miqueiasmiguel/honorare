import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { RecursoService } from '../recurso.service';
import { GuiaService } from '../guia.service';
import type { GuiaItem, SituacaoGuia } from '../guia.types';
import type { GuiaNoRecursoDto, RecursoDto } from '../recurso.types';

@Component({
  selector: 'app-recurso-guias',
  template: `
    <div class="recurso-guias">
      <header class="recurso-guias__header">
        <div class="recurso-guias__meta">
          <div class="recurso-guias__breadcrumb">
            <span class="recurso-guias__operadora">{{ recurso()?.operadoraNome }}</span>
            <span class="recurso-guias__sep" aria-hidden="true">›</span>
            <span class="recurso-guias__prestador">{{ recurso()?.prestadorNome }}</span>
          </div>
          <h2 class="recurso-guias__titulo">{{ recurso()?.numero ?? '—' }}</h2>
        </div>
        <div class="recurso-guias__header-acoes">
          <button class="recurso-guias__btn-editar" type="button" (click)="editarRecurso()">
            Editar recurso
          </button>
          <button class="recurso-guias__btn-pdf" type="button" (click)="baixarPdf()">
            Baixar PDF
          </button>
        </div>
      </header>

      <section class="recurso-guias__secao">
        <div class="recurso-guias__secao-header">
          <h3 class="recurso-guias__secao-titulo">
            Guias vinculadas
            @if (guias().length > 0) {
              <span class="recurso-guias__count">{{ guias().length }}</span>
            }
          </h3>
        </div>
        <div class="recurso-guias__secao-body">
          @for (guia of guias(); track guia.id) {
            <div class="guia-card">
              <div
                class="guia-card__header"
                role="button"
                tabindex="0"
                (click)="alternarExpansao(guia.id)"
                (keydown.enter)="alternarExpansao(guia.id)"
              >
                <span class="guia-card__numero-guia">{{ guia.numeroGuia }}</span>
                <span class="guia-card__data">{{ formatarData(guia.dataAtendimento) }}</span>
                <span class="guia-card__beneficiario">{{ guia.beneficiarioNome ?? '—' }}</span>
                <span class="guia-card__itens">{{ guia.itens.length }} iten(s)</span>
                @if (guia.observacao) {
                  <span class="guia-card__obs-badge">Obs.</span>
                }
                <button
                  class="guia-card__remover"
                  type="button"
                  (click)="$event.stopPropagation(); removerGuia(guia.id)"
                >
                  Remover
                </button>
                <span
                  class="guia-card__expand-icon"
                  [class.guia-card__expand-icon--aberta]="guiaExpandida() === guia.id"
                  aria-hidden="true"
                  >›</span
                >
              </div>

              @if (guiaExpandida() === guia.id) {
                <div class="guia-card__detalhe">
                  <div class="guia-card__observacao">
                    <label class="guia-card__obs-label" [for]="'obs-' + guia.id">Observação</label>
                    <textarea
                      class="guia-card__obs-input"
                      [id]="'obs-' + guia.id"
                      [value]="observacoesEmEdicao()[guia.id] ?? ''"
                      (input)="onObservacaoInput(guia.id, $any($event.target).value)"
                      rows="2"
                      maxlength="2000"
                      placeholder="Descreva a divergência (aparece no PDF e no portal do médico)"
                    ></textarea>
                    <button
                      class="guia-card__obs-salvar"
                      type="button"
                      [disabled]="salvandoObservacao()[guia.id]"
                      (click)="salvarObservacao(guia.id)"
                    >
                      {{ salvandoObservacao()[guia.id] ? 'Salvando…' : 'Salvar observação' }}
                    </button>
                  </div>

                  <table class="guia-card__itens-table">
                    <thead>
                      <tr>
                        <th>Código</th>
                        <th>Procedimento</th>
                        <th>Posição</th>
                        <th>VL CORRETO</th>
                        <th>PG OPERADORA</th>
                        <th>GLOSA</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (item of guia.itens; track item.id) {
                        <tr>
                          <td>{{ item.codigoTuss }}</td>
                          <td>{{ item.descricaoProcedimento }}</td>
                          <td>{{ item.posicaoExecutor }}</td>
                          <td class="guia-card__valor-apurado-cell">
                            <input
                              class="guia-card__valor-input"
                              type="number"
                              step="0.01"
                              min="0"
                              [value]="valoresEmEdicao()[item.id] ?? ''"
                              (input)="onValorApuradoInput(item.id, $any($event.target).value)"
                              (blur)="salvarValorApurado(guia.id, item.id)"
                              (keydown.enter)="salvarValorApurado(guia.id, item.id)"
                              [disabled]="salvandoValorApurado()[item.id]"
                              placeholder="0,00"
                            />
                          </td>
                          <td>{{ formatarMoeda(item.valorLiquidado) }}</td>
                          <td
                            [class.guia-card__glosa]="
                              item.valorApurado !== null &&
                              item.valorLiquidado !== null &&
                              item.valorApurado > item.valorLiquidado
                            "
                          >
                            {{
                              item.valorApurado !== null && item.valorLiquidado !== null
                                ? formatarMoeda(item.valorApurado - item.valorLiquidado)
                                : '—'
                            }}
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </div>
          } @empty {
            <p class="recurso-guias__vazio">Nenhuma guia vinculada a este recurso.</p>
          }
        </div>
      </section>

      <section class="recurso-guias__secao">
        <div class="recurso-guias__secao-header">
          <h3 class="recurso-guias__secao-titulo">Adicionar guias</h3>
        </div>
        <div class="recurso-guias__secao-body">
          <div class="recurso-guias__filtros-painel">
            <div class="recurso-guias__filtros-grade">
              <input
                class="recurso-guias__filtro-input"
                type="text"
                placeholder="Guia"
                [value]="filtroNumeroGuia()"
                (input)="filtroNumeroGuia.set($any($event.target).value)"
              />
              <input
                class="recurso-guias__filtro-input"
                type="text"
                placeholder="Beneficiário"
                [value]="filtroBeneficiario()"
                (input)="filtroBeneficiario.set($any($event.target).value)"
              />
              <select
                class="recurso-guias__filtro-select"
                (change)="filtroSituacao.set($any($event.target).value)"
              >
                <option value="" [selected]="!filtroSituacao()">Todas as situações</option>
                <option value="Apresentada" [selected]="filtroSituacao() === 'Apresentada'">
                  Apresentada
                </option>
                <option value="Liquidada" [selected]="filtroSituacao() === 'Liquidada'">
                  Liquidada
                </option>
              </select>
              <label class="recurso-guias__filtro-toggle">
                <input
                  type="checkbox"
                  [checked]="filtroSomenteGlosa()"
                  (change)="filtroSomenteGlosa.set($any($event.target).checked)"
                />
                Só com glosa
              </label>
            </div>
            <div class="recurso-guias__filtros-linha">
              <div class="recurso-guias__date-range">
                <input
                  class="recurso-guias__filtro-input recurso-guias__filtro-input--data"
                  type="date"
                  [value]="filtroDataInicio()"
                  (input)="filtroDataInicio.set($any($event.target).value)"
                />
                <span class="recurso-guias__date-sep" aria-hidden="true">a</span>
                <input
                  class="recurso-guias__filtro-input recurso-guias__filtro-input--data"
                  type="date"
                  [value]="filtroDataFim()"
                  (input)="filtroDataFim.set($any($event.target).value)"
                />
              </div>
              <button class="recurso-guias__btn-filtrar" type="button" (click)="filtrar()">
                Filtrar
              </button>
            </div>
          </div>

          @if (!filtroAplicado()) {
            <p class="recurso-guias__hint">Aplique filtros para buscar guias disponíveis.</p>
          }

          @if (filtroAplicado() && candidatas().length > 0) {
            <div class="recurso-guias__acoes-candidatas">
              <button
                class="recurso-guias__btn-adicionar-todas"
                type="button"
                (click)="adicionarTodas()"
              >
                Adicionar todas ({{ totalCandidatas() }})
              </button>
            </div>
          }

          @if (filtroAplicado()) {
            <table class="recurso-guias__tabela-candidatas">
              <thead>
                <tr>
                  <th>Guia</th>
                  <th>Data</th>
                  <th>Beneficiário</th>
                  <th>Situação</th>
                  <th>Itens</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (candidata of candidatas(); track candidata.id) {
                  <tr class="recurso-guias__linha-candidata">
                    <td>{{ candidata.numeroGuia }}</td>
                    <td>{{ formatarData(candidata.dataAtendimento) }}</td>
                    <td>{{ candidata.beneficiarioNome }}</td>
                    <td>
                      <span
                        class="recurso-guias__situacao-badge"
                        [class.recurso-guias__situacao-badge--apresentada]="
                          candidata.situacao === 'Apresentada'
                        "
                        [class.recurso-guias__situacao-badge--liquidada]="
                          candidata.situacao === 'Liquidada'
                        "
                        >{{ candidata.situacao }}</span
                      >
                    </td>
                    <td>{{ candidata.totalItens }}</td>
                    <td>
                      <button
                        class="recurso-guias__btn-adicionar"
                        type="button"
                        (click)="adicionarGuia(candidata)"
                      >
                        Adicionar
                      </button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="6">
                      <p class="recurso-guias__vazio">Nenhuma guia encontrada com esses filtros.</p>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </section>

      @if (erroValidacao()) {
        <p class="recurso-guias__erro">{{ erroValidacao() }}</p>
      }
    </div>
  `,
  styleUrl: './recurso-guias.component.scss',
})
export class RecursoGuiasComponent implements OnInit {
  private readonly _recursoService = inject(RecursoService);
  private readonly _guiaService = inject(GuiaService);
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);

  readonly recursoId = signal<string | null>(null);
  readonly recurso = signal<RecursoDto | null>(null);
  readonly guias = signal<GuiaNoRecursoDto[]>([]);

  readonly prestadorId = signal('');
  readonly operadoraId = signal('');

  readonly filtroNumeroGuia = signal('');
  readonly filtroBeneficiario = signal('');
  readonly filtroDataInicio = signal('');
  readonly filtroDataFim = signal('');
  readonly filtroSituacao = signal<SituacaoGuia | ''>('');
  readonly filtroSomenteGlosa = signal(false);

  readonly candidatas = signal<GuiaItem[]>([]);
  readonly totalCandidatas = signal(0);
  readonly carregandoCandidatas = signal(false);
  readonly filtroAplicado = signal(false);
  readonly erroValidacao = signal('');

  readonly guiaExpandida = signal<string | null>(null);
  readonly observacoesEmEdicao = signal<Record<string, string | undefined>>({});
  readonly valoresEmEdicao = signal<Record<string, string | undefined>>({});
  readonly salvandoObservacao = signal<Record<string, boolean>>({});
  readonly salvandoValorApurado = signal<Record<string, boolean>>({});

  ngOnInit(): void {
    const id = this._route.snapshot.paramMap.get('id');
    if (id) {
      this.recursoId.set(id);
      this._carregar(id);
    }
  }

  onObservacaoInput(guiaId: string, value: string): void {
    this.observacoesEmEdicao.update((m) => ({ ...m, [guiaId]: value }));
  }

  onValorApuradoInput(itemId: string, value: string): void {
    this.valoresEmEdicao.update((m) => ({ ...m, [itemId]: value }));
  }

  alternarExpansao(guiaId: string): void {
    if (this.guiaExpandida() === guiaId) {
      this.guiaExpandida.set(null);
      return;
    }
    const guia = this.guias().find((g) => g.id === guiaId);
    if (!guia) {
      return;
    }
    this.guiaExpandida.set(guiaId);
    this.observacoesEmEdicao.update((m) => ({ ...m, [guiaId]: guia.observacao ?? '' }));
    guia.itens.forEach((item) => {
      this.valoresEmEdicao.update((m) => ({
        ...m,
        [item.id]: item.valorApurado != null ? String(item.valorApurado) : '',
      }));
    });
  }

  salvarObservacao(guiaId: string): void {
    const texto = this.observacoesEmEdicao()[guiaId] ?? '';
    this.salvandoObservacao.update((m) => ({ ...m, [guiaId]: true }));
    this._guiaService.atualizarObservacao(guiaId, texto).subscribe({
      next: () => {
        this.guias.update((gs) =>
          gs.map((g) => (g.id === guiaId ? { ...g, observacao: texto } : g)),
        );
        this.salvandoObservacao.update((m) => ({ ...m, [guiaId]: false }));
      },
      error: () => {
        this.erroValidacao.set('Erro ao salvar observação.');
        this.salvandoObservacao.update((m) => ({ ...m, [guiaId]: false }));
      },
    });
  }

  salvarValorApurado(guiaId: string, itemId: string): void {
    const raw = this.valoresEmEdicao()[itemId] ?? '';
    const valor = raw === '' ? null : parseFloat(raw.replace(',', '.'));
    this.salvandoValorApurado.update((m) => ({ ...m, [itemId]: true }));
    this._guiaService.atualizarValorApuradoItem(guiaId, itemId, valor).subscribe({
      next: () => {
        this.guias.update((gs) =>
          gs.map((g) =>
            g.id === guiaId
              ? {
                  ...g,
                  itens: g.itens.map((i) => (i.id === itemId ? { ...i, valorApurado: valor } : i)),
                }
              : g,
          ),
        );
        this.salvandoValorApurado.update((m) => ({ ...m, [itemId]: false }));
      },
      error: () => {
        this.erroValidacao.set('Erro ao salvar valor apurado.');
        this.salvandoValorApurado.update((m) => ({ ...m, [itemId]: false }));
      },
    });
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

  filtrar(): void {
    const id = this.recursoId();
    if (!id) {
      return;
    }
    this.carregandoCandidatas.set(true);
    this.erroValidacao.set('');
    const situacao = this.filtroSituacao();
    this._guiaService
      .listar({
        prestadorId: this.prestadorId(),
        operadoraId: this.operadoraId(),
        semRecurso: true,
        numeroGuia: this.filtroNumeroGuia() || undefined,
        beneficiario: this.filtroBeneficiario() || undefined,
        dataInicio: this.filtroDataInicio() || undefined,
        dataFim: this.filtroDataFim() || undefined,
        situacao: situacao !== '' ? situacao : undefined,
        somenteComGlosa: this.filtroSomenteGlosa() || undefined,
        pagina: 1,
        itensPorPagina: 50,
      })
      .subscribe({
        next: (result) => {
          this.candidatas.set(result.itens);
          this.totalCandidatas.set(result.total);
          this.filtroAplicado.set(true);
          this.carregandoCandidatas.set(false);
        },
        error: () => {
          this.erroValidacao.set('Erro ao buscar guias.');
          this.carregandoCandidatas.set(false);
        },
      });
  }

  adicionarTodas(): void {
    const id = this.recursoId();
    if (!id) {
      return;
    }
    this.erroValidacao.set('');
    const situacao = this.filtroSituacao();
    this._recursoService
      .adicionarGuiasLote(id, {
        prestadorId: this.prestadorId(),
        operadoraId: this.operadoraId(),
        dataInicio: this.filtroDataInicio() || undefined,
        dataFim: this.filtroDataFim() || undefined,
        situacao: situacao !== '' ? situacao : undefined,
        numeroGuia: this.filtroNumeroGuia() || undefined,
        beneficiario: this.filtroBeneficiario() || undefined,
        somenteComGlosa: this.filtroSomenteGlosa() || undefined,
      })
      .subscribe({
        next: () => {
          this._carregar(id);
          this.filtrar();
        },
        error: () => {
          this.erroValidacao.set('Erro ao adicionar guias em lote.');
        },
      });
  }

  adicionarGuia(guia: GuiaItem): void {
    const id = this.recursoId();
    if (!id) {
      return;
    }
    this.erroValidacao.set('');
    this._recursoService.adicionarGuia(id, guia.id).subscribe({
      next: () => {
        this.candidatas.update((prev) => prev.filter((g) => g.id !== guia.id));
        this._carregar(id);
      },
      error: () => {
        this.erroValidacao.set('Erro ao adicionar guia. Tente novamente.');
      },
    });
  }

  removerGuia(guiaId: string): void {
    const id = this.recursoId();
    if (!id) {
      return;
    }
    this.erroValidacao.set('');
    this._recursoService.removerGuia(id, guiaId).subscribe({
      next: () => {
        this.guias.update((prev) => prev.filter((g) => g.id !== guiaId));
      },
      error: () => {
        this.erroValidacao.set('Erro ao remover guia. Tente novamente.');
      },
    });
  }

  editarRecurso(): void {
    const id = this.recursoId();
    if (id) {
      void this._router.navigate(['/admin/recursos', id]);
    }
  }

  baixarPdf(): void {
    const id = this.recursoId();
    if (id) {
      this._recursoService.baixarPdf(id);
    }
  }

  formatarData(iso: string): string {
    return new Intl.DateTimeFormat('pt-BR').format(new Date(`${iso}T00:00:00`));
  }

  private _carregar(id: string): void {
    this._recursoService.obterPorId(id).subscribe({
      next: (detalhe) => {
        this.recurso.set(detalhe.header);
        this.guias.set(detalhe.guias);
        this.prestadorId.set(detalhe.header.prestadorId);
        this.operadoraId.set(detalhe.header.operadoraId);
      },
      error: () => {
        this.erroValidacao.set('Erro ao carregar recurso.');
      },
    });
  }
}
