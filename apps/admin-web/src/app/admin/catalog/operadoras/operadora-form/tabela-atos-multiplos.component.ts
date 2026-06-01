import { Component, inject, Input, OnInit, signal } from '@angular/core';
import { CatalogService } from '../../catalog.service';
import type { TabelaOrdemOperadoraItem } from '../../catalog.types';

interface LinhaOrdem {
  numero: number;
  label: string;
  mesmaVia: number;
  viaDiferente: number;
}

const PADRAO_LINHAS: LinhaOrdem[] = [
  { numero: 1, label: '1º Procedimento', mesmaVia: 100, viaDiferente: 100 },
  { numero: 2, label: '2º Procedimento', mesmaVia: 50, viaDiferente: 70 },
  { numero: 3, label: '3º Procedimento', mesmaVia: 40, viaDiferente: 50 },
  { numero: 4, label: '4º Procedimento', mesmaVia: 30, viaDiferente: 40 },
  { numero: 5, label: '5º Procedimento', mesmaVia: 20, viaDiferente: 30 },
  { numero: 6, label: '6º ou mais', mesmaVia: 10, viaDiferente: 10 },
];

@Component({
  selector: 'app-tabela-atos-multiplos',
  template: `
    <section class="tabela-atos">
      <h2 class="tabela-atos__title">Tabela de Atos Múltiplos</h2>

      @if (loading()) {
        <p class="tabela-atos__loading">Carregando…</p>
      } @else {
        <table class="tabela-atos__tabela">
          <thead>
            <tr>
              <th class="tabela-atos__th">Procedimento</th>
              <th class="tabela-atos__th">Mesma Via (%)</th>
              <th class="tabela-atos__th">Via Diferente (%)</th>
            </tr>
          </thead>
          <tbody>
            @for (linha of linhas(); track linha.numero) {
              <tr class="tabela-atos__linha">
                <td class="tabela-atos__td tabela-atos__td--label">{{ linha.label }}</td>
                <td class="tabela-atos__td">
                  <input
                    type="number"
                    class="tabela-atos__input"
                    min="1"
                    max="100"
                    step="1"
                    [value]="linha.mesmaVia"
                    (input)="onMesmaViaChange(linha, $any($event.target).value)"
                  />
                </td>
                <td class="tabela-atos__td">
                  <input
                    type="number"
                    class="tabela-atos__input"
                    min="1"
                    max="100"
                    step="1"
                    [value]="linha.viaDiferente"
                    (input)="onViaDiferenteChange(linha, $any($event.target).value)"
                  />
                </td>
              </tr>
            }
          </tbody>
        </table>

        @if (erro()) {
          <p class="tabela-atos__erro">{{ erro() }}</p>
        }

        <div class="tabela-atos__actions">
          <button
            type="button"
            class="tabela-atos__btn-salvar"
            [disabled]="salvando()"
            (click)="salvar()"
          >
            {{ salvando() ? 'Salvando…' : 'Salvar tabela' }}
          </button>
          <button
            type="button"
            class="tabela-atos__btn-restaurar"
            [disabled]="salvando()"
            (click)="restaurarPadroes()"
          >
            Restaurar padrões
          </button>
        </div>
      }
    </section>
  `,
  styleUrl: './tabela-atos-multiplos.component.scss',
})
export class TabelaAtosMultiplosComponent implements OnInit {
  @Input({ required: true }) operadoraId!: string;

  private readonly _catalogService = inject(CatalogService);

  readonly linhas = signal<LinhaOrdem[]>(PADRAO_LINHAS.map((l) => ({ ...l })));
  readonly loading = signal(false);
  readonly salvando = signal(false);
  readonly erro = signal('');

  ngOnInit(): void {
    this._carregar();
  }

  onMesmaViaChange(linha: LinhaOrdem, value: string): void {
    const parsed = parseInt(value, 10);
    if (!isNaN(parsed)) {
      this.linhas.update((ls) =>
        ls.map((l) => (l.numero === linha.numero ? { ...l, mesmaVia: parsed } : l)),
      );
    }
  }

  onViaDiferenteChange(linha: LinhaOrdem, value: string): void {
    const parsed = parseInt(value, 10);
    if (!isNaN(parsed)) {
      this.linhas.update((ls) =>
        ls.map((l) => (l.numero === linha.numero ? { ...l, viaDiferente: parsed } : l)),
      );
    }
  }

  salvar(): void {
    this.erro.set('');
    const payload: TabelaOrdemOperadoraItem[] = this.linhas().flatMap((l) => [
      { numeroProcedimento: l.numero, tipoVia: 'MesmaVia', percentual: l.mesmaVia / 100 },
      { numeroProcedimento: l.numero, tipoVia: 'ViaDiferente', percentual: l.viaDiferente / 100 },
    ]);

    this.salvando.set(true);
    this._catalogService.salvarTabelaOrdem(this.operadoraId, payload).subscribe({
      next: () => {
        this.salvando.set(false);
      },
      error: () => {
        this.erro.set('Erro ao salvar tabela. Verifique os valores e tente novamente.');
        this.salvando.set(false);
      },
    });
  }

  restaurarPadroes(): void {
    if (!window.confirm('Restaurar valores padrão? A tabela configurada será removida.')) {
      return;
    }
    this.salvando.set(true);
    this.erro.set('');
    this._catalogService.excluirTabelaOrdem(this.operadoraId).subscribe({
      next: () => {
        this.linhas.set(PADRAO_LINHAS.map((l) => ({ ...l })));
        this.salvando.set(false);
      },
      error: () => {
        this.erro.set('Erro ao restaurar padrões.');
        this.salvando.set(false);
      },
    });
  }

  private _carregar(): void {
    this.loading.set(true);
    this._catalogService.listarTabelaOrdem(this.operadoraId).subscribe({
      next: (items) => {
        if (items.length === 0) {
          this.linhas.set(PADRAO_LINHAS.map((l) => ({ ...l })));
        } else {
          this.linhas.set(
            PADRAO_LINHAS.map((padrao) => {
              const mv = items.find(
                (i) => i.numeroProcedimento === padrao.numero && i.tipoVia === 'MesmaVia',
              );
              const vd = items.find(
                (i) => i.numeroProcedimento === padrao.numero && i.tipoVia === 'ViaDiferente',
              );
              return {
                ...padrao,
                mesmaVia: mv ? Math.round(mv.percentual * 100) : padrao.mesmaVia,
                viaDiferente: vd ? Math.round(vd.percentual * 100) : padrao.viaDiferente,
              };
            }),
          );
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }
}
