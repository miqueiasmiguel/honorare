import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { debounceTime, Subject } from 'rxjs';
import { MedicoGuiaService } from '../medico-guia.service';
import type { MedicoGuiaSummaryItem, SituacaoGuia } from '../medico-guia.types';

@Component({
  selector: 'app-guia-list',
  imports: [],
  template: `
    <div class="guia-list">
      <div class="guia-list__filters">
        <input
          class="guia-list__input"
          type="date"
          placeholder="Data início"
          (input)="onDataInicioInput($event)"
        />
        <input
          class="guia-list__input"
          type="date"
          placeholder="Data fim"
          (input)="onDataFimInput($event)"
        />
        <input
          class="guia-list__input"
          type="text"
          placeholder="Operadora"
          (input)="onOperadoraInput($event)"
        />
      </div>

      @for (guia of guias(); track guia.id) {
        <div
          class="guia-list__row"
          role="button"
          tabindex="0"
          (click)="navegar(guia.id)"
          (keydown.enter)="navegar(guia.id)"
          (keydown.space)="navegar(guia.id)"
        >
          <span class="guia-list__cell guia-list__cell--data">{{ guia.dataAtendimento }}</span>
          <span class="guia-list__cell guia-list__cell--senha">{{ guia.senha ?? '—' }}</span>
          <span class="guia-list__cell guia-list__cell--beneficiario">{{
            guia.beneficiarioNome ?? '—'
          }}</span>
          <span class="guia-list__cell guia-list__cell--operadora">{{ guia.operadoraNome }}</span>
          <span [class]="badgeClass(guia.situacao)">{{ guia.situacao }}</span>
          @if (guia.temObservacao) {
            <span class="guia-list__obs-icon" title="Tem observação">⚠</span>
          }
        </div>
      }

      @if (guias().length === 0 && !loading()) {
        <p class="guia-list__empty">Nenhuma guia encontrada.</p>
      }

      <div class="guia-list__pagination">
        @if (pagina() > 1) {
          <button class="guia-list__btn" type="button" (click)="paginaAnterior()">Anterior</button>
        }
        @if (hasNextPage()) {
          <button class="guia-list__btn" type="button" (click)="proximaPagina()">Próximo</button>
        }
      </div>
    </div>
  `,
  styles: [
    `
      .guia-list {
        padding: 16px;
      }

      .guia-list__filters {
        display: flex;
        gap: 8px;
        margin-bottom: 16px;
        flex-wrap: wrap;
      }

      .guia-list__input {
        padding: 8px 12px;
        border: 1px solid var(--color-borda-media);
        border-radius: 6px;
        font-family: var(--font-sans);
        font-size: 15px;
        color: var(--color-tinta);
        background-color: var(--color-pergaminho-claro);
      }

      .guia-list__row {
        display: flex;
        align-items: center;
        gap: 12px;
        padding: 12px 16px;
        border-bottom: 1px solid var(--color-borda-discreta);
        cursor: pointer;
        transition: background-color 150ms ease-out;
      }

      .guia-list__row:hover {
        background-color: var(--color-pergaminho-claro);
      }

      .guia-list__cell {
        font-family: var(--font-sans);
        font-size: 15px;
        color: var(--color-tinta);
      }

      .guia-list__obs-icon {
        color: var(--color-ambar);
        font-size: 16px;
      }

      .guia-list__empty {
        padding: 32px 16px;
        color: var(--color-tinta-secundaria);
        font-family: var(--font-sans);
        font-size: 15px;
        text-align: center;
      }

      .guia-list__pagination {
        display: flex;
        gap: 8px;
        padding: 16px 0;
      }

      .guia-list__btn {
        padding: 8px 16px;
        border: 1px solid var(--color-borda-media);
        border-radius: 6px;
        background: none;
        cursor: pointer;
        font-family: var(--font-sans);
        font-size: 15px;
        color: var(--color-tinta);
      }

      .badge {
        display: inline-block;
        padding: 2px 8px;
        border-radius: 4px;
        font-family: var(--font-sans);
        font-size: 12px;
        font-weight: 500;
      }

      .badge--ambar {
        background-color: var(--color-ambar-claro);
        color: var(--color-ambar);
      }

      .badge--ferrugem {
        background-color: var(--color-ferrugem-claro);
        color: var(--color-ferrugem);
      }
    `,
  ],
})
export class GuiaListComponent implements OnInit {
  private readonly _service = inject(MedicoGuiaService);
  private readonly _router = inject(Router);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _operadora$ = new Subject<string>();

  readonly guias = signal<MedicoGuiaSummaryItem[]>([]);
  readonly total = signal(0);
  readonly pagina = signal(1);
  readonly itensPorPagina = signal(20);
  readonly loading = signal(false);
  readonly dataInicio = signal('');
  readonly dataFim = signal('');

  readonly hasNextPage = computed(
    () => this.pagina() < Math.ceil(this.total() / this.itensPorPagina()),
  );

  ngOnInit(): void {
    this._operadora$.pipe(debounceTime(400), takeUntilDestroyed(this._destroyRef)).subscribe(() => {
      this.pagina.set(1);
      this._carregar();
    });

    this._carregar();
  }

  onOperadoraChange(value: string): void {
    this._operadora$.next(value);
  }

  onOperadoraInput(event: Event): void {
    this.onOperadoraChange((event.target as HTMLInputElement).value);
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
    return 'badge';
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
