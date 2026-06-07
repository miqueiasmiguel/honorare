import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, forkJoin, Subject } from 'rxjs';
import { Router } from '@angular/router';
import { CatalogService } from '../../catalog/catalog.service';
import { GuiaService } from '../guia.service';
import type { OperadoraItem, PrestadorItem } from '../../catalog/catalog.types';
import type { GuiaItem, GuiaOrdenacao, SituacaoGuia } from '../guia.types';
import { ImportarCsvModalComponent } from '../guias/importar-csv-modal/importar-csv-modal.component';

@Component({
  selector: 'app-guia-list',
  imports: [ImportarCsvModalComponent],
  template: `
    <div class="guia-list">
      <div class="guia-list__header">
        <h2 class="guia-list__title">Guias</h2>
        <div class="guia-list__header-acoes">
          <button
            class="guia-list__btn-importar-csv"
            type="button"
            (click)="mostrarImportarCsvModal.set(true)"
          >
            Importar CSV
          </button>
          <button class="guia-list__btn-nova" type="button" (click)="novaGuia()">Nova Guia</button>
        </div>
      </div>

      <app-importar-csv-modal
        [open]="mostrarImportarCsvModal()"
        (concluido)="onImportCsvConcluido()"
        (cancelado)="mostrarImportarCsvModal.set(false)"
      />

      <div class="guia-list__filters">
        <div class="guia-list__filter-row">
          <select
            class="guia-list__select"
            (change)="onFiltroPrestadorChange($any($event.target).value)"
          >
            <option value="" [selected]="!filtroPrestadorId()">Todos os prestadores</option>
            @for (p of prestadores(); track p.id) {
              <option [value]="p.id" [selected]="p.id === filtroPrestadorId()">{{ p.nome }}</option>
            }
          </select>

          <select
            class="guia-list__select"
            (change)="onFiltroOperadoraChange($any($event.target).value)"
          >
            <option value="" [selected]="!filtroOperadoraId()">Todas as operadoras</option>
            @for (o of operadoras(); track o.id) {
              <option [value]="o.id" [selected]="o.id === filtroOperadoraId()">{{ o.nome }}</option>
            }
          </select>

          <select
            class="guia-list__select"
            (change)="onFiltroSituacaoChange($any($event.target).value)"
          >
            <option value="" [selected]="!filtroSituacao()">Todas as situações</option>
            <option value="Apresentada" [selected]="filtroSituacao() === 'Apresentada'">
              Apresentada
            </option>
            <option value="Liquidada" [selected]="filtroSituacao() === 'Liquidada'">
              Liquidada
            </option>
            <option value="EmRecurso" [selected]="filtroSituacao() === 'EmRecurso'">
              Em Recurso
            </option>
          </select>
        </div>

        <div class="guia-list__filter-row">
          <input
            class="guia-list__input"
            type="text"
            placeholder="Guia"
            [value]="filtroNumeroGuia()"
            (input)="onFiltroNumeroGuiaChange($any($event.target).value)"
          />

          <input
            class="guia-list__input"
            type="text"
            placeholder="Nome do beneficiário"
            [value]="filtroBeneficiario()"
            (input)="onFiltroBeneficiarioChange($any($event.target).value)"
          />

          <input
            class="guia-list__input guia-list__input--date"
            type="date"
            [value]="filtroDataInicio()"
            (change)="onFiltroDataInicioChange($any($event.target).value)"
          />

          <input
            class="guia-list__input guia-list__input--date"
            type="date"
            [value]="filtroDataFim()"
            (change)="onFiltroDataFimChange($any($event.target).value)"
          />
        </div>

        <div class="guia-list__filter-row guia-list__filter-row--checkboxes">
          <label class="guia-list__checkbox-label">
            <input
              type="checkbox"
              [checked]="filtroSemRecurso()"
              (change)="onFiltroSemRecursoChange($any($event.target).checked)"
            />
            Sem recurso
          </label>

          <label class="guia-list__checkbox-label">
            <input
              type="checkbox"
              [checked]="filtroSomenteComGlosa()"
              (change)="onFiltroSomenteComGlosaChange($any($event.target).checked)"
            />
            Somente com glosa
          </label>

          <button class="guia-list__btn-limpar" type="button" (click)="limparFiltros()">
            Limpar filtros
          </button>
        </div>
      </div>

      <table class="guia-list__table">
        <thead>
          <tr class="guia-list__head-row">
            <th class="guia-list__th guia-list__th--sortable">
              <button class="guia-list__sort" type="button" (click)="ordenar('dataAtendimento')">
                Data
                <span class="guia-list__sort-icon">{{ iconeOrdenacao('dataAtendimento') }}</span>
              </button>
            </th>
            <th class="guia-list__th guia-list__th--sortable">
              <button class="guia-list__sort" type="button" (click)="ordenar('prestadorNome')">
                Prestador
                <span class="guia-list__sort-icon">{{ iconeOrdenacao('prestadorNome') }}</span>
              </button>
            </th>
            <th class="guia-list__th guia-list__th--sortable">
              <button class="guia-list__sort" type="button" (click)="ordenar('operadoraNome')">
                Operadora
                <span class="guia-list__sort-icon">{{ iconeOrdenacao('operadoraNome') }}</span>
              </button>
            </th>
            <th class="guia-list__th guia-list__th--sortable">
              <button class="guia-list__sort" type="button" (click)="ordenar('beneficiarioNome')">
                Beneficiário
                <span class="guia-list__sort-icon">{{ iconeOrdenacao('beneficiarioNome') }}</span>
              </button>
            </th>
            <th class="guia-list__th">Carteira</th>
            <th class="guia-list__th guia-list__th--sortable">
              <button class="guia-list__sort" type="button" (click)="ordenar('numeroGuia')">
                Guia
                <span class="guia-list__sort-icon">{{ iconeOrdenacao('numeroGuia') }}</span>
              </button>
            </th>
            <th class="guia-list__th guia-list__th--sortable">
              <button class="guia-list__sort" type="button" (click)="ordenar('situacao')">
                Situação
                <span class="guia-list__sort-icon">{{ iconeOrdenacao('situacao') }}</span>
              </button>
            </th>
            <th class="guia-list__th">Nº Itens</th>
            <th class="guia-list__th">Ações</th>
          </tr>
        </thead>
        <tbody>
          @if (loading()) {
            <tr>
              <td class="guia-list__empty" colspan="9">Carregando...</td>
            </tr>
          } @else {
            @for (g of guias(); track g.id) {
              <tr [class]="rowClass(g.situacao)">
                <td class="guia-list__cell">{{ formatarData(g.dataAtendimento) }}</td>
                <td class="guia-list__cell">{{ g.prestadorNome }}</td>
                <td class="guia-list__cell">{{ g.operadoraNome }}</td>
                <td class="guia-list__cell">{{ g.beneficiarioNome }}</td>
                <td class="guia-list__cell">{{ g.beneficiarioCarteira }}</td>
                <td class="guia-list__cell">{{ g.numeroGuia }}</td>
                <td class="guia-list__cell">{{ g.situacao }}</td>
                <td class="guia-list__cell guia-list__cell--mono">{{ g.totalItens }}</td>
                <td class="guia-list__cell">
                  <button class="guia-list__btn-editar" type="button" (click)="editar(g.id)">
                    Editar
                  </button>
                  <button class="guia-list__btn-excluir" type="button" (click)="excluir(g)">
                    Excluir
                  </button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td class="guia-list__empty" colspan="9">Nenhuma guia encontrada.</td>
              </tr>
            }
          }
        </tbody>
      </table>

      @if (total() > itensPorPagina()) {
        <div class="guia-list__pagination">
          <button
            class="guia-list__pagination-btn"
            type="button"
            [disabled]="pagina() <= 1"
            (click)="paginaAnterior()"
          >
            ← Anterior
          </button>
          <span class="guia-list__pagination-info">
            Página {{ pagina() }} de {{ totalPaginas() }} ({{ total() }} guias)
          </span>
          <button
            class="guia-list__pagination-btn"
            type="button"
            [disabled]="pagina() >= totalPaginas()"
            (click)="proximaPagina()"
          >
            Próxima →
          </button>
        </div>
      }

      @if (erroExclusao()) {
        <p class="guia-list__erro">{{ erroExclusao() }}</p>
      }
    </div>
  `,
  styleUrl: './guia-list.component.scss',
})
export class GuiaListComponent implements OnInit {
  private readonly _guiaService = inject(GuiaService);
  private readonly _catalogService = inject(CatalogService);
  private readonly _router = inject(Router);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _textoFiltroChange$ = new Subject<void>();

  readonly guias = signal<GuiaItem[]>([]);
  readonly loading = signal(false);
  readonly total = signal(0);
  readonly pagina = signal(1);
  readonly itensPorPagina = signal(20);
  readonly prestadores = signal<PrestadorItem[]>([]);
  readonly operadoras = signal<OperadoraItem[]>([]);

  readonly filtroSituacao = signal<SituacaoGuia | ''>('');
  readonly filtroPrestadorId = signal('');
  readonly filtroOperadoraId = signal('');
  readonly filtroDataInicio = signal('');
  readonly filtroDataFim = signal('');
  readonly filtroNumeroGuia = signal('');
  readonly filtroBeneficiario = signal('');
  readonly filtroSemRecurso = signal(false);
  readonly filtroSomenteComGlosa = signal(false);
  readonly ordenarPor = signal<GuiaOrdenacao>('dataAtendimento');
  readonly descendente = signal(true);
  readonly erroExclusao = signal('');
  readonly mostrarImportarCsvModal = signal(false);

  readonly totalPaginas = computed(() =>
    Math.max(1, Math.ceil(this.total() / this.itensPorPagina())),
  );

  ngOnInit(): void {
    this._textoFiltroChange$
      .pipe(debounceTime(300), takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this.pagina.set(1);
        this._carregar();
      });

    forkJoin({
      prestadores: this._catalogService.listarPrestadores({
        ativo: true,
        pagina: 1,
        itensPorPagina: 500,
      }),
      operadoras: this._catalogService.listarOperadoras({
        ativa: true,
        pagina: 1,
        itensPorPagina: 500,
      }),
    }).subscribe({
      next: ({ prestadores, operadoras }) => {
        this.prestadores.set(prestadores.itens);
        this.operadoras.set(operadoras.itens);
      },
      error: () => {
        // dropdowns ficam vazios — não bloqueia a listagem principal
      },
    });

    this._carregar();
  }

  formatarData(value: string): string {
    if (!value) {
      return '';
    }
    const [y, m, d] = value.split('-');
    return `${d}/${m}/${y}`;
  }

  rowClass(situacao: SituacaoGuia): string {
    const map: Record<SituacaoGuia, string> = {
      Apresentada: 'guia-list__row guia-list__row--apresentada',
      Liquidada: 'guia-list__row guia-list__row--liquidada',
      EmRecurso: 'guia-list__row guia-list__row--em-recurso',
    };
    return map[situacao];
  }

  limparFiltros(): void {
    this.filtroSituacao.set('');
    this.filtroPrestadorId.set('');
    this.filtroOperadoraId.set('');
    this.filtroDataInicio.set('');
    this.filtroDataFim.set('');
    this.filtroNumeroGuia.set('');
    this.filtroBeneficiario.set('');
    this.filtroSemRecurso.set(false);
    this.filtroSomenteComGlosa.set(false);
    this.ordenarPor.set('dataAtendimento');
    this.descendente.set(true);
    this.pagina.set(1);
    this._carregar();
  }

  novaGuia(): void {
    void this._router.navigate(['/admin/guias/nova']);
  }

  onImportCsvConcluido(): void {
    this.mostrarImportarCsvModal.set(false);
    this._carregar();
  }

  editar(id: string): void {
    void this._router.navigate(['/admin/guias', id]);
  }

  excluir(g: GuiaItem): void {
    if (!window.confirm(`Excluir guia de "${g.prestadorNome}"?`)) {
      return;
    }
    this.erroExclusao.set('');
    this._guiaService.excluir(g.id).subscribe({
      next: () => {
        this._carregar();
      },
      error: () => {
        this.erroExclusao.set('Erro ao excluir. Tente novamente.');
      },
    });
  }

  onFiltroSituacaoChange(value: string): void {
    this.filtroSituacao.set(value as SituacaoGuia | '');
    this.pagina.set(1);
    this._carregar();
  }

  onFiltroPrestadorChange(value: string): void {
    this.filtroPrestadorId.set(value);
    this.pagina.set(1);
    this._carregar();
  }

  onFiltroOperadoraChange(value: string): void {
    this.filtroOperadoraId.set(value);
    this.pagina.set(1);
    this._carregar();
  }

  onFiltroDataInicioChange(value: string): void {
    this.filtroDataInicio.set(value);
    this.pagina.set(1);
    this._carregar();
  }

  onFiltroDataFimChange(value: string): void {
    this.filtroDataFim.set(value);
    this.pagina.set(1);
    this._carregar();
  }

  onFiltroNumeroGuiaChange(value: string): void {
    this.filtroNumeroGuia.set(value);
    this._textoFiltroChange$.next();
  }

  onFiltroBeneficiarioChange(value: string): void {
    this.filtroBeneficiario.set(value);
    this._textoFiltroChange$.next();
  }

  onFiltroSemRecursoChange(checked: boolean): void {
    this.filtroSemRecurso.set(checked);
    this.pagina.set(1);
    this._carregar();
  }

  onFiltroSomenteComGlosaChange(checked: boolean): void {
    this.filtroSomenteComGlosa.set(checked);
    this.pagina.set(1);
    this._carregar();
  }

  ordenar(coluna: GuiaOrdenacao): void {
    if (this.ordenarPor() === coluna) {
      this.descendente.update((d) => !d);
    } else {
      this.ordenarPor.set(coluna);
      // data começa decrescente (mais recentes primeiro); colunas de texto, crescente
      this.descendente.set(coluna === 'dataAtendimento');
    }
    this.pagina.set(1);
    this._carregar();
  }

  iconeOrdenacao(coluna: GuiaOrdenacao): string {
    if (this.ordenarPor() !== coluna) {
      return '';
    }
    return this.descendente() ? '↓' : '↑';
  }

  paginaAnterior(): void {
    if (this.pagina() > 1) {
      this.pagina.update((p) => p - 1);
      this._carregar();
    }
  }

  proximaPagina(): void {
    if (this.pagina() < this.totalPaginas()) {
      this.pagina.update((p) => p + 1);
      this._carregar();
    }
  }

  private _carregar(): void {
    this.loading.set(true);
    this._guiaService
      .listar({
        situacao: this.filtroSituacao() || undefined,
        prestadorId: this.filtroPrestadorId() || undefined,
        operadoraId: this.filtroOperadoraId() || undefined,
        dataInicio: this.filtroDataInicio() || undefined,
        dataFim: this.filtroDataFim() || undefined,
        numeroGuia: this.filtroNumeroGuia() || undefined,
        beneficiario: this.filtroBeneficiario() || undefined,
        semRecurso: this.filtroSemRecurso() || undefined,
        somenteComGlosa: this.filtroSomenteComGlosa() || undefined,
        ordenarPor: this.ordenarPor(),
        descendente: this.descendente(),
        pagina: this.pagina(),
        itensPorPagina: this.itensPorPagina(),
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
