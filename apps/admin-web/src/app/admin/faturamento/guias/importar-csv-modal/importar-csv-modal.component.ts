import { Component, EventEmitter, inject, Input, OnInit, Output, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { CatalogService } from '../../../catalog/catalog.service';
import { GuiaService } from '../../guia.service';
import type { OperadoraItem, PrestadorItem } from '../../../catalog/catalog.types';
import type { ResultadoImportacaoGuiaDto } from '../../guia.types';

@Component({
  selector: 'app-importar-csv-modal',
  templateUrl: './importar-csv-modal.component.html',
  styleUrl: './importar-csv-modal.component.scss',
})
export class ImportarCsvModalComponent implements OnInit {
  @Input() open = false;
  @Output() concluido = new EventEmitter<void>();
  @Output() cancelado = new EventEmitter<void>();

  private readonly _catalogService = inject(CatalogService);
  private readonly _guiaService = inject(GuiaService);

  readonly prestadores = signal<PrestadorItem[]>([]);
  readonly operadoras = signal<OperadoraItem[]>([]);
  readonly prestadorId = signal('');
  readonly operadoraId = signal('');
  readonly arquivo = signal<File | null>(null);
  readonly somenteValidar = signal(false);
  readonly uploading = signal(false);
  readonly resultado = signal<ResultadoImportacaoGuiaDto | null>(null);
  readonly erroValidacao = signal('');

  ngOnInit(): void {
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
        this.erroValidacao.set('Erro ao carregar prestadores e operadoras.');
      },
    });
  }

  onArquivoChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) {
      this.arquivo.set(file);
    }
  }

  importar(): void {
    const arquivo = this.arquivo();
    if (!arquivo || !this.prestadorId() || !this.operadoraId()) {
      this.erroValidacao.set('Selecione prestador, operadora e arquivo CSV.');
      return;
    }
    this.erroValidacao.set('');
    this.uploading.set(true);
    this._guiaService
      .importarCsv(arquivo, this.prestadorId(), this.operadoraId(), this.somenteValidar())
      .subscribe({
        next: (res) => {
          this.uploading.set(false);
          this.resultado.set(res);
        },
        error: () => {
          this.uploading.set(false);
          this.erroValidacao.set('Erro ao importar CSV. Verifique o arquivo e tente novamente.');
        },
      });
  }

  cancelar(): void {
    this.cancelado.emit();
  }

  concluir(): void {
    this.concluido.emit();
  }
}
