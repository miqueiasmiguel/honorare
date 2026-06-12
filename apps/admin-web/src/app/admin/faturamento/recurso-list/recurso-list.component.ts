import { Component, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { forkJoin } from 'rxjs';
import { RecursoService } from '../recurso.service';
import { CatalogService } from '../../catalog/catalog.service';
import type { RecursoDto } from '../recurso.types';
import type { OperadoraItem, PrestadorItem } from '../../catalog/catalog.types';

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
        <select
          class="recurso-list__select"
          (change)="onFiltroPrestadorChange($any($event.target).value)"
        >
          <option value="" [selected]="!filtroPrestadorId()">Todos os prestadores</option>
          @for (p of prestadores(); track p.id) {
            <option [value]="p.id" [selected]="p.id === filtroPrestadorId()">{{ p.nome }}</option>
          }
        </select>

        <select
          class="recurso-list__select"
          (change)="onFiltroOperadoraChange($any($event.target).value)"
        >
          <option value="" [selected]="!filtroOperadoraId()">Todas as operadoras</option>
          @for (o of operadoras(); track o.id) {
            <option [value]="o.id" [selected]="o.id === filtroOperadoraId()">{{ o.nome }}</option>
          }
        </select>
      </div>

      <table class="recurso-list__table">
        <thead>
          <tr class="recurso-list__head-row">
            <th class="recurso-list__th">Número</th>
            <th class="recurso-list__th">Tipo</th>
            <th class="recurso-list__th">Operadora</th>
            <th class="recurso-list__th">Prestador</th>
            <th class="recurso-list__th">Data Emissão</th>
            <th class="recurso-list__th">Guias</th>
            <th class="recurso-list__th recurso-list__th--acoes">Ações</th>
          </tr>
        </thead>
        <tbody>
          @for (r of recursos(); track r.id) {
            <tr class="recurso-list__row">
              <td class="recurso-list__cell">{{ r.numero }}</td>
              <td class="recurso-list__cell">
                <span
                  class="recurso-list__badge recurso-list__badge--tipo"
                  [class.recurso-list__badge--branca]="r.tipo === 'GlosaBranca'"
                  >{{ r.tipo === 'GlosaBranca' ? 'Branca' : 'Parcial' }}</span
                >
              </td>
              <td class="recurso-list__cell">{{ r.operadoraNome }}</td>
              <td class="recurso-list__cell">{{ r.prestadorNome }}</td>
              <td class="recurso-list__cell">{{ r.dataEmissao | date: 'dd/MM/yyyy' }}</td>
              <td class="recurso-list__cell">
                <span class="recurso-list__badge recurso-list__badge--guias">{{
                  r.totalGuias
                }}</span>
              </td>
              <td class="recurso-list__cell recurso-list__cell--acoes">
                <div class="recurso-list__menu">
                  <button class="recurso-list__menu-trigger" type="button" aria-label="Ações">
                    &#8942;
                  </button>
                  <div class="recurso-list__menu-dropdown">
                    <button
                      class="recurso-list__menu-item"
                      type="button"
                      (click)="gerenciarGuias(r.id)"
                    >
                      Gerenciar guias
                    </button>
                    <button class="recurso-list__menu-item" type="button" (click)="editar(r.id)">
                      Editar
                    </button>
                    <button class="recurso-list__menu-item" type="button" (click)="baixarPdf(r.id)">
                      Baixar PDF
                    </button>
                    <button
                      class="recurso-list__menu-item recurso-list__menu-item--excluir"
                      type="button"
                      (click)="excluir(r)"
                    >
                      Excluir
                    </button>
                  </div>
                </div>
              </td>
            </tr>
          } @empty {
            <tr>
              <td class="recurso-list__empty" colspan="7">Nenhum recurso encontrado.</td>
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
  private readonly _catalogService = inject(CatalogService);
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
  readonly prestadores = signal<PrestadorItem[]>([]);
  readonly operadoras = signal<OperadoraItem[]>([]);

  ngOnInit(): void {
    forkJoin({
      prestadores: this._catalogService.listarPrestadores({ pagina: 1, itensPorPagina: 200 }),
      operadoras: this._catalogService.listarOperadoras({ pagina: 1, itensPorPagina: 200 }),
    }).subscribe({
      next: ({ prestadores, operadoras }) => {
        this.prestadores.set(prestadores.itens);
        this.operadoras.set(operadoras.itens);
      },
      error: () => undefined,
    });
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
