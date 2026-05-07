import { Component, EventEmitter, inject, Input, Output, signal } from '@angular/core';
import { CatalogService } from '../../catalog.service';
import type { ImportarCsvResult } from '../../catalog.types';

@Component({
  selector: 'app-tabela-csv-modal',
  templateUrl: './tabela-csv-modal.component.html',
  styleUrl: './tabela-csv-modal.component.scss',
})
export class TabelaCsvModalComponent {
  @Input() operadoraId = '';

  @Output() concluido = new EventEmitter<void>();
  @Output() cancelado = new EventEmitter<void>();

  private readonly _catalogService = inject(CatalogService);

  readonly uploading = signal(false);
  readonly resultado = signal<ImportarCsvResult | null>(null);

  onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    this.onArquivoSelecionado(file);
  }

  onArquivoSelecionado(file: File): void {
    this.uploading.set(true);
    this.resultado.set(null);
    this._catalogService.importarTabelaCsv(this.operadoraId, file).subscribe({
      next: (r) => {
        this.uploading.set(false);
        this.resultado.set(r);
      },
      error: () => {
        this.uploading.set(false);
      },
    });
  }

  fechar(): void {
    this.cancelado.emit();
  }

  concluir(): void {
    this.concluido.emit();
  }
}
