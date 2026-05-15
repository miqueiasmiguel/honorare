import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { DemonstrativoService } from '../demonstrativo.service';
import { GuiaService } from '../guia.service';
import type { DemonstrativoDto, ItemDemonstrativoDto } from '../demonstrativo.types';
import type { GuiaDetalheItem } from '../guia.types';

@Component({
  selector: 'app-conciliacao',
  template: `
    <div class="conciliacao">
      <div class="conciliacao__header">
        <h2 class="conciliacao__operadora">{{ header()?.operadoraNome }}</h2>
        <span class="conciliacao__competencia">Competência: {{ header()?.competencia }}</span>
        <span class="conciliacao__progresso">
          {{ totalConciliados() }} de {{ itens().length }} itens conciliados
        </span>
      </div>

      @if (erroOperacao()) {
        <p class="conciliacao__erro">{{ erroOperacao() }}</p>
      }

      <div class="conciliacao__lista">
        @for (item of itens(); track item.id) {
          <div class="conciliacao__item">
            <span class="conciliacao__item-senha">{{ item.senha }}</span>
            <span class="conciliacao__item-codigo-tuss">{{ item.codigoTuss }}</span>
            <span class="conciliacao__item-valor-apresentado">{{ item.valorApresentado }}</span>
            <span class="conciliacao__item-valor-pago">{{ item.valorPago }}</span>
            <span class="conciliacao__item-valor-glosado">{{ item.valorGlosado }}</span>
            @if (item.motivoGlosa) {
              <span class="conciliacao__item-motivo-glosa">{{ item.motivoGlosa }}</span>
            }

            @if (item.conciliado) {
              <span class="conciliacao__badge conciliacao__badge--conciliado">Conciliado</span>
              <button
                class="conciliacao__btn-desvincular"
                type="button"
                (click)="desconciliar(item.id)"
              >
                Desvincular
              </button>
            } @else {
              <span class="conciliacao__badge conciliacao__badge--pendente">Pendente</span>
              <button class="conciliacao__btn-vincular" type="button" (click)="abrirBusca(item.id)">
                Vincular
              </button>
            }

            @if (itemBuscandoId() === item.id) {
              <div class="conciliacao__busca">
                <input
                  class="conciliacao__busca-input"
                  type="text"
                  placeholder="Senha da guia"
                  [value]="buscaSenha()"
                  (input)="onBuscaChange($any($event.target).value)"
                />
                @for (guia of guiasEncontradas(); track guia.id) {
                  @for (ig of guia.itens; track ig.id) {
                    <div class="conciliacao__guia-resultado">
                      <span class="conciliacao__guia-prestador">{{ guia.prestadorNome }}</span>
                      <span class="conciliacao__guia-data">{{ guia.dataAtendimento }}</span>
                      <span class="conciliacao__guia-tuss">{{ ig.codigoTuss }}</span>
                      <span class="conciliacao__guia-posicao">{{ ig.posicaoExecutor }}</span>
                      <span class="conciliacao__guia-valor">{{ ig.valorApurado }}</span>
                      <button
                        class="conciliacao__btn-confirmar-vincular"
                        type="button"
                        (click)="vincular(item.id, ig.id)"
                      >
                        Vincular
                      </button>
                    </div>
                  }
                }
                @if (guiasEncontradas().length === 0 && buscaSenha().length > 0) {
                  <p class="conciliacao__busca-vazia">Nenhuma guia encontrada.</p>
                }
              </div>
            }
          </div>
        }
      </div>
    </div>
  `,
  styleUrl: './conciliacao.component.scss',
  imports: [],
})
export class ConciliacaoComponent implements OnInit {
  private readonly _service = inject(DemonstrativoService);
  private readonly _guiaService = inject(GuiaService);
  private readonly _route = inject(ActivatedRoute);
  private _debounceTimer: ReturnType<typeof setTimeout> | null = null;

  readonly demonstrativoId = signal<string | null>(null);
  readonly header = signal<DemonstrativoDto | null>(null);
  readonly itens = signal<ItemDemonstrativoDto[]>([]);
  readonly erroOperacao = signal('');
  readonly itemBuscandoId = signal<string | null>(null);
  readonly buscaSenha = signal('');
  readonly guiasEncontradas = signal<GuiaDetalheItem[]>([]);

  readonly totalConciliados = computed(() => this.itens().filter((i) => i.conciliado).length);

  ngOnInit(): void {
    const id = this._route.snapshot.paramMap.get('id');
    if (id) {
      this.demonstrativoId.set(id);
      this._carregar(id);
    }
  }

  abrirBusca(itemId: string): void {
    this.itemBuscandoId.set(itemId);
    this.buscaSenha.set('');
    this.guiasEncontradas.set([]);
  }

  onBuscaChange(valor: string): void {
    this.buscaSenha.set(valor);
    if (this._debounceTimer !== null) {
      clearTimeout(this._debounceTimer);
    }
    if (!valor.trim()) {
      this.guiasEncontradas.set([]);
      return;
    }
    this._debounceTimer = setTimeout(() => {
      this._buscarGuias(valor);
    }, 400);
  }

  vincular(itemDemId: string, itemGuiaId: string): void {
    const demId = this.demonstrativoId();
    if (!demId) {
      return;
    }
    this.erroOperacao.set('');
    this._service.conciliarItem(demId, itemDemId, itemGuiaId).subscribe({
      next: () => {
        this.itens.update((prev) =>
          prev.map((i) => (i.id === itemDemId ? { ...i, conciliado: true, itemGuiaId } : i)),
        );
        this.itemBuscandoId.set(null);
      },
      error: () => {
        this.erroOperacao.set('Erro ao vincular item. Tente novamente.');
      },
    });
  }

  desconciliar(itemDemId: string): void {
    const demId = this.demonstrativoId();
    if (!demId) {
      return;
    }
    this.erroOperacao.set('');
    this._service.desconciliarItem(demId, itemDemId).subscribe({
      next: () => {
        this.itens.update((prev) =>
          prev.map((i) => (i.id === itemDemId ? { ...i, conciliado: false, itemGuiaId: null } : i)),
        );
      },
      error: () => {
        this.erroOperacao.set('Erro ao desvincular item. Tente novamente.');
      },
    });
  }

  private _carregar(id: string): void {
    this._service.obterPorId(id).subscribe({
      next: (detalhe) => {
        this.header.set(detalhe.header);
        this.itens.set(detalhe.itens);
      },
      error: () => {
        this.erroOperacao.set('Erro ao carregar o demonstrativo.');
      },
    });
  }

  private _buscarGuias(senha: string): void {
    this._guiaService.listar({ pagina: 1, itensPorPagina: 10 }).subscribe({
      next: (result) => {
        const senhaLower = senha.toLowerCase();
        const matching = result.itens.filter((g) => g.senha.toLowerCase().includes(senhaLower));
        if (matching.length === 0) {
          this.guiasEncontradas.set([]);
          return;
        }
        this._guiaService.obterPorId(matching[0].id).subscribe({
          next: (detalhe) => {
            this.guiasEncontradas.set([detalhe]);
          },
          error: () => {
            /* silent */
          },
        });
      },
      error: () => {
        /* silent */
      },
    });
  }
}
