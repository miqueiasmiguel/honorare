import { Component, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { GuiaService } from '../guia.service';
import type { GuiaItem, SituacaoGuia } from '../guia.types';

@Component({
  selector: 'app-guia-list',
  template: `
    <div class="guia-list">
      <div class="guia-list__header">
        <h2 class="guia-list__title">Controle de Pagamentos</h2>
        <button class="guia-list__btn-nova" type="button" (click)="novaGuia()">Nova Guia</button>
      </div>

      <div class="guia-list__filters">
        <select
          class="guia-list__select--situacao"
          [value]="filtroSituacao()"
          (change)="onFiltroSituacaoChange($any($event.target).value)"
        >
          <option value="">Todas as situações</option>
          <option value="Apresentada">Apresentada</option>
          <option value="Liquidada">Liquidada</option>
          <option value="EmRecurso">Em Recurso</option>
        </select>

        <input
          class="guia-list__input--data-inicio"
          type="date"
          [value]="filtroDataInicio()"
          (change)="onFiltroDataInicioChange($any($event.target).value)"
        />

        <input
          class="guia-list__input--data-fim"
          type="date"
          [value]="filtroDataFim()"
          (change)="onFiltroDataFimChange($any($event.target).value)"
        />
      </div>

      <table class="guia-list__table">
        <thead>
          <tr class="guia-list__head-row">
            <th class="guia-list__th">Data</th>
            <th class="guia-list__th">Prestador</th>
            <th class="guia-list__th">Operadora</th>
            <th class="guia-list__th">Beneficiário</th>
            <th class="guia-list__th">Carteira</th>
            <th class="guia-list__th">Senha</th>
            <th class="guia-list__th">Situação</th>
            <th class="guia-list__th">Nº Itens</th>
            <th class="guia-list__th">Ações</th>
          </tr>
        </thead>
        <tbody>
          @for (g of guias(); track g.id) {
            <tr [class]="rowClass(g.situacao)">
              <td class="guia-list__cell">{{ g.dataAtendimento | date: 'dd/MM/yyyy' }}</td>
              <td class="guia-list__cell">{{ g.prestadorNome }}</td>
              <td class="guia-list__cell">{{ g.operadoraNome }}</td>
              <td class="guia-list__cell">{{ g.beneficiarioNome }}</td>
              <td class="guia-list__cell">{{ g.beneficiarioCarteira }}</td>
              <td class="guia-list__cell">{{ g.senha }}</td>
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
        </tbody>
      </table>

      @if (erroExclusao()) {
        <p class="guia-list__erro">{{ erroExclusao() }}</p>
      }
    </div>
  `,
  styleUrl: './guia-list.component.scss',
  imports: [DatePipe],
})
export class GuiaListComponent implements OnInit {
  private readonly _guiaService = inject(GuiaService);
  private readonly _router = inject(Router);

  readonly guias = signal<GuiaItem[]>([]);
  readonly loading = signal(false);
  readonly total = signal(0);
  readonly pagina = signal(1);
  readonly itensPorPagina = signal(20);
  readonly filtroSituacao = signal<SituacaoGuia | ''>('');
  readonly filtroDataInicio = signal('');
  readonly filtroDataFim = signal('');
  readonly erroExclusao = signal('');

  ngOnInit(): void {
    this._carregar();
  }

  rowClass(situacao: SituacaoGuia): string {
    const map: Record<SituacaoGuia, string> = {
      Apresentada: 'guia-list__row guia-list__row--apresentada',
      Liquidada: 'guia-list__row guia-list__row--liquidada',
      EmRecurso: 'guia-list__row guia-list__row--em-recurso',
    };
    return map[situacao];
  }

  novaGuia(): void {
    void this._router.navigate(['/admin/guias/nova']);
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

  private _carregar(): void {
    this.loading.set(true);
    this._guiaService
      .listar({
        situacao: this.filtroSituacao() || undefined,
        dataInicio: this.filtroDataInicio() || undefined,
        dataFim: this.filtroDataFim() || undefined,
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
