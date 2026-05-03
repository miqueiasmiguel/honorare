import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { debounceTime, Subject } from 'rxjs';
import { CatalogService } from '../../catalog.service';
import type { ImportarCsvResult, ProcedimentoItem } from '../../catalog.types';

@Component({
  selector: 'app-procedimento-list',
  templateUrl: './procedimento-list.component.html',
  styleUrl: './procedimento-list.component.scss',
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
  readonly importResult = signal<ImportarCsvResult | null>(null);

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

  excluir(proc: ProcedimentoItem, event: Event): void {
    event.stopPropagation();
    if (!window.confirm(`Excluir procedimento "${proc.codigoTuss} — ${proc.descricao}"?`)) {
      return;
    }
    this._catalogService.excluirProcedimento(proc.id).subscribe({
      next: () => {
        this._carregarProcedimentos();
      },
      error: () => undefined,
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

  onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    this.onArquivoSelecionado(file);
  }

  onArquivoSelecionado(file: File): void {
    this.importResult.set(null);
    this._catalogService.importarCsv(file).subscribe({
      next: (result) => {
        this.importResult.set(result);
      },
      error: () => undefined,
    });
  }

  downloadTemplate(): void {
    const linhas = [
      'CodigoTuss;Descricao;Porte;PorteAnestesico;EhSadt;TemPorteProprioVideo',
      '30715013;Herniorrafia inguinal;6B;4;false;false',
      '40314340;Eletroencefalograma;;;true;false',
    ];
    const conteudo = linhas.join('\n');
    const blob = new Blob([conteudo], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'template-procedimentos.csv';
    a.click();
    URL.revokeObjectURL(url);
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
