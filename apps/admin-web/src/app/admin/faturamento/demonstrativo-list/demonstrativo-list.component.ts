import { Component, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { DemonstrativoService } from '../demonstrativo.service';
import type { DemonstrativoDto } from '../demonstrativo.types';
import { ImportarDemonstrativoModalComponent } from '../demonstrativos/importar-demonstrativo-modal/importar-demonstrativo-modal.component';

@Component({
  selector: 'app-demonstrativo-list',
  template: `
    <div class="demonstrativo-list">
      <div class="demonstrativo-list__header">
        <h2 class="demonstrativo-list__title">Demonstrativos</h2>
        <div class="demonstrativo-list__header-acoes">
          <button
            class="demonstrativo-list__btn-importar"
            type="button"
            (click)="mostrarModalImportar.set(true)"
          >
            Importar demonstrativo CSV
          </button>
          <button class="demonstrativo-list__btn-novo" type="button" (click)="novoDemonstrativo()">
            Novo Demonstrativo
          </button>
        </div>
      </div>

      <div class="demonstrativo-list__filters">
        <input
          class="demonstrativo-list__input--competencia"
          type="month"
          placeholder="Competência"
          [value]="filtroCompetencia()"
          (input)="onFiltroCompetenciaChange($any($event.target).value)"
        />
        <input
          class="demonstrativo-list__input--operadora"
          type="text"
          placeholder="ID Operadora"
          [value]="filtroOperadoraId()"
          (input)="onFiltroOperadoraChange($any($event.target).value)"
        />
      </div>

      <table class="demonstrativo-list__table">
        <thead>
          <tr class="demonstrativo-list__head-row">
            <th class="demonstrativo-list__th">Operadora</th>
            <th class="demonstrativo-list__th">Competência</th>
            <th class="demonstrativo-list__th">Data Recebimento</th>
            <th class="demonstrativo-list__th">Conciliados</th>
            <th class="demonstrativo-list__th">Ações</th>
          </tr>
        </thead>
        <tbody>
          @for (d of demonstrativos(); track d.id) {
            <tr class="demonstrativo-list__row">
              <td class="demonstrativo-list__cell">{{ d.operadoraNome }}</td>
              <td class="demonstrativo-list__cell">{{ d.competencia }}</td>
              <td class="demonstrativo-list__cell">{{ d.dataRecebimento | date: 'dd/MM/yyyy' }}</td>
              <td class="demonstrativo-list__cell">
                <span class="demonstrativo-list__badge">
                  {{ d.itensConciliados }}/{{ d.totalItens }}
                </span>
              </td>
              <td class="demonstrativo-list__cell">
                <button
                  class="demonstrativo-list__btn-conciliar"
                  type="button"
                  (click)="conciliar(d.id)"
                >
                  Conciliar
                </button>
                <button class="demonstrativo-list__btn-editar" type="button" (click)="editar(d.id)">
                  Editar
                </button>
                <button
                  class="demonstrativo-list__btn-excluir"
                  type="button"
                  [disabled]="d.itensConciliados > 0"
                  (click)="excluir(d)"
                >
                  Excluir
                </button>
              </td>
            </tr>
          } @empty {
            <tr>
              <td class="demonstrativo-list__empty" colspan="5">
                Nenhum demonstrativo encontrado.
              </td>
            </tr>
          }
        </tbody>
      </table>

      @if (erroExclusao()) {
        <p class="demonstrativo-list__erro">{{ erroExclusao() }}</p>
      }
    </div>

    <app-importar-demonstrativo-modal
      [open]="mostrarModalImportar()"
      (importacaoConcluida)="onImportacaoConcluida()"
      (cancelado)="mostrarModalImportar.set(false)"
    />
  `,
  styleUrl: './demonstrativo-list.component.scss',
  imports: [DatePipe, ImportarDemonstrativoModalComponent],
})
export class DemonstrativoListComponent implements OnInit {
  private readonly _service = inject(DemonstrativoService);
  private readonly _router = inject(Router);
  private _debounceTimer: ReturnType<typeof setTimeout> | null = null;

  readonly demonstrativos = signal<DemonstrativoDto[]>([]);
  readonly loading = signal(false);
  readonly total = signal(0);
  readonly pagina = signal(1);
  readonly itensPorPagina = signal(20);
  readonly filtroCompetencia = signal('');
  readonly filtroOperadoraId = signal('');
  readonly erroExclusao = signal('');
  readonly mostrarModalImportar = signal(false);

  ngOnInit(): void {
    this._carregar();
  }

  novoDemonstrativo(): void {
    void this._router.navigate(['/admin/demonstrativos/novo']);
  }

  editar(id: string): void {
    void this._router.navigate(['/admin/demonstrativos', id]);
  }

  conciliar(id: string): void {
    void this._router.navigate(['/admin/demonstrativos', id, 'conciliar']);
  }

  excluir(d: DemonstrativoDto): void {
    if (d.itensConciliados > 0) {
      return;
    }
    if (!window.confirm(`Excluir demonstrativo de "${d.operadoraNome}" (${d.competencia})?`)) {
      return;
    }
    this.erroExclusao.set('');
    this._service.excluir(d.id).subscribe({
      next: () => {
        this._carregar();
      },
      error: () => {
        this.erroExclusao.set('Erro ao excluir. Tente novamente.');
      },
    });
  }

  onImportacaoConcluida(): void {
    this.mostrarModalImportar.set(false);
    this._carregar();
  }

  onFiltroCompetenciaChange(value: string): void {
    this.filtroCompetencia.set(value);
    this._agendarCarregamento();
  }

  onFiltroOperadoraChange(value: string): void {
    this.filtroOperadoraId.set(value);
    this._agendarCarregamento();
  }

  private _agendarCarregamento(): void {
    if (this._debounceTimer !== null) {
      clearTimeout(this._debounceTimer);
    }
    this._debounceTimer = setTimeout(() => {
      this._carregar();
    }, 400);
  }

  private _carregar(): void {
    this.loading.set(true);
    this._service
      .listar({
        competencia: this.filtroCompetencia() || undefined,
        operadoraId: this.filtroOperadoraId() || undefined,
        pagina: this.pagina(),
        itensPorPagina: this.itensPorPagina(),
      })
      .subscribe({
        next: (result) => {
          this.demonstrativos.set(result.itens);
          this.total.set(result.total);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }
}
