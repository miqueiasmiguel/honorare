import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import type { OperadoraItem, PrestadorItem } from '../../catalog/catalog.types';
import { CatalogService } from '../../catalog/catalog.service';
import { RecursoService } from '../recurso.service';

@Component({
  selector: 'app-recurso-form',
  template: `
    <form class="recurso-form" (submit)="onSubmit($event)">
      <div class="recurso-form__field">
        <label class="recurso-form__label" for="operadora-id">Operadora</label>
        <select
          id="operadora-id"
          class="recurso-form__select--operadora"
          [value]="operadoraId()"
          (change)="operadoraId.set($any($event.target).value)"
        >
          <option value="">Selecione uma operadora</option>
          @for (o of operadoras(); track o.id) {
            <option [value]="o.id">{{ o.nome }}</option>
          }
        </select>
      </div>

      <div class="recurso-form__field">
        <label class="recurso-form__label" for="prestador-id">Prestador</label>
        <select
          id="prestador-id"
          class="recurso-form__select--prestador"
          [value]="prestadorId()"
          (change)="prestadorId.set($any($event.target).value)"
        >
          <option value="">Selecione um prestador</option>
          @for (p of prestadores(); track p.id) {
            <option [value]="p.id">{{ p.nome }}</option>
          }
        </select>
      </div>

      <div class="recurso-form__field">
        <label class="recurso-form__label" for="data-emissao">Data de Emissão</label>
        <input
          type="date"
          id="data-emissao"
          class="recurso-form__input--data-emissao"
          [value]="dataEmissao()"
          (input)="dataEmissao.set($any($event.target).value)"
        />
      </div>

      <div class="recurso-form__field">
        <p class="recurso-form__label">Número</p>
        <span class="recurso-form__numero">{{ dataEmissao() | date: 'yyyyMM' }}</span>
      </div>

      <div class="recurso-form__field">
        <label class="recurso-form__label" for="observacao">Observação</label>
        <textarea
          id="observacao"
          class="recurso-form__textarea--observacao"
          [value]="observacao()"
          (input)="observacao.set($any($event.target).value)"
        ></textarea>
      </div>

      @if (erroValidacao()) {
        <span class="recurso-form__erro-validacao">{{ erroValidacao() }}</span>
      }

      <div class="recurso-form__actions">
        <button type="button" class="recurso-form__btn-cancelar" (click)="cancelar()">
          Cancelar
        </button>
        <button type="submit" class="recurso-form__btn-salvar">
          {{ modoEditar() ? 'Salvar' : 'Criar Recurso' }}
        </button>
      </div>
    </form>
  `,
  styleUrl: './recurso-form.component.scss',
  imports: [DatePipe],
})
export class RecursoFormComponent implements OnInit {
  private readonly _recursoService = inject(RecursoService);
  private readonly _catalogService = inject(CatalogService);
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);

  readonly modoEditar = signal(false);
  readonly recursoId = signal<string | null>(null);
  readonly erroValidacao = signal('');

  readonly operadoras = signal<OperadoraItem[]>([]);
  readonly prestadores = signal<PrestadorItem[]>([]);

  readonly operadoraId = signal('');
  readonly prestadorId = signal('');
  readonly dataEmissao = signal('');
  readonly observacao = signal('');

  ngOnInit(): void {
    this._carregarOperadoras();
    this._carregarPrestadores();

    const id = this._route.snapshot.paramMap.get('id');
    if (id) {
      this.modoEditar.set(true);
      this.recursoId.set(id);
      this._carregarRecurso(id);
    }
  }

  cancelar(): void {
    void this._router.navigate(['/admin/recursos']);
  }

  onSubmit(event: Event): void {
    event.preventDefault();
    this.erroValidacao.set('');

    if (!this.operadoraId()) {
      this.erroValidacao.set('Selecione uma operadora.');
      return;
    }
    if (!this.prestadorId()) {
      this.erroValidacao.set('Selecione um prestador.');
      return;
    }
    if (!this.dataEmissao()) {
      this.erroValidacao.set('Informe a data de emissão.');
      return;
    }

    const payload = {
      operadoraId: this.operadoraId(),
      prestadorId: this.prestadorId(),
      dataEmissao: this.dataEmissao(),
      observacao: this.observacao() || null,
    };

    const editId = this.recursoId();
    if (this.modoEditar() && editId !== null) {
      this._recursoService.atualizar(editId, payload).subscribe({
        next: () => {
          void this._router.navigate(['/admin/recursos']);
        },
        error: () => {
          this.erroValidacao.set('Erro ao salvar. Tente novamente.');
        },
      });
    } else {
      this._recursoService.criar(payload).subscribe({
        next: (recurso) => {
          void this._router.navigate(['/admin/recursos', recurso.id, 'guias']);
        },
        error: () => {
          this.erroValidacao.set('Erro ao criar. Tente novamente.');
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
      });
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

  private _carregarRecurso(id: string): void {
    this._recursoService.obterPorId(id).subscribe({
      next: (detalhe) => {
        const h = detalhe.header;
        this.operadoraId.set(h.operadoraId);
        this.prestadorId.set(h.prestadorId);
        this.dataEmissao.set(h.dataEmissao);
        this.observacao.set(h.observacao ?? '');
      },
      error: () => {
        this.erroValidacao.set('Erro ao carregar recurso.');
      },
    });
  }
}
