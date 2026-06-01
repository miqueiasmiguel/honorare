import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { BeneficiarioAutocompleteComponent } from '../../catalog/beneficiarios/beneficiario-autocomplete/beneficiario-autocomplete.component';
import type { BeneficiarioItem, OperadoraItem, PrestadorItem } from '../../catalog/catalog.types';
import { CatalogService } from '../../catalog/catalog.service';
import { CalculoDetalheComponent } from '../calculo-detalhe/calculo-detalhe.component';
import { GuiaService } from '../guia.service';
import type {
  Acomodacao,
  GuiaCalculoResult,
  ItemGuiaDisplay,
  PosicaoExecutor,
  SituacaoGuia,
  ViaAcesso,
} from '../guia.types';
import { ItemGuiaFormComponent } from './item-guia-form/item-guia-form.component';

@Component({
  selector: 'app-guia-form',
  template: `
    <form class="guia-form" (submit)="onSubmit($event)">
      <!-- Topbar: título + badge de situação + ações -->
      <div class="guia-form__topbar">
        <div class="guia-form__topbar-info">
          <h1 class="guia-form__topbar-titulo">
            {{ modoEditar() ? 'Detalhes da Guia' : 'Nova Guia' }}
          </h1>
          @if (modoEditar() && situacao()) {
            <span
              class="guia-form__situacao-badge"
              [class.guia-form__situacao-badge--positivo]="situacao() === 'Liquidada'"
              [class.guia-form__situacao-badge--atencao]="situacao() === 'Apresentada'"
              [class.guia-form__situacao-badge--neutro]="situacao() === 'EmRecurso'"
              >{{ situacaoLabel() }}</span
            >
          }
        </div>
        <div class="guia-form__topbar-acoes">
          <button type="button" class="guia-form__btn-cancelar" (click)="cancelar()">
            Cancelar
          </button>
          <button type="submit" class="guia-form__btn-salvar">
            {{ modoEditar() ? 'Salvar' : 'Criar Guia' }}
          </button>
        </div>
      </div>

      @if (erroValidacao()) {
        <div class="guia-form__erro-banner">
          <span class="guia-form__erro-validacao">{{ erroValidacao() }}</span>
        </div>
      }

      <!-- Seção: Identificação -->
      <section class="guia-form__secao">
        <h2 class="guia-form__secao-titulo">Identificação</h2>
        <div class="guia-form__grade">
          <div class="guia-form__campo">
            @if (modoEditar()) {
              <span class="guia-form__label">Prestador</span>
              <div class="guia-form__campo-readonly">{{ prestadorNomeExibicao() }}</div>
            } @else {
              <label class="guia-form__label" for="prestador-id">Prestador</label>
              <select
                id="prestador-id"
                class="guia-form__select--prestador"
                (change)="prestadorId.set($any($event.target).value)"
              >
                <option value="" [selected]="!prestadorId()">Selecione um prestador</option>
                @for (p of prestadores(); track p.id) {
                  <option [value]="p.id" [selected]="p.id === prestadorId()">{{ p.nome }}</option>
                }
              </select>
            }
          </div>

          <div class="guia-form__campo">
            <label class="guia-form__label" for="operadora-id">Operadora</label>
            <select
              id="operadora-id"
              class="guia-form__select--operadora"
              (change)="operadoraId.set($any($event.target).value)"
            >
              <option value="" [selected]="!operadoraId()">Selecione uma operadora</option>
              @for (o of operadoras(); track o.id) {
                <option [value]="o.id" [selected]="o.id === operadoraId()">{{ o.nome }}</option>
              }
            </select>
          </div>

          <div class="guia-form__campo guia-form__campo--full">
            <app-beneficiario-autocomplete
              label="Beneficiário"
              [initialBeneficiario]="beneficiarioAtual()"
              (beneficiarioChange)="onBeneficiarioChange($event)"
            />
          </div>
        </div>
      </section>

      <!-- Seção: Atendimento -->
      <section class="guia-form__secao">
        <h2 class="guia-form__secao-titulo">Atendimento</h2>
        <div class="guia-form__grade">
          <div class="guia-form__campo">
            <label class="guia-form__label" for="numero-guia">Guia</label>
            <input
              type="text"
              id="numero-guia"
              class="guia-form__input--numero-guia"
              [value]="numeroGuia()"
              (input)="numeroGuia.set($any($event.target).value)"
            />
          </div>

          <div class="guia-form__campo">
            <label class="guia-form__label" for="data-atendimento">Data de Atendimento</label>
            <input
              type="date"
              id="data-atendimento"
              class="guia-form__input--data-atendimento"
              [value]="dataAtendimento()"
              (input)="dataAtendimento.set($any($event.target).value)"
            />
          </div>

          <div class="guia-form__campo guia-form__campo--full guia-form__campo--checkbox">
            <input
              type="checkbox"
              id="eh-pacote"
              class="guia-form__checkbox--eh-pacote"
              [checked]="ehPacote()"
              (change)="ehPacote.set($any($event.target).checked)"
            />
            <label class="guia-form__label" for="eh-pacote">É Pacote</label>
          </div>

          <div class="guia-form__campo guia-form__campo--full">
            <label class="guia-form__label" for="observacao">Observação</label>
            <textarea
              id="observacao"
              class="guia-form__textarea--observacao"
              [value]="observacao()"
              (input)="observacao.set($any($event.target).value)"
            ></textarea>
          </div>
        </div>
      </section>

      <!-- Seção: Itens da Guia -->
      <section class="guia-form__secao">
        <div class="guia-form__secao-cabecalho">
          <h2 class="guia-form__secao-titulo guia-form__secao-titulo--inline">Itens da Guia</h2>
          @if (!adicionandoItem()) {
            <button
              type="button"
              class="guia-form__btn-adicionar-item"
              (click)="adicionandoItem.set(true)"
            >
              + Adicionar Item
            </button>
          }
        </div>

        @for (item of itens(); track $index) {
          <div class="guia-form__item">
            <div class="guia-form__item-topo">
              <div class="guia-form__item-identificacao">
                <span class="guia-form__item-codigo">{{ item.codigoTuss ?? '—' }}</span>
                <span class="guia-form__item-descricao">
                  {{ item.descricaoProcedimento ?? 'Procedimento não identificado' }}
                </span>
              </div>
              <button
                type="button"
                class="guia-form__btn-remover-item"
                (click)="removerItem($index)"
                aria-label="Remover item"
              >
                ×
              </button>
            </div>

            <div class="guia-form__item-meta">
              <span class="guia-form__badge">{{ POSICAO_LABELS[item.posicaoExecutor] }}</span>
              <span class="guia-form__badge">{{
                formatarPercentualOrdem(item.percentualOrdem)
              }}</span>
              <span class="guia-form__badge">{{ VIA_LABELS[item.viaAcesso] }}</span>
              <span class="guia-form__badge">{{ ACOMODACAO_LABELS[item.acomodacao] }}</span>
              @if (item.ehUrgencia) {
                <span class="guia-form__badge guia-form__badge--alerta">Urgência</span>
              }
              @if (item.posicaoExecutor === 'Anestesista' && item.tempoAnestesicoMin) {
                <span class="guia-form__badge">{{ item.tempoAnestesicoMin }} min</span>
              }
            </div>

            @if (item.valorApurado !== null || (modoEditar() && item.id)) {
              <div class="guia-form__item-financeiro">
                @if (item.valorApurado !== null) {
                  <div class="guia-form__item-valor-grupo">
                    <span class="guia-form__item-valor-label">Apurado</span>
                    <span class="guia-form__item-valor-num">{{
                      formatarMoeda(item.valorApurado)
                    }}</span>
                  </div>
                }
                @if (modoEditar() && item.id) {
                  <div class="guia-form__item-pagamento">
                    <div class="guia-form__item-pagamento-campo">
                      <label class="guia-form__item-valor-label" [for]="'vliq-' + item.id">
                        VL Liquidado
                      </label>
                      <input
                        class="guia-form__item-valor-input"
                        [id]="'vliq-' + item.id"
                        type="number"
                        step="0.01"
                        min="0"
                        [value]="valoresLiquidadoEmEdicao()[item.id] ?? ''"
                        (input)="onValorLiquidadoInput(item.id, $any($event.target).value)"
                        (blur)="salvarPagamentoItem(item.id)"
                        [disabled]="salvandoPagamento()[item.id]"
                        placeholder="0,00"
                      />
                    </div>
                    <div class="guia-form__item-pagamento-campo">
                      <label class="guia-form__item-valor-label" [for]="'mglosa-' + item.id">
                        Motivo Glosa
                      </label>
                      <input
                        class="guia-form__item-glosa-input"
                        [id]="'mglosa-' + item.id"
                        type="text"
                        [value]="motivosGlosaEmEdicao()[item.id] ?? ''"
                        (input)="onMotivoGlosaInput(item.id, $any($event.target).value)"
                        (blur)="salvarPagamentoItem(item.id)"
                        [disabled]="salvandoPagamento()[item.id]"
                        placeholder="Código de glosa"
                      />
                    </div>
                  </div>
                }
              </div>
            }
          </div>
        }

        @if (adicionandoItem()) {
          <app-item-guia-form
            [ehPacote]="ehPacote()"
            [operadoraId]="operadoraId()"
            (itemChange)="onItemChange($event)"
          />
        }
      </section>

      @if (modoEditar() && calculo() !== null) {
        <section class="guia-form__secao">
          <h2 class="guia-form__secao-titulo">Apuração</h2>
          <app-calculo-detalhe [calculo]="calculo()" />
        </section>
      }
    </form>
  `,
  styleUrl: './guia-form.component.scss',
  imports: [BeneficiarioAutocompleteComponent, ItemGuiaFormComponent, CalculoDetalheComponent],
})
export class GuiaFormComponent implements OnInit {
  private readonly _guiaService = inject(GuiaService);
  private readonly _catalogService = inject(CatalogService);
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);

  readonly POSICAO_LABELS: Record<PosicaoExecutor, string> = {
    Cirurgiao: 'Cirurgião',
    PrimeiroAuxiliar: '1º Auxiliar',
    SegundoAuxiliar: '2º Auxiliar',
    TerceiroAuxiliar: '3º Auxiliar',
    Anestesista: 'Anestesista',
    ClinicoAssistente: 'Clínico Assistente',
  };

  readonly VIA_LABELS: Record<ViaAcesso, string> = {
    Convencional: 'Convencional',
    Videolaparoscopia: 'Videolaparoscopia',
    Endoscopica: 'Endoscópica',
    Percutanea: 'Percutânea',
    NaoAplicavel: 'N/A',
  };

  readonly ACOMODACAO_LABELS: Record<Acomodacao, string> = {
    Enfermaria: 'Enfermaria',
    Apartamento: 'Apartamento',
    Ambulatorial: 'Ambulatorial',
  };

  readonly modoEditar = signal(false);
  readonly guiaId = signal<string | null>(null);
  readonly carregando = signal(false);
  readonly erroValidacao = signal('');
  readonly prestadorNomeExibicao = signal('');

  readonly situacaoLabel = computed(() => {
    const s = this.situacao();
    if (s === 'Liquidada') return 'Liquidada';
    if (s === 'EmRecurso') return 'Em Recurso';
    return 'Apresentada';
  });

  readonly prestadores = signal<PrestadorItem[]>([]);
  readonly operadoras = signal<OperadoraItem[]>([]);

  readonly prestadorId = signal('');
  readonly operadoraId = signal('');
  readonly beneficiarioId = signal('');
  readonly numeroGuia = signal('');
  readonly dataAtendimento = signal('');
  readonly ehPacote = signal(false);
  readonly observacao = signal('');

  readonly beneficiarioAtual = signal<BeneficiarioItem | null>(null);
  readonly itens = signal<ItemGuiaDisplay[]>([]);
  readonly adicionandoItem = signal(false);
  readonly calculo = signal<GuiaCalculoResult | null>(null);
  readonly situacao = signal<SituacaoGuia | null>(null);
  readonly valoresLiquidadoEmEdicao = signal<Record<string, string | undefined>>({});
  readonly motivosGlosaEmEdicao = signal<Record<string, string | undefined>>({});
  readonly salvandoPagamento = signal<Record<string, boolean>>({});

  ngOnInit(): void {
    const id = this._route.snapshot.paramMap.get('id');

    if (id) {
      this.modoEditar.set(true);
      this.guiaId.set(id);
      this.carregando.set(true);

      forkJoin({
        prestadores: this._catalogService.listarPrestadores({
          ativo: true,
          pagina: 1,
          itensPorPagina: 200,
        }),
        operadoras: this._catalogService.listarOperadoras({
          ativa: true,
          pagina: 1,
          itensPorPagina: 200,
        }),
        guia: this._guiaService.obterPorId(id),
      }).subscribe({
        next: ({ prestadores, operadoras, guia }) => {
          this.prestadores.set(prestadores.itens);
          this.operadoras.set(operadoras.itens);
          this.prestadorId.set(guia.prestadorId);
          this.prestadorNomeExibicao.set(guia.prestadorNome);
          this.operadoraId.set(guia.operadoraId);
          this.beneficiarioId.set(guia.beneficiarioId ?? '');
          this.beneficiarioAtual.set(
            guia.beneficiarioId
              ? {
                  id: guia.beneficiarioId,
                  carteira: guia.beneficiarioCarteira,
                  nome: guia.beneficiarioNome,
                  criadoEm: '',
                }
              : null,
          );
          this.numeroGuia.set(guia.numeroGuia);
          this.dataAtendimento.set(guia.dataAtendimento);
          this.ehPacote.set(guia.ehPacote);
          this.observacao.set(guia.observacao);
          this.situacao.set(guia.situacao);
          this.itens.set(
            guia.itens.map((i) => ({
              id: i.id,
              procedimentoId: i.procedimentoId,
              posicaoExecutor: i.posicaoExecutor,
              percentualOrdem: i.percentualOrdem,
              viaAcesso: i.viaAcesso,
              acomodacao: i.acomodacao,
              ehUrgencia: i.ehUrgencia,
              valorApurado: i.valorApurado,
              tempoAnestesicoMin: i.tempoAnestesicoMin ?? null,
              codigoTuss: i.codigoTuss,
              descricaoProcedimento: i.descricaoProcedimento,
              valorLiquidado: i.valorLiquidado,
              motivoGlosa: i.motivoGlosa,
            })),
          );
          const liquidadoMap: Record<string, string> = {};
          const glosaMap: Record<string, string> = {};
          guia.itens.forEach((i) => {
            liquidadoMap[i.id] = i.valorLiquidado != null ? String(i.valorLiquidado) : '';
            glosaMap[i.id] = i.motivoGlosa ?? '';
          });
          this.valoresLiquidadoEmEdicao.set(liquidadoMap);
          this.motivosGlosaEmEdicao.set(glosaMap);
          this.carregando.set(false);
        },
        error: () => {
          this.erroValidacao.set('Erro ao carregar a guia.');
          this.carregando.set(false);
        },
      });

      this._carregarCalculo(id);
    } else {
      forkJoin({
        prestadores: this._catalogService.listarPrestadores({
          ativo: true,
          pagina: 1,
          itensPorPagina: 200,
        }),
        operadoras: this._catalogService.listarOperadoras({
          ativa: true,
          pagina: 1,
          itensPorPagina: 200,
        }),
      }).subscribe({
        next: ({ prestadores, operadoras }) => {
          this.prestadores.set(prestadores.itens);
          this.operadoras.set(operadoras.itens);
        },
        error: () => {
          this.erroValidacao.set('Erro ao carregar dados do catálogo.');
        },
      });
    }
  }

  onBeneficiarioChange(b: BeneficiarioItem | null): void {
    this.beneficiarioId.set(b?.id ?? '');
    this.beneficiarioAtual.set(b);
  }

  onItemChange(item: ItemGuiaDisplay | null): void {
    if (item !== null) {
      this.itens.update((prev) => [...prev, item]);
    }
    this.adicionandoItem.set(false);
  }

  removerItem(index: number): void {
    this.itens.update((prev) => prev.filter((_, i) => i !== index));
  }

  formatarPercentualOrdem(valor: number): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'percent',
      minimumFractionDigits: 0,
      maximumFractionDigits: 2,
    }).format(valor);
  }

  formatarMoeda(value: number | null): string {
    if (value === null) return '';
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(value);
  }

  cancelar(): void {
    void this._router.navigate(['/admin/guias']);
  }

  onValorLiquidadoInput(itemId: string, value: string): void {
    this.valoresLiquidadoEmEdicao.update((m) => ({ ...m, [itemId]: value }));
  }

  onMotivoGlosaInput(itemId: string, value: string): void {
    this.motivosGlosaEmEdicao.update((m) => ({ ...m, [itemId]: value }));
  }

  salvarPagamentoItem(itemId: string): void {
    const guiaId = this.guiaId();
    if (!guiaId) {
      return;
    }
    const rawValor = this.valoresLiquidadoEmEdicao()[itemId] ?? '';
    const valorLiquidado = rawValor === '' ? null : parseFloat(rawValor.replace(',', '.'));
    // eslint-disable-next-line @typescript-eslint/prefer-nullish-coalescing
    const motivoGlosa = this.motivosGlosaEmEdicao()[itemId] || null;

    this.salvandoPagamento.update((m) => ({ ...m, [itemId]: true }));
    this._guiaService
      .atualizarPagamentoItem(guiaId, itemId, valorLiquidado, motivoGlosa)
      .subscribe({
        next: (itemAtualizado) => {
          this.itens.update((items) =>
            items.map((i) =>
              i.id === itemId
                ? {
                    ...i,
                    valorLiquidado: itemAtualizado.valorLiquidado,
                    motivoGlosa: itemAtualizado.motivoGlosa,
                  }
                : i,
            ),
          );
          const todosLiquidados = this.itens().every((i) => i.valorLiquidado != null);
          this.situacao.set(todosLiquidados ? 'Liquidada' : 'Apresentada');
          this.salvandoPagamento.update((m) => ({ ...m, [itemId]: false }));
        },
        error: () => {
          this.erroValidacao.set('Erro ao salvar pagamento do item.');
          this.salvandoPagamento.update((m) => ({ ...m, [itemId]: false }));
        },
      });
  }

  onSubmit(event: Event): void {
    event.preventDefault();
    this.erroValidacao.set('');

    if (!this.prestadorId()) {
      this.erroValidacao.set('Selecione um prestador.');
      return;
    }
    if (!this.operadoraId()) {
      this.erroValidacao.set('Selecione uma operadora.');
      return;
    }
    if (!this.dataAtendimento()) {
      this.erroValidacao.set('Informe a data de atendimento.');
      return;
    }
    if (this.itens().length === 0) {
      this.erroValidacao.set('A guia deve ter pelo menos um item.');
      return;
    }

    const itens = this.itens();

    const editId = this.guiaId();
    if (this.modoEditar() && editId !== null) {
      this._guiaService
        .atualizar(editId, {
          operadoraId: this.operadoraId(),
          beneficiarioId: this.beneficiarioId() || null,
          numeroGuia: this.numeroGuia(),
          dataAtendimento: this.dataAtendimento(),
          ehPacote: this.ehPacote(),
          observacao: this.observacao(),
          itens,
        })
        .subscribe({
          next: () => {
            void this._router.navigate(['/admin/guias']);
          },
          error: (err: HttpErrorResponse) => {
            this.erroValidacao.set(
              (err.error as { detail?: string } | null)?.detail ??
                'Erro ao salvar a guia. Verifique os dados e tente novamente.',
            );
          },
        });
    } else {
      this._guiaService
        .criar({
          prestadorId: this.prestadorId(),
          operadoraId: this.operadoraId(),
          beneficiarioId: this.beneficiarioId() || null,
          numeroGuia: this.numeroGuia(),
          dataAtendimento: this.dataAtendimento(),
          ehPacote: this.ehPacote(),
          observacao: this.observacao(),
          itens,
        })
        .subscribe({
          next: () => {
            void this._router.navigate(['/admin/guias']);
          },
          error: (err: HttpErrorResponse) => {
            this.erroValidacao.set(
              (err.error as { detail?: string } | null)?.detail ??
                'Erro ao criar a guia. Verifique os dados e tente novamente.',
            );
          },
        });
    }
  }

  private _carregarCalculo(id: string): void {
    this._guiaService.obterCalculo(id).subscribe({
      next: (result) => {
        this.calculo.set(result);
      },
      error: () => {
        this.calculo.set(null);
      },
    });
  }
}
