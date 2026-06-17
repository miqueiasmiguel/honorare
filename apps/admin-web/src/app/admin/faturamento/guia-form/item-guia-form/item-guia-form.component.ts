import { Component, DestroyRef, inject, input, OnInit, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, Subject } from 'rxjs';
import { CatalogService } from '../../../catalog/catalog.service';
import type { ProcedimentoItem } from '../../../catalog/catalog.types';
import type { Acomodacao, ItemGuiaDisplay, PosicaoExecutor, ViaAcesso } from '../../guia.types';

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

      @if (!semCalculo()) {
        <div class="item-guia-form__field">
          <label class="item-guia-form__label" for="posicao-executor">Posição do Executor</label>
          <select
            id="posicao-executor"
            class="item-guia-form__select--posicao"
            (change)="onPosicaoChange($any($event.target).value)"
          >
            <option value="Cirurgiao" [selected]="posicaoExecutor() === 'Cirurgiao'">
              Cirurgião
            </option>
            <option value="PrimeiroAuxiliar" [selected]="posicaoExecutor() === 'PrimeiroAuxiliar'">
              1º Auxiliar
            </option>
            <option value="SegundoAuxiliar" [selected]="posicaoExecutor() === 'SegundoAuxiliar'">
              2º Auxiliar
            </option>
            <option value="TerceiroAuxiliar" [selected]="posicaoExecutor() === 'TerceiroAuxiliar'">
              3º Auxiliar
            </option>
            <option value="Anestesista" [selected]="posicaoExecutor() === 'Anestesista'">
              Anestesista
            </option>
            <option
              value="ClinicoAssistente"
              [selected]="posicaoExecutor() === 'ClinicoAssistente'"
            >
              Clínico Assistente
            </option>
          </select>
        </div>

        <div class="item-guia-form__field">
          <label class="item-guia-form__label" for="via-acesso">Via de Acesso</label>
          <select
            id="via-acesso"
            class="item-guia-form__select--via"
            (change)="onViaChange($any($event.target).value)"
          >
            <option value="Convencional" [selected]="viaAcesso() === 'Convencional'">
              Convencional
            </option>
            <option value="Videolaparoscopia" [selected]="viaAcesso() === 'Videolaparoscopia'">
              Videolaparoscopia
            </option>
            <option value="Endoscopica" [selected]="viaAcesso() === 'Endoscopica'">
              Endoscópica
            </option>
            <option value="Percutanea" [selected]="viaAcesso() === 'Percutanea'">Percutânea</option>
            <option value="NaoAplicavel" [selected]="viaAcesso() === 'NaoAplicavel'">
              Não Aplicável
            </option>
          </select>
        </div>

        <div class="item-guia-form__field">
          <label class="item-guia-form__label" for="acomodacao">Acomodação</label>
          <select
            id="acomodacao"
            class="item-guia-form__select--acomodacao"
            (change)="onAcomodacaoChange($any($event.target).value)"
          >
            <option value="Enfermaria" [selected]="acomodacao() === 'Enfermaria'">
              Enfermaria
            </option>
            <option value="Apartamento" [selected]="acomodacao() === 'Apartamento'">
              Apartamento
            </option>
            <option value="Ambulatorial" [selected]="acomodacao() === 'Ambulatorial'">
              Ambulatorial
            </option>
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

        @if (posicaoExecutor() === 'Anestesista') {
          <div class="item-guia-form__field">
            <label class="item-guia-form__label" for="tempo-anestesico"
              >Tempo anestésico (min)</label
            >
            <input
              type="number"
              id="tempo-anestesico"
              data-testid="tempo-anestesico"
              class="item-guia-form__input--tempo-anestesico"
              min="0"
              [value]="tempoAnestesicoMin() ?? ''"
              (input)="onTempoAnestesicoChange($any($event.target).value)"
            />
          </div>
        }
      }

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
  styleUrl: './item-guia-form.component.scss',
})
export class ItemGuiaFormComponent implements OnInit {
  private readonly _catalogService = inject(CatalogService);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _busca$ = new Subject<string>();

  readonly ehPacote = input<boolean>(false);
  readonly semCalculo = input<boolean>(false);
  readonly item = input<ItemGuiaDisplay | null>(null);
  readonly operadoraId = input<string>('');
  readonly itemChange = output<ItemGuiaDisplay | null>();

  readonly procedimentoId = signal('');
  readonly procedimentoBusca = signal('');
  readonly procedimentoSelecionado = signal<ProcedimentoItem | null>(null);
  readonly procedimentosSugestoes = signal<ProcedimentoItem[]>([]);
  readonly buscandoProcedimento = signal(false);

  readonly posicaoExecutor = signal<PosicaoExecutor>('Cirurgiao');
  readonly viaAcesso = signal<ViaAcesso>('Convencional');
  readonly acomodacao = signal<Acomodacao>('Enfermaria');
  readonly ehUrgencia = signal(false);
  readonly valorApurado = signal('');
  readonly tempoAnestesicoMin = signal<number | null>(null);

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
      this.viaAcesso.set(current.viaAcesso);
      this.acomodacao.set(current.acomodacao);
      this.ehUrgencia.set(current.ehUrgencia);
      this.valorApurado.set(current.valorApurado !== null ? String(current.valorApurado) : '');
      this.tempoAnestesicoMin.set(current.tempoAnestesicoMin ?? null);
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

  onViaChange(value: string): void {
    this.viaAcesso.set(value as ViaAcesso);
  }

  onAcomodacaoChange(value: string): void {
    this.acomodacao.set(value as Acomodacao);
  }

  onUrgenciaChange(checked: boolean): void {
    this.ehUrgencia.set(checked);
  }

  onTempoAnestesicoChange(value: string): void {
    this.tempoAnestesicoMin.set(value ? parseInt(value, 10) : null);
  }

  onFormSubmit(event: Event): void {
    event.preventDefault();
    const valorApuradoStr = this.valorApurado();
    const proc = this.procedimentoSelecionado();
    this.itemChange.emit({
      procedimentoId: this.procedimentoId(),
      posicaoExecutor: this.posicaoExecutor(),
      viaAcesso: this.viaAcesso(),
      acomodacao: this.acomodacao(),
      ehUrgencia: this.ehUrgencia(),
      valorApurado: valorApuradoStr ? parseFloat(valorApuradoStr) : null,
      tempoAnestesicoMin: this.tempoAnestesicoMin(),
      codigoTuss: proc?.codigoTuss,
      descricaoProcedimento: proc?.descricao,
    });
  }

  onCancelar(): void {
    this.itemChange.emit(null);
  }
}
