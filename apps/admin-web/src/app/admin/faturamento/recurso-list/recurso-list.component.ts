import { Component, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { RecursoService } from '../recurso.service';
import type { RecursoDto } from '../recurso.types';

@Component({
  selector: 'app-recurso-list',
  template: `
    <div class="recurso-list">
      <div class="recurso-list__header">
        <h2 class="recurso-list__title">Recursos</h2>
        <button class="recurso-list__btn-novo" type="button" (click)="novoRecurso()">
          Novo Recurso
        </button>
      </div>

      <div class="recurso-list__filters">
        <input
          class="recurso-list__input--operadora"
          type="text"
          placeholder="ID Operadora"
          [value]="filtroOperadoraId()"
          (input)="onFiltroOperadoraChange($any($event.target).value)"
        />
        <input
          class="recurso-list__input--prestador"
          type="text"
          placeholder="ID Prestador"
          [value]="filtroPrestadorId()"
          (input)="onFiltroPrestadorChange($any($event.target).value)"
        />
      </div>

      <table class="recurso-list__table">
        <thead>
          <tr class="recurso-list__head-row">
            <th class="recurso-list__th">Número</th>
            <th class="recurso-list__th">Operadora</th>
            <th class="recurso-list__th">Prestador</th>
            <th class="recurso-list__th">Data Emissão</th>
            <th class="recurso-list__th">Guias</th>
            <th class="recurso-list__th">Ações</th>
          </tr>
        </thead>
        <tbody>
          @for (r of recursos(); track r.id) {
            <tr class="recurso-list__row">
              <td class="recurso-list__cell">{{ r.numero }}</td>
              <td class="recurso-list__cell">{{ r.operadoraNome }}</td>
              <td class="recurso-list__cell">{{ r.prestadorNome }}</td>
              <td class="recurso-list__cell">{{ r.dataEmissao | date: 'dd/MM/yyyy' }}</td>
              <td class="recurso-list__cell">
                <span class="recurso-list__badge">{{ r.totalGuias }}</span>
              </td>
              <td class="recurso-list__cell">
                <button
                  class="recurso-list__btn-guias"
                  type="button"
                  (click)="gerenciarGuias(r.id)"
                >
                  Gerenciar guias
                </button>
                <button class="recurso-list__btn-pdf" type="button" (click)="baixarPdf(r.id)">
                  PDF
                </button>
                <button class="recurso-list__btn-editar" type="button" (click)="editar(r.id)">
                  Editar
                </button>
                <button class="recurso-list__btn-excluir" type="button" (click)="excluir(r)">
                  Excluir
                </button>
              </td>
            </tr>
          } @empty {
            <tr>
              <td class="recurso-list__empty" colspan="6">Nenhum recurso encontrado.</td>
            </tr>
          }
        </tbody>
      </table>

      @if (erroExclusao()) {
        <p class="recurso-list__erro">{{ erroExclusao() }}</p>
      }
    </div>
  `,
  styleUrl: './recurso-list.component.scss',
  imports: [DatePipe],
})
export class RecursoListComponent implements OnInit {
  private readonly _service = inject(RecursoService);
  private readonly _router = inject(Router);
  private _debounceTimer: ReturnType<typeof setTimeout> | null = null;

  readonly recursos = signal<RecursoDto[]>([]);
  readonly loading = signal(false);
  readonly total = signal(0);
  readonly pagina = signal(1);
  readonly itensPorPagina = signal(20);
  readonly filtroOperadoraId = signal('');
  readonly filtroPrestadorId = signal('');
  readonly erroExclusao = signal('');

  ngOnInit(): void {
    this._carregar();
  }

  novoRecurso(): void {
    void this._router.navigate(['/admin/recursos/novo']);
  }

  editar(id: string): void {
    void this._router.navigate(['/admin/recursos', id]);
  }

  gerenciarGuias(id: string): void {
    void this._router.navigate(['/admin/recursos', id, 'guias']);
  }

  baixarPdf(id: string): void {
    this._service.baixarPdf(id);
  }

  excluir(r: RecursoDto): void {
    if (!window.confirm(`Excluir recurso "${r.numero}"?`)) {
      return;
    }
    this.erroExclusao.set('');
    this._service.excluir(r.id).subscribe({
      next: () => {
        this._carregar();
      },
      error: () => {
        this.erroExclusao.set('Erro ao excluir. Tente novamente.');
      },
    });
  }

  onFiltroOperadoraChange(value: string): void {
    this.filtroOperadoraId.set(value);
    this._agendarCarregamento();
  }

  onFiltroPrestadorChange(value: string): void {
    this.filtroPrestadorId.set(value);
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
        operadoraId: this.filtroOperadoraId() || undefined,
        prestadorId: this.filtroPrestadorId() || undefined,
        pagina: this.pagina(),
        itensPorPagina: this.itensPorPagina(),
      })
      .subscribe({
        next: (result) => {
          this.recursos.set(result.itens);
          this.total.set(result.total);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }
}
