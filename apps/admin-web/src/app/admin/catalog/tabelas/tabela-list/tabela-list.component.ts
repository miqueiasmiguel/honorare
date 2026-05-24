import { Component, inject, OnInit, signal } from '@angular/core';
import { CatalogService } from '../../catalog.service';
import { TabelaFormComponent } from '../tabela-form/tabela-form.component';
import { TabelaCsvModalComponent } from '../tabela-csv-modal/tabela-csv-modal.component';
import { TabelaPorteAnestesicoCsvModalComponent } from '../tabela-porte-anestesico-csv-modal/tabela-porte-anestesico-csv-modal.component';
import type { OperadoraItem, TabelaItem } from '../../catalog.types';

@Component({
  selector: 'app-tabela-list',
  imports: [TabelaFormComponent, TabelaCsvModalComponent, TabelaPorteAnestesicoCsvModalComponent],
  templateUrl: './tabela-list.component.html',
  styleUrl: './tabela-list.component.scss',
})
export class TabelaListComponent implements OnInit {
  private readonly _catalogService = inject(CatalogService);

  readonly operadoras = signal<OperadoraItem[]>([]);
  readonly selectedOperadoraId = signal('');
  readonly tabelas = signal<TabelaItem[]>([]);
  readonly loading = signal(false);
  readonly total = signal(0);
  readonly pagina = signal(1);
  readonly itensPorPagina = signal(20);
  readonly mostrarForm = signal(false);
  readonly editandoTabelaId = signal<string | null>(null);
  readonly mostrarCsvModal = signal(false);
  readonly mostrarModalPorte = signal(false);

  ngOnInit(): void {
    this._catalogService
      .listarOperadoras({ pagina: 1, itensPorPagina: 200, ativa: true })
      .subscribe({
        next: (r) => {
          this.operadoras.set(r.itens);
        },
        error: () => undefined,
      });
  }

  onOperadoraChange(id: string): void {
    this.selectedOperadoraId.set(id);
    this.pagina.set(1);
    if (id) {
      this._carregarTabelas();
    } else {
      this.tabelas.set([]);
      this.total.set(0);
    }
  }

  novo(): void {
    this.editandoTabelaId.set(null);
    this.mostrarForm.set(true);
  }

  editar(tabela: TabelaItem): void {
    this.editandoTabelaId.set(tabela.id);
    this.mostrarForm.set(true);
  }

  excluir(tabela: TabelaItem, event: Event): void {
    event.stopPropagation();
    if (!window.confirm(`Excluir entrada "${tabela.codigoTuss}"?`)) {
      return;
    }
    this._catalogService.excluirTabela(tabela.id).subscribe({
      next: () => {
        this._carregarTabelas();
      },
      error: () => undefined,
    });
  }

  abrirCsvModal(): void {
    this.mostrarCsvModal.set(true);
  }

  abrirModalPorte(): void {
    this.mostrarModalPorte.set(true);
  }

  onFormSalvo(): void {
    this.mostrarForm.set(false);
    this.editandoTabelaId.set(null);
    this._carregarTabelas();
  }

  onFormCancelado(): void {
    this.mostrarForm.set(false);
    this.editandoTabelaId.set(null);
  }

  onCsvConcluido(): void {
    this.mostrarCsvModal.set(false);
    this._carregarTabelas();
  }

  proximaPagina(): void {
    const totalPaginas = Math.ceil(this.total() / this.itensPorPagina());
    if (this.pagina() < totalPaginas) {
      this.pagina.set(this.pagina() + 1);
      this._carregarTabelas();
    }
  }

  paginaAnterior(): void {
    if (this.pagina() > 1) {
      this.pagina.set(this.pagina() - 1);
      this._carregarTabelas();
    }
  }

  formatarValor(valor: number): string {
    return valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }

  private _carregarTabelas(): void {
    const operadoraId = this.selectedOperadoraId();
    if (!operadoraId) {
      return;
    }
    this.loading.set(true);
    this._catalogService
      .listarTabelas({
        operadoraId,
        pagina: this.pagina(),
        itensPorPagina: this.itensPorPagina(),
      })
      .subscribe({
        next: (result) => {
          this.tabelas.set(result.itens);
          this.total.set(result.total);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }
}
