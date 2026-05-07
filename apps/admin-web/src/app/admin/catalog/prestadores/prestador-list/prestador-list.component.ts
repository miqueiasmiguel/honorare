import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { debounceTime, Subject } from 'rxjs';
import { CatalogService } from '../../catalog.service';
import type { PrestadorItem } from '../../catalog.types';

@Component({
  selector: 'app-prestador-list',
  templateUrl: './prestador-list.component.html',
  styleUrl: './prestador-list.component.scss',
})
export class PrestadorListComponent implements OnInit {
  private readonly _catalogService = inject(CatalogService);
  private readonly _router = inject(Router);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _busca$ = new Subject<string>();

  readonly prestadores = signal<PrestadorItem[]>([]);
  readonly loading = signal(false);
  readonly total = signal(0);
  readonly pagina = signal(1);
  readonly itensPorPagina = signal(20);
  readonly filtroBusca = signal('');
  readonly exibirInativos = signal(false);
  readonly erroExclusao = signal('');

  ngOnInit(): void {
    this._busca$
      .pipe(debounceTime(400), takeUntilDestroyed(this._destroyRef))
      .subscribe((busca) => {
        this.filtroBusca.set(busca);
        this.pagina.set(1);
        this._carregarPrestadores();
      });
    this._carregarPrestadores();
  }

  onBuscaChange(value: string): void {
    this._busca$.next(value);
  }

  toggleExibirInativos(): void {
    this.exibirInativos.set(!this.exibirInativos());
    this.pagina.set(1);
    this._carregarPrestadores();
  }

  novo(): void {
    void this._router.navigate(['/admin/catalog/prestadores/novo']);
  }

  editar(prestador: PrestadorItem): void {
    void this._router.navigate(['/admin/catalog/prestadores', prestador.id]);
  }

  excluir(prestador: PrestadorItem, event: Event): void {
    event.stopPropagation();
    if (!window.confirm(`Excluir prestador "${prestador.nome}"?`)) {
      return;
    }
    this.erroExclusao.set('');
    this._catalogService.excluirPrestador(prestador.id).subscribe({
      next: () => {
        this._carregarPrestadores();
      },
      error: (err: unknown) => {
        if (err instanceof HttpErrorResponse && err.status === 409) {
          this.erroExclusao.set('Prestador possui guias associadas e não pode ser excluído.');
        }
      },
    });
  }

  proximaPagina(): void {
    const totalPaginas = Math.ceil(this.total() / this.itensPorPagina());
    if (this.pagina() < totalPaginas) {
      this.pagina.set(this.pagina() + 1);
      this._carregarPrestadores();
    }
  }

  paginaAnterior(): void {
    if (this.pagina() > 1) {
      this.pagina.set(this.pagina() - 1);
      this._carregarPrestadores();
    }
  }

  private _carregarPrestadores(): void {
    this.loading.set(true);
    this._catalogService
      .listarPrestadores({
        busca: this.filtroBusca() || undefined,
        ativo: this.exibirInativos() ? undefined : true,
        pagina: this.pagina(),
        itensPorPagina: this.itensPorPagina(),
      })
      .subscribe({
        next: (result) => {
          this.prestadores.set(result.itens);
          this.total.set(result.total);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }
}
