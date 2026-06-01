import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CatalogService } from '../../catalog/catalog.service';
import type { OperadoraItem } from '../../catalog/catalog.types';
import { DemonstrativoService } from '../demonstrativo.service';

interface ItemFormState {
  id: string | null;
  senha: string;
  codigoTuss: string;
  descricao: string | null;
  valorApresentado: number;
  valorPago: number;
  motivoGlosa: string | null;
  conciliado: boolean;
}

@Component({
  selector: 'app-demonstrativo-form',
  template: `
    <form class="demonstrativo-form" (submit)="onSubmit($event)">
      <div class="demonstrativo-form__field">
        <label class="demonstrativo-form__label" for="operadora-id">Operadora</label>
        <select
          id="operadora-id"
          class="demonstrativo-form__select--operadora"
          [value]="operadoraId()"
          (change)="operadoraId.set($any($event.target).value)"
        >
          <option value="">Selecione uma operadora</option>
          @for (o of operadoras(); track o.id) {
            <option [value]="o.id">{{ o.nome }}</option>
          }
        </select>
      </div>

      <div class="demonstrativo-form__field">
        <label class="demonstrativo-form__label" for="competencia">Competência</label>
        <input
          type="month"
          id="competencia"
          class="demonstrativo-form__input--competencia"
          [value]="competencia()"
          (input)="competencia.set($any($event.target).value)"
        />
      </div>

      <div class="demonstrativo-form__field">
        <label class="demonstrativo-form__label" for="data-recebimento">Data de Recebimento</label>
        <input
          type="date"
          id="data-recebimento"
          class="demonstrativo-form__input--data-recebimento"
          [value]="dataRecebimento()"
          (input)="dataRecebimento.set($any($event.target).value)"
        />
      </div>

      <div class="demonstrativo-form__field">
        <label class="demonstrativo-form__label" for="observacao">Observação</label>
        <textarea
          id="observacao"
          class="demonstrativo-form__textarea--observacao"
          [value]="observacao()"
          (input)="observacao.set($any($event.target).value)"
        ></textarea>
      </div>

      <div class="demonstrativo-form__itens">
        <h3 class="demonstrativo-form__itens-titulo">Itens do Demonstrativo</h3>

        @for (item of itens(); track $index) {
          <div
            class="demonstrativo-form__item-card"
            [class.demonstrativo-form__item-card--conciliado]="item.conciliado"
          >
            <div class="demonstrativo-form__item-header">
              <span class="demonstrativo-form__item-numero">Item {{ $index + 1 }}</span>
              @if (item.conciliado) {
                <span
                  class="demonstrativo-form__item-badge demonstrativo-form__item-badge--conciliado"
                >
                  Conciliado
                </span>
              }
              <button
                type="button"
                class="demonstrativo-form__btn-remover-item"
                [disabled]="item.conciliado"
                (click)="removerItem($index)"
              >
                Remover
              </button>
            </div>

            <div class="demonstrativo-form__item-campos demonstrativo-form__item-campos--linha-1">
              <div class="demonstrativo-form__item-campo">
                <label
                  class="demonstrativo-form__item-label"
                  [attr.for]="'item-' + $index + '-senha'"
                >
                  Senha
                </label>
                <input
                  type="text"
                  class="demonstrativo-form__item-input"
                  [id]="'item-' + $index + '-senha'"
                  [value]="item.senha"
                  (input)="atualizarItemSenha($index, $any($event.target).value)"
                />
              </div>
              <div class="demonstrativo-form__item-campo">
                <label
                  class="demonstrativo-form__item-label"
                  [attr.for]="'item-' + $index + '-tuss'"
                >
                  Código TUSS
                </label>
                <input
                  type="text"
                  class="demonstrativo-form__item-input demonstrativo-form__item-input--mono"
                  [id]="'item-' + $index + '-tuss'"
                  [value]="item.codigoTuss"
                  (input)="atualizarItemCodigoTuss($index, $any($event.target).value)"
                />
              </div>
              <div class="demonstrativo-form__item-campo">
                <label
                  class="demonstrativo-form__item-label"
                  [attr.for]="'item-' + $index + '-descricao'"
                >
                  Descrição
                </label>
                <input
                  type="text"
                  class="demonstrativo-form__item-input"
                  [id]="'item-' + $index + '-descricao'"
                  [value]="item.descricao ?? ''"
                  (input)="atualizarItemDescricao($index, $any($event.target).value)"
                />
              </div>
            </div>

            <div class="demonstrativo-form__item-campos demonstrativo-form__item-campos--linha-2">
              <div class="demonstrativo-form__item-campo">
                <label
                  class="demonstrativo-form__item-label"
                  [attr.for]="'item-' + $index + '-vl-apresentado'"
                >
                  Vl. Apresentado (R$)
                </label>
                <input
                  type="number"
                  class="demonstrativo-form__item-input demonstrativo-form__item-input--valor"
                  [id]="'item-' + $index + '-vl-apresentado'"
                  min="0"
                  step="0.01"
                  [value]="item.valorApresentado"
                  (input)="atualizarItemValorApresentado($index, $any($event.target).valueAsNumber)"
                />
              </div>
              <div class="demonstrativo-form__item-campo">
                <label
                  class="demonstrativo-form__item-label"
                  [attr.for]="'item-' + $index + '-vl-pago'"
                >
                  Vl. Pago (R$)
                </label>
                <input
                  type="number"
                  class="demonstrativo-form__item-input demonstrativo-form__item-input--valor"
                  [id]="'item-' + $index + '-vl-pago'"
                  min="0"
                  step="0.01"
                  [value]="item.valorPago"
                  (input)="atualizarItemValorPago($index, $any($event.target).valueAsNumber)"
                />
              </div>
              <div class="demonstrativo-form__item-campo">
                <span class="demonstrativo-form__item-label">Vl. Glosado (R$)</span>
                <span
                  class="demonstrativo-form__valor-glosado"
                  [class.demonstrativo-form__valor-glosado--zero]="
                    item.valorApresentado - item.valorPago === 0
                  "
                >
                  {{ (item.valorApresentado - item.valorPago).toFixed(2) }}
                </span>
              </div>
              <div class="demonstrativo-form__item-campo">
                <label
                  class="demonstrativo-form__item-label"
                  [attr.for]="'item-' + $index + '-motivo-glosa'"
                >
                  Motivo da Glosa
                </label>
                <input
                  type="text"
                  class="demonstrativo-form__item-input"
                  [id]="'item-' + $index + '-motivo-glosa'"
                  [value]="item.motivoGlosa ?? ''"
                  (input)="atualizarItemMotivoGlosa($index, $any($event.target).value)"
                />
              </div>
            </div>
          </div>
        }

        <button
          type="button"
          class="demonstrativo-form__btn-adicionar-item"
          (click)="adicionarItem()"
        >
          + Adicionar Item
        </button>
      </div>

      @if (erroValidacao()) {
        <span class="demonstrativo-form__erro-validacao">{{ erroValidacao() }}</span>
      }

      <div class="demonstrativo-form__actions">
        <button type="button" class="demonstrativo-form__btn-cancelar" (click)="cancelar()">
          Cancelar
        </button>
        <button type="submit" class="demonstrativo-form__btn-salvar">
          {{ modoEditar() ? 'Salvar' : 'Criar Demonstrativo' }}
        </button>
      </div>
    </form>
  `,
  styleUrl: './demonstrativo-form.component.scss',
  imports: [],
})
export class DemonstrativoFormComponent implements OnInit {
  private readonly _service = inject(DemonstrativoService);
  private readonly _catalogService = inject(CatalogService);
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);

  readonly modoEditar = signal(false);
  readonly demonstrativoId = signal<string | null>(null);
  readonly erroValidacao = signal('');

  readonly operadoras = signal<OperadoraItem[]>([]);

  readonly operadoraId = signal('');
  readonly competencia = signal('');
  readonly dataRecebimento = signal('');
  readonly observacao = signal('');

  readonly itens = signal<ItemFormState[]>([]);

  ngOnInit(): void {
    this._carregarOperadoras();

    const id = this._route.snapshot.paramMap.get('id');
    if (id) {
      this.modoEditar.set(true);
      this.demonstrativoId.set(id);
      this._carregarDemonstrativo(id);
    }
  }

  adicionarItem(): void {
    this.itens.update((prev) => [
      ...prev,
      {
        id: null,
        senha: '',
        codigoTuss: '',
        descricao: null,
        valorApresentado: 0,
        valorPago: 0,
        motivoGlosa: null,
        conciliado: false,
      },
    ]);
  }

  removerItem(index: number): void {
    const item = this.itens()[index];
    if (item.conciliado) {
      return;
    }
    this.itens.update((prev) => prev.filter((_, i) => i !== index));
  }

  atualizarItemSenha(index: number, value: string): void {
    this.itens.update((prev) =>
      prev.map((item, i) => (i === index ? { ...item, senha: value } : item)),
    );
  }

  atualizarItemCodigoTuss(index: number, value: string): void {
    this.itens.update((prev) =>
      prev.map((item, i) => (i === index ? { ...item, codigoTuss: value } : item)),
    );
  }

  atualizarItemDescricao(index: number, value: string): void {
    this.itens.update((prev) =>
      prev.map((item, i) => (i === index ? { ...item, descricao: value || null } : item)),
    );
  }

  atualizarItemValorApresentado(index: number, value: number): void {
    this.itens.update((prev) =>
      prev.map((item, i) => (i === index ? { ...item, valorApresentado: value } : item)),
    );
  }

  atualizarItemValorPago(index: number, value: number): void {
    this.itens.update((prev) =>
      prev.map((item, i) => (i === index ? { ...item, valorPago: value } : item)),
    );
  }

  atualizarItemMotivoGlosa(index: number, value: string): void {
    this.itens.update((prev) =>
      prev.map((item, i) => (i === index ? { ...item, motivoGlosa: value || null } : item)),
    );
  }

  cancelar(): void {
    void this._router.navigate(['/admin/demonstrativos']);
  }

  onSubmit(event: Event): void {
    event.preventDefault();
    this.erroValidacao.set('');

    if (!this.operadoraId()) {
      this.erroValidacao.set('Selecione uma operadora.');
      return;
    }
    if (!this.competencia()) {
      this.erroValidacao.set('Informe a competência.');
      return;
    }
    if (!this.dataRecebimento()) {
      this.erroValidacao.set('Informe a data de recebimento.');
      return;
    }

    const payload = {
      operadoraId: this.operadoraId(),
      competencia: this.competencia(),
      dataRecebimento: this.dataRecebimento(),
      observacao: this.observacao() || null,
    };

    const editId = this.demonstrativoId();
    if (this.modoEditar() && editId !== null) {
      this._service.atualizar(editId, payload).subscribe({
        next: () => {
          void this._router.navigate(['/admin/demonstrativos']);
        },
        error: () => {
          this.erroValidacao.set('Erro ao salvar o demonstrativo. Verifique os dados.');
        },
      });
    } else {
      this._service.criar(payload).subscribe({
        next: () => {
          void this._router.navigate(['/admin/demonstrativos']);
        },
        error: () => {
          this.erroValidacao.set('Erro ao criar o demonstrativo. Verifique os dados.');
        },
      });
    }
  }

  private _carregarOperadoras(): void {
    this._catalogService
      .listarOperadoras({ ativa: true, pagina: 1, itensPorPagina: 200 })
      .subscribe({
        next: (result) => {
          this.operadoras.set(result.itens);
        },
        error: () => {
          /* falha silenciosa — select ficará vazio */
        },
      });
  }

  private _carregarDemonstrativo(id: string): void {
    this._service.obterPorId(id).subscribe({
      next: (detalhe) => {
        this.operadoraId.set(detalhe.header.operadoraId);
        this.competencia.set(detalhe.header.competencia);
        this.dataRecebimento.set(detalhe.header.dataRecebimento);
        this.observacao.set(detalhe.header.observacao ?? '');
        this.itens.set(
          detalhe.itens.map((item) => ({
            id: item.id,
            senha: item.senha,
            codigoTuss: item.codigoTuss,
            descricao: item.descricao,
            valorApresentado: item.valorApresentado,
            valorPago: item.valorPago,
            motivoGlosa: item.motivoGlosa,
            conciliado: item.conciliado,
          })),
        );
      },
      error: () => {
        this.erroValidacao.set('Erro ao carregar o demonstrativo.');
      },
    });
  }
}
