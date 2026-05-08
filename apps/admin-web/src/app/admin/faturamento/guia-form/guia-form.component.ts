import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { BeneficiarioAutocompleteComponent } from '../../catalog/beneficiarios/beneficiario-autocomplete/beneficiario-autocomplete.component';
import type { BeneficiarioItem, OperadoraItem, PrestadorItem } from '../../catalog/catalog.types';
import { CatalogService } from '../../catalog/catalog.service';
import { GuiaService } from '../guia.service';
import type { CriarItemGuiaPayload } from '../guia.types';
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
          [value]="prestadorId()"
          (change)="prestadorId.set($any($event.target).value)"
        >
          <option value="">Selecione um prestador</option>
          @for (p of prestadores(); track p.id) {
            <option [value]="p.id">{{ p.nome }}</option>
          }
        </select>
      </div>

      <div class="guia-form__field">
        <label class="guia-form__label" for="operadora-id">Operadora</label>
        <select
          id="operadora-id"
          class="guia-form__select--operadora"
          [value]="operadoraId()"
          (change)="operadoraId.set($any($event.target).value)"
        >
          <option value="">Selecione uma operadora</option>
          @for (o of operadoras(); track o.id) {
            <option [value]="o.id">{{ o.nome }}</option>
          }
        </select>
      </div>

      <div class="guia-form__field">
        <app-beneficiario-autocomplete
          label="Beneficiário"
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
            <span class="guia-form__item-procedimento">{{ item.procedimentoId }}</span>
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
        <app-item-guia-form [ehPacote]="ehPacote()" (itemChange)="onItemChange($event)" />
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
  imports: [BeneficiarioAutocompleteComponent, ItemGuiaFormComponent],
})
export class GuiaFormComponent implements OnInit {
  private readonly _guiaService = inject(GuiaService);
  private readonly _catalogService = inject(CatalogService);
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);

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

  readonly itens = signal<CriarItemGuiaPayload[]>([]);
  readonly adicionandoItem = signal(false);

  ngOnInit(): void {
    this._carregarPrestadores();
    this._carregarOperadoras();

    const id = this._route.snapshot.paramMap.get('id');
    if (id) {
      this.modoEditar.set(true);
      this.guiaId.set(id);
      this._carregarGuia(id);
    }
  }

  onBeneficiarioChange(b: BeneficiarioItem | null): void {
    this.beneficiarioId.set(b?.id ?? '');
  }

  onItemChange(item: CriarItemGuiaPayload | null): void {
    if (item !== null) {
      this.itens.update((prev) => [...prev, item]);
    }
    this.adicionandoItem.set(false);
  }

  removerItem(index: number): void {
    this.itens.update((prev) => prev.filter((_, i) => i !== index));
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
          error: () => {
            this.erroValidacao.set('Erro ao salvar a guia. Verifique os dados e tente novamente.');
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
          error: () => {
            this.erroValidacao.set('Erro ao criar a guia. Verifique os dados e tente novamente.');
          },
        });
    }
  }

  private _carregarPrestadores(): void {
    this._catalogService
      .listarPrestadores({ ativo: true, pagina: 1, itensPorPagina: 200 })
      .subscribe({
        next: (result) => {
          this.prestadores.set(result.itens);
        },
      });
  }

  private _carregarOperadoras(): void {
    this._catalogService
      .listarOperadoras({ ativa: true, pagina: 1, itensPorPagina: 200 })
      .subscribe({
        next: (result) => {
          this.operadoras.set(result.itens);
        },
      });
  }

  private _carregarGuia(id: string): void {
    this.carregando.set(true);
    this._guiaService.obterPorId(id).subscribe({
      next: (guia) => {
        this.prestadorId.set(guia.prestadorId);
        this.operadoraId.set(guia.operadoraId);
        this.beneficiarioId.set(guia.beneficiarioId ?? '');
        this.senha.set(guia.senha);
        this.dataAtendimento.set(guia.dataAtendimento);
        this.ehPacote.set(guia.ehPacote);
        this.observacao.set(guia.observacao);
        this.itens.set(
          guia.itens.map((i) => ({
            procedimentoId: i.procedimentoId,
            posicaoExecutor: i.posicaoExecutor,
            ordemProcedimento: i.ordemProcedimento,
            viaAcesso: i.viaAcesso,
            acomodacao: i.acomodacao,
            ehUrgencia: i.ehUrgencia,
            valorApurado: i.valorApurado,
          })),
        );
        this.carregando.set(false);
      },
      error: () => {
        this.carregando.set(false);
      },
    });
  }
}
