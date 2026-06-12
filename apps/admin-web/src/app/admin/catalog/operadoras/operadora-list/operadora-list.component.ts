import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { debounceTime, Subject } from 'rxjs';
import { CatalogService } from '../../catalog.service';
import type { OperadoraItem, TipoRuleSet } from '../../catalog.types';

@Component({
  selector: 'app-operadora-list',
  templateUrl: './operadora-list.component.html',
  styleUrl: './operadora-list.component.scss',
})
export class OperadoraListComponent implements OnInit {
  private readonly _catalogService = inject(CatalogService);
  private readonly _router = inject(Router);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _busca$ = new Subject<string>();

  readonly operadoras = signal<OperadoraItem[]>([]);
  readonly loading = signal(false);
  readonly total = signal(0);
  readonly pagina = signal(1);
  readonly itensPorPagina = signal(20);
  readonly filtroNome = signal('');
  readonly exibirInativas = signal(false);

  ngOnInit(): void {
    this._busca$.pipe(debounceTime(300), takeUntilDestroyed(this._destroyRef)).subscribe((nome) => {
      this.filtroNome.set(nome);
      this.pagina.set(1);
      this._carregarOperadoras();
    });

    this._carregarOperadoras();
  }

  onBuscaChange(value: string): void {
    this._busca$.next(value);
  }

  toggleExibirInativas(): void {
    this.exibirInativas.set(!this.exibirInativas());
    this.pagina.set(1);
    this._carregarOperadoras();
  }

  navegar(id: string): void {
    void this._router.navigate(['/admin/catalog/operadoras', id]);
  }

  novaOperadora(): void {
    void this._router.navigate(['/admin/catalog/operadoras/nova']);
  }

  excluir(operadora: OperadoraItem, event: Event): void {
    event.stopPropagation();
    if (!window.confirm(`Excluir operadora "${operadora.nome}"?`)) {
      return;
    }
    this._catalogService.excluirOperadora(operadora.id).subscribe({
      next: () => {
        this._carregarOperadoras();
      },
      error: () => undefined,
    });
  }

  proximaPagina(): void {
    const totalPaginas = Math.ceil(this.total() / this.itensPorPagina());
    if (this.pagina() < totalPaginas) {
      this.pagina.set(this.pagina() + 1);
      this._carregarOperadoras();
    }
  }

  paginaAnterior(): void {
    if (this.pagina() > 1) {
      this.pagina.set(this.pagina() - 1);
      this._carregarOperadoras();
    }
  }

  tipoBadgeClass(tipo: TipoRuleSet): string {
    return tipo === 'Unimed' ? 'badge badge--unimed' : 'badge badge--nulo';
  }

  tipoLabel(tipo: TipoRuleSet): string {
    return tipo === 'Unimed' ? 'UNIMED' : 'Sem cálculo';
  }

  formatarCnpj(cnpj: string | null): string {
    if (!cnpj) {
      return '—';
    }
    if (cnpj.length !== 14) {
      return cnpj;
    }
    return `${cnpj.slice(0, 2)}.${cnpj.slice(2, 5)}.${cnpj.slice(5, 8)}/${cnpj.slice(8, 12)}-${cnpj.slice(12)}`;
  }

  private _carregarOperadoras(): void {
    this.loading.set(true);
    this._catalogService
      .listarOperadoras({
        nome: this.filtroNome() || undefined,
        ativa: this.exibirInativas() ? undefined : true,
        pagina: this.pagina(),
        itensPorPagina: this.itensPorPagina(),
      })
      .subscribe({
        next: (result) => {
          this.operadoras.set(result.itens);
          this.total.set(result.total);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }
}
