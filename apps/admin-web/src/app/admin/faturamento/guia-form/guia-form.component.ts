import { Component, inject, OnInit, signal } from '@angular/core';
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
  ViaAcesso,
} from '../guia.types';
import { ItemGuiaFormComponent } from './item-guia-form/item-guia-form.component';

@Component({
  selector: 'app-guia-form',
  template: `
    <form class="guia-form" (submit)="onSubmit($event)">
      <div class="guia-form__field">
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
      </div>

      <div class="guia-form__field">
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

      <div class="guia-form__field">
        <app-beneficiario-autocomplete
          label="Beneficiário"
          [initialBeneficiario]="beneficiarioAtual()"
          (beneficiarioChange)="onBeneficiarioChange($event)"
        />
      </div>

      <div class="guia-form__field">
        <label class="guia-form__label" for="senha">Senha</label>
        <input
          type="text"
          id="senha"
          class="guia-form__input--senha"
          [value]="senha()"
          (input)="senha.set($any($event.target).value)"
        />
      </div>

      <div class="guia-form__field">
        <label class="guia-form__label" for="data-atendimento">Data de Atendimento</label>
        <input
          type="date"
          id="data-atendimento"
          class="guia-form__input--data-atendimento"
          [value]="dataAtendimento()"
          (input)="dataAtendimento.set($any($event.target).value)"
        />
      </div>

      <div class="guia-form__field guia-form__field--checkbox">
        <input
          type="checkbox"
          id="eh-pacote"
          class="guia-form__checkbox--eh-pacote"
          [checked]="ehPacote()"
          (change)="ehPacote.set($any($event.target).checked)"
        />
        <label class="guia-form__label" for="eh-pacote">É Pacote</label>
      </div>

      <div class="guia-form__field">
        <label class="guia-form__label" for="observacao">Observação</label>
        <textarea
          id="observacao"
          class="guia-form__textarea--observacao"
          [value]="observacao()"
          (input)="observacao.set($any($event.target).value)"
        ></textarea>
      </div>

      <div class="guia-form__itens">
        <h3 class="guia-form__itens-titulo">Itens da Guia</h3>
        @for (item of itens(); track $index) {
          <div class="guia-form__item">
            <div class="guia-form__item-header">
              <span class="guia-form__item-codigo">{{ item.codigoTuss ?? '—' }}</span>
              <span class="guia-form__item-descricao">
                {{ item.descricaoProcedimento ?? 'Procedimento não identificado' }}
              </span>
            </div>
            <div class="guia-form__item-meta">
              <span class="guia-form__item-badge">{{ POSICAO_LABELS[item.posicaoExecutor] }}</span>
              <span class="guia-form__item-badge">{{
                formatarPercentualOrdem(item.percentualOrdem)
              }}</span>
              <span class="guia-form__item-badge">{{ VIA_LABELS[item.viaAcesso] }}</span>
              <span class="guia-form__item-badge">{{ ACOMODACAO_LABELS[item.acomodacao] }}</span>
              @if (item.ehUrgencia) {
                <span class="guia-form__item-badge guia-form__item-badge--urgencia">Urgência</span>
              }
              @if (item.posicaoExecutor === 'Anestesista' && item.tempoAnestesicoMin) {
                <span class="guia-form__item-badge">{{ item.tempoAnestesicoMin }} min</span>
              }
            </div>
            @if (
              item.valorApurado !== null ||
              (item.valorLiquidado !== null && item.valorLiquidado !== undefined)
            ) {
              <div class="guia-form__item-valores">
                @if (item.valorApurado !== null) {
                  <span class="guia-form__item-valor">
                    <span class="guia-form__item-valor-label">Apurado</span>
                    <span class="guia-form__item-valor-num">{{
                      formatarMoeda(item.valorApurado)
                    }}</span>
                  </span>
                }
                @if (item.valorLiquidado !== null && item.valorLiquidado !== undefined) {
                  <span class="guia-form__item-valor">
                    <span class="guia-form__item-valor-label">Liquidado</span>
                    <span class="guia-form__item-valor-num">{{
                      formatarMoeda(item.valorLiquidado)
                    }}</span>
                  </span>
                }
              </div>
            }
            <button type="button" class="guia-form__btn-remover-item" (click)="removerItem($index)">
              Remover
            </button>
          </div>
        }
      </div>

      @if (!adicionandoItem()) {
        <button
          type="button"
          class="guia-form__btn-adicionar-item"
          (click)="adicionandoItem.set(true)"
        >
          Adicionar Item
        </button>
      }

      @if (adicionandoItem()) {
        <app-item-guia-form
          [ehPacote]="ehPacote()"
          [operadoraId]="operadoraId()"
          (itemChange)="onItemChange($event)"
        />
      }

      @if (modoEditar() && calculo() !== null) {
        <section class="guia-form__apuracao">
          <h3 class="guia-form__apuracao-titulo">Apuração</h3>
          <app-calculo-detalhe [calculo]="calculo()" />
        </section>
      }

      @if (erroValidacao()) {
        <span class="guia-form__erro-validacao">{{ erroValidacao() }}</span>
      }

      <div class="guia-form__actions">
        <button type="button" class="guia-form__btn-cancelar" (click)="cancelar()">Cancelar</button>
        <button type="submit" class="guia-form__btn-salvar">
          {{ modoEditar() ? 'Salvar' : 'Criar Guia' }}
        </button>
      </div>
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

  readonly prestadores = signal<PrestadorItem[]>([]);
  readonly operadoras = signal<OperadoraItem[]>([]);

  readonly prestadorId = signal('');
  readonly operadoraId = signal('');
  readonly beneficiarioId = signal('');
  readonly senha = signal('');
  readonly dataAtendimento = signal('');
  readonly ehPacote = signal(false);
  readonly observacao = signal('');

  readonly beneficiarioAtual = signal<BeneficiarioItem | null>(null);
  readonly itens = signal<ItemGuiaDisplay[]>([]);
  readonly adicionandoItem = signal(false);
  readonly calculo = signal<GuiaCalculoResult | null>(null);

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
          this.senha.set(guia.senha);
          this.dataAtendimento.set(guia.dataAtendimento);
          this.ehPacote.set(guia.ehPacote);
          this.observacao.set(guia.observacao);
          this.itens.set(
            guia.itens.map((i) => ({
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
            })),
          );
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
          senha: this.senha(),
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
          senha: this.senha(),
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
