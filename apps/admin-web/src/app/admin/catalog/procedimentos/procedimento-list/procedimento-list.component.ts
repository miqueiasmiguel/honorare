import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { debounceTime, Subject } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { CatalogService } from '../../catalog.service';
import type { ProcedimentoItem } from '../../catalog.types';
import { ImportarModalComponent } from '../importar-modal/importar-modal.component';

@Component({
  selector: 'app-procedimento-list',
  templateUrl: './procedimento-list.component.html',
  styleUrl: './procedimento-list.component.scss',
  imports: [ImportarModalComponent],
})
export class ProcedimentoListComponent implements OnInit {
  private readonly _catalogService = inject(CatalogService);
  private readonly _router = inject(Router);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _busca$ = new Subject<string>();

  readonly procedimentos = signal<ProcedimentoItem[]>([]);
  readonly loading = signal(false);
  readonly total = signal(0);
  readonly pagina = signal(1);
  readonly itensPorPagina = signal(20);
  readonly filtroBusca = signal('');
  readonly exibirInativos = signal(false);
  readonly mostrarImportarModal = signal(false);

  ngOnInit(): void {
    this._busca$
      .pipe(debounceTime(300), takeUntilDestroyed(this._destroyRef))
      .subscribe((busca) => {
        this.filtroBusca.set(busca);
        this.pagina.set(1);
        this._carregarProcedimentos();
      });

    this._carregarProcedimentos();
  }

  onBuscaChange(value: string): void {
    this._busca$.next(value);
  }

  toggleExibirInativos(): void {
    this.exibirInativos.set(!this.exibirInativos());
    this.pagina.set(1);
    this._carregarProcedimentos();
  }

  navegar(id: string): void {
    void this._router.navigate(['/admin/catalog/procedimentos', id]);
  }

  novoProcedimento(): void {
    void this._router.navigate(['/admin/catalog/procedimentos/novo']);
  }

  abrirImportarModal(): void {
    this.mostrarImportarModal.set(true);
  }

  onImportConcluido(): void {
    this.mostrarImportarModal.set(false);
    this._carregarProcedimentos();
  }

  excluir(proc: ProcedimentoItem, event: Event): void {
    event.stopPropagation();
    if (!window.confirm(`Excluir procedimento "${proc.codigoTuss} — ${proc.descricao}"?`)) {
      return;
    }
    this._catalogService.excluirProcedimento(proc.id).subscribe({
      next: () => {
        this._carregarProcedimentos();
      },
      error: (err: HttpErrorResponse) => {
        const body = err.error as { detail?: string } | null;
        const detalhe = body?.detail ?? 'Não foi possível excluir o procedimento.';
        window.alert(detalhe);
      },
    });
  }

  proximaPagina(): void {
    const totalPaginas = Math.ceil(this.total() / this.itensPorPagina());
    if (this.pagina() < totalPaginas) {
      this.pagina.set(this.pagina() + 1);
      this._carregarProcedimentos();
    }
  }

  paginaAnterior(): void {
    if (this.pagina() > 1) {
      this.pagina.set(this.pagina() - 1);
      this._carregarProcedimentos();
    }
  }

  truncar(texto: string): string {
    return texto.length > 60 ? texto.slice(0, 60) + '…' : texto;
  }

  private _carregarProcedimentos(): void {
    this.loading.set(true);
    this._catalogService
      .listarProcedimentos({
        busca: this.filtroBusca() || undefined,
        ativo: this.exibirInativos() ? undefined : true,
        pagina: this.pagina(),
        itensPorPagina: this.itensPorPagina(),
      })
      .subscribe({
        next: (result) => {
          this.procedimentos.set(result.itens);
          this.total.set(result.total);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }
}
