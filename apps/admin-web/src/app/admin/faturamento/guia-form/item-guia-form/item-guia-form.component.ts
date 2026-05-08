import { Component, DestroyRef, inject, input, OnInit, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, Subject } from 'rxjs';
import { CatalogService } from '../../../catalog/catalog.service';
import type { ProcedimentoItem } from '../../../catalog/catalog.types';
import type {
  Acomodacao,
  CriarItemGuiaPayload,
  OrdemProcedimento,
  PosicaoExecutor,
  ViaAcesso,
} from '../../guia.types';

@Component({
  selector: 'app-item-guia-form',
  template: `
    <form class="item-guia-form" (submit)="onFormSubmit($event)">
      <div class="item-guia-form__field">
        <label class="item-guia-form__label" for="busca-procedimento">Procedimento (TUSS)</label>
        <input
          type="text"
          id="busca-procedimento"
          class="item-guia-form__input--busca-procedimento"
          [value]="procedimentoBusca()"
          placeholder="Código ou descrição TUSS"
          (input)="onBuscaChange($any($event.target).value)"
        />
        @if (buscandoProcedimento()) {
          <span class="item-guia-form__spinner" aria-label="Buscando procedimentos"></span>
        }
        @if (procedimentosSugestoes().length > 0) {
          <ul class="item-guia-form__sugestoes">
            @for (p of procedimentosSugestoes(); track p.id) {
              <li class="item-guia-form__sugestao">
                <button
                  type="button"
                  class="item-guia-form__sugestao-btn"
                  (click)="selecionarProcedimento(p)"
                >
                  {{ p.codigoTuss }} — {{ p.descricao }}
                </button>
              </li>
            }
          </ul>
        }
        @if (procedimentoSelecionado() !== null) {
          <span class="item-guia-form__procedimento-selecionado">
            {{ procedimentoSelecionado()?.codigoTuss }} — {{ procedimentoSelecionado()?.descricao }}
          </span>
        }
      </div>

      <div class="item-guia-form__field">
        <label class="item-guia-form__label" for="posicao-executor">Posição do Executor</label>
        <select
          id="posicao-executor"
          class="item-guia-form__select--posicao"
          [value]="posicaoExecutor()"
          (change)="onPosicaoChange($any($event.target).value)"
        >
          <option value="Cirurgiao">Cirurgião</option>
          <option value="PrimeiroAuxiliar">1º Auxiliar</option>
          <option value="SegundoAuxiliar">2º Auxiliar</option>
          <option value="TerceiroAuxiliar">3º Auxiliar</option>
          <option value="Anestesista">Anestesista</option>
          <option value="ClinicoAssistente">Clínico Assistente</option>
        </select>
      </div>

      <div class="item-guia-form__field">
        <label class="item-guia-form__label" for="ordem-procedimento">Ordem</label>
        <select
          id="ordem-procedimento"
          class="item-guia-form__select--ordem"
          [value]="ordemProcedimento()"
          (change)="onOrdemChange($any($event.target).value)"
        >
          <option value="Unico">Único</option>
          <option value="Principal">Principal</option>
          <option value="SecundarioMesmaVia">Secundário (mesma via)</option>
          <option value="SecundarioViaDiferente">Secundário (via diferente)</option>
        </select>
      </div>

      <div class="item-guia-form__field">
        <label class="item-guia-form__label" for="via-acesso">Via de Acesso</label>
        <select
          id="via-acesso"
          class="item-guia-form__select--via"
          [value]="viaAcesso()"
          (change)="onViaChange($any($event.target).value)"
        >
          <option value="Convencional">Convencional</option>
          <option value="Videolaparoscopia">Videolaparoscopia</option>
          <option value="Endoscopica">Endoscópica</option>
          <option value="Percutanea">Percutânea</option>
          <option value="NaoAplicavel">Não Aplicável</option>
        </select>
      </div>

      <div class="item-guia-form__field">
        <label class="item-guia-form__label" for="acomodacao">Acomodação</label>
        <select
          id="acomodacao"
          class="item-guia-form__select--acomodacao"
          [value]="acomodacao()"
          (change)="onAcomodacaoChange($any($event.target).value)"
        >
          <option value="Enfermaria">Enfermaria</option>
          <option value="Apartamento">Apartamento</option>
          <option value="Ambulatorial">Ambulatorial</option>
        </select>
      </div>

      <div class="item-guia-form__field item-guia-form__field--checkbox">
        <input
          type="checkbox"
          id="eh-urgencia"
          class="item-guia-form__checkbox--urgencia"
          [checked]="ehUrgencia()"
          (change)="onUrgenciaChange($any($event.target).checked)"
        />
        <label class="item-guia-form__label" for="eh-urgencia">Urgência</label>
      </div>

      @if (ehPacote()) {
        <div class="item-guia-form__field">
          <label class="item-guia-form__label" for="valor-apurado">Valor Apurado (R$)</label>
          <input
            type="number"
            id="valor-apurado"
            class="item-guia-form__input--valor-apurado"
            required
            min="0"
            step="0.01"
            [value]="valorApurado()"
            (input)="valorApurado.set($any($event.target).value)"
          />
        </div>
      }

      <div class="item-guia-form__actions">
        <button type="button" class="item-guia-form__btn-cancelar" (click)="onCancelar()">
          Cancelar
        </button>
        <button type="submit" class="item-guia-form__btn-salvar">Salvar Item</button>
      </div>
    </form>
  `,
  styles: [
    `
      .item-guia-form {
        display: flex;
        flex-direction: column;
        gap: 16px;
      }
      .item-guia-form__field {
        display: flex;
        flex-direction: column;
        gap: 4px;
      }
      .item-guia-form__field--checkbox {
        flex-direction: row;
        align-items: center;
        gap: 8px;
      }
      .item-guia-form__label {
        font-weight: 500;
      }
      .item-guia-form__actions {
        display: flex;
        gap: 8px;
        justify-content: flex-end;
        padding-top: 8px;
      }
      .item-guia-form__sugestoes {
        list-style: none;
        padding: 0;
        margin: 0;
        border: 1px solid var(--color-borda);
      }
      .item-guia-form__sugestao-btn {
        width: 100%;
        text-align: left;
        background: none;
        border: none;
        cursor: pointer;
        padding: 8px 12px;
      }
      .item-guia-form__sugestao-btn:hover {
        background: var(--color-superficie);
      }
    `,
  ],
})
export class ItemGuiaFormComponent implements OnInit {
  private readonly _catalogService = inject(CatalogService);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _busca$ = new Subject<string>();

  readonly ehPacote = input<boolean>(false);
  readonly item = input<CriarItemGuiaPayload | null>(null);
  readonly itemChange = output<CriarItemGuiaPayload | null>();

  readonly procedimentoId = signal('');
  readonly procedimentoBusca = signal('');
  readonly procedimentoSelecionado = signal<ProcedimentoItem | null>(null);
  readonly procedimentosSugestoes = signal<ProcedimentoItem[]>([]);
  readonly buscandoProcedimento = signal(false);

  readonly posicaoExecutor = signal<PosicaoExecutor>('Cirurgiao');
  readonly ordemProcedimento = signal<OrdemProcedimento>('Unico');
  readonly viaAcesso = signal<ViaAcesso>('Convencional');
  readonly acomodacao = signal<Acomodacao>('Enfermaria');
  readonly ehUrgencia = signal(false);
  readonly valorApurado = signal('');

  ngOnInit(): void {
    this._busca$
      .pipe(debounceTime(300), takeUntilDestroyed(this._destroyRef))
      .subscribe((busca) => {
        if (!busca.trim()) {
          this.procedimentosSugestoes.set([]);
          this.buscandoProcedimento.set(false);
          return;
        }
        this.buscandoProcedimento.set(true);
        this._catalogService
          .listarProcedimentos({ busca, pagina: 1, itensPorPagina: 10 })
          .subscribe({
            next: (result) => {
              this.procedimentosSugestoes.set(result.itens);
              this.buscandoProcedimento.set(false);
            },
            error: () => {
              this.procedimentosSugestoes.set([]);
              this.buscandoProcedimento.set(false);
            },
          });
      });

    const current = this.item();
    if (current !== null) {
      this.procedimentoId.set(current.procedimentoId);
      this.posicaoExecutor.set(current.posicaoExecutor);
      this.ordemProcedimento.set(current.ordemProcedimento);
      this.viaAcesso.set(current.viaAcesso);
      this.acomodacao.set(current.acomodacao);
      this.ehUrgencia.set(current.ehUrgencia);
      this.valorApurado.set(current.valorApurado !== null ? String(current.valorApurado) : '');
    }
  }

  onBuscaChange(value: string): void {
    this.procedimentoBusca.set(value);
    this._busca$.next(value);
  }

  selecionarProcedimento(p: ProcedimentoItem): void {
    this.procedimentoId.set(p.id);
    this.procedimentoBusca.set(`${p.codigoTuss} — ${p.descricao}`);
    this.procedimentoSelecionado.set(p);
    this.procedimentosSugestoes.set([]);
  }

  onPosicaoChange(value: string): void {
    this.posicaoExecutor.set(value as PosicaoExecutor);
  }

  onOrdemChange(value: string): void {
    this.ordemProcedimento.set(value as OrdemProcedimento);
  }

  onViaChange(value: string): void {
    this.viaAcesso.set(value as ViaAcesso);
  }

  onAcomodacaoChange(value: string): void {
    this.acomodacao.set(value as Acomodacao);
  }

  onUrgenciaChange(checked: boolean): void {
    this.ehUrgencia.set(checked);
  }

  onFormSubmit(event: Event): void {
    event.preventDefault();
    const valorApuradoStr = this.valorApurado();
    this.itemChange.emit({
      procedimentoId: this.procedimentoId(),
      posicaoExecutor: this.posicaoExecutor(),
      ordemProcedimento: this.ordemProcedimento(),
      viaAcesso: this.viaAcesso(),
      acomodacao: this.acomodacao(),
      ehUrgencia: this.ehUrgencia(),
      valorApurado: valorApuradoStr ? parseFloat(valorApuradoStr) : null,
    });
  }

  onCancelar(): void {
    this.itemChange.emit(null);
  }
}
