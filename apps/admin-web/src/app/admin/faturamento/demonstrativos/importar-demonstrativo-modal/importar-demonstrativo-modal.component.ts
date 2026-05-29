import {
  Component,
  computed,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output,
  signal,
} from '@angular/core';
import { forkJoin } from 'rxjs';
import { DemonstrativoService } from '../../demonstrativo.service';
import { CatalogService } from '../../../catalog/catalog.service';
import type { ResultadoImportacaoDto } from '../../demonstrativo.types';
import type { OperadoraItem, PrestadorItem } from '../../../catalog/catalog.types';

type Passo = 'selecao' | 'preview' | 'concluido';

@Component({
  selector: 'app-importar-demonstrativo-modal',
  templateUrl: './importar-demonstrativo-modal.component.html',
  styleUrl: './importar-demonstrativo-modal.component.scss',
})
export class ImportarDemonstrativoModalComponent implements OnInit {
  @Input() open = false;

  @Output() importacaoConcluida = new EventEmitter<void>();
  @Output() cancelado = new EventEmitter<void>();

  private readonly _demoService = inject(DemonstrativoService);
  private readonly _catalogService = inject(CatalogService);

  readonly passo = signal<Passo>('selecao');
  readonly prestadores = signal<PrestadorItem[]>([]);
  readonly operadoras = signal<OperadoraItem[]>([]);
  readonly prestadorId = signal('');
  readonly operadoraId = signal('');
  readonly arquivo = signal<File | null>(null);
  readonly carregando = signal(false);
  readonly resultado = signal<ResultadoImportacaoDto | null>(null);

  readonly podeValidar = computed(
    () => this.arquivo() !== null && this.prestadorId() !== '' && this.operadoraId() !== '',
  );

  readonly temErros = computed(() => (this.resultado()?.erros.length ?? 0) > 0);

  ngOnInit(): void {
    forkJoin({
      prestadores: this._catalogService.listarPrestadores({
        pagina: 1,
        itensPorPagina: 200,
        ativo: true,
      }),
      operadoras: this._catalogService.listarOperadoras({
        pagina: 1,
        itensPorPagina: 200,
        ativa: true,
      }),
    }).subscribe({
      next: ({ prestadores, operadoras }) => {
        this.prestadores.set(prestadores.itens);
        this.operadoras.set(operadoras.itens);
      },
      error: () => undefined,
    });
  }

  onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) {
      this.onArquivoSelecionado(file);
    }
  }

  onArquivoSelecionado(file: File): void {
    this.arquivo.set(file);
  }

  validar(): void {
    const file = this.arquivo();
    if (!file || !this.podeValidar()) {
      return;
    }
    this.carregando.set(true);
    this._demoService.importarCsv(file, this.prestadorId(), this.operadoraId(), true).subscribe({
      next: (r) => {
        this.carregando.set(false);
        this.resultado.set(r);
        this.passo.set('preview');
      },
      error: () => {
        this.carregando.set(false);
      },
    });
  }

  confirmar(): void {
    const file = this.arquivo();
    if (!file) {
      return;
    }
    this.carregando.set(true);
    this._demoService.importarCsv(file, this.prestadorId(), this.operadoraId(), false).subscribe({
      next: (r) => {
        this.carregando.set(false);
        this.resultado.set(r);
        this.passo.set('concluido');
        this.importacaoConcluida.emit();
      },
      error: () => {
        this.carregando.set(false);
      },
    });
  }

  voltar(): void {
    this.passo.set('selecao');
    this.resultado.set(null);
  }

  cancelar(): void {
    this.cancelado.emit();
  }
}
