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
import { CatalogService } from '../../catalog.service';
import type {
  ImportarCsvResult,
  ImportarTabelaPorteResult,
  OperadoraItem,
} from '../../catalog.types';

export type TipoImportacao = 'procedimentos' | 'valoresOperadora' | 'porteAnestesico';

interface FormatoInfo {
  separador: string;
  encoding: string;
  colunas: string;
  exemplo: string;
  arquivo: string;
  obsExtra?: string;
}

const FORMATOS: Record<TipoImportacao, FormatoInfo> = {
  procedimentos: {
    separador: ';',
    encoding: 'UTF-8',
    colunas: 'CodigoTuss;Descricao;Porte;PorteAnestesico;EhSadt;TemPorteProprioVideo',
    exemplo: [
      'CodigoTuss;Descricao;Porte;PorteAnestesico;EhSadt;TemPorteProprioVideo',
      '30715013;Herniorrafia inguinal;6B;J;false;false',
      '40314340;Eletroencefalograma;;;true;false',
    ].join('\n'),
    arquivo: 'template-procedimentos.csv',
  },
  valoresOperadora: {
    separador: ';',
    encoding: 'UTF-8',
    colunas: 'CodigoTuss;Valor',
    exemplo: ['CodigoTuss;Valor', '30715013;526,50', '40314340;124,80'].join('\n'),
    arquivo: 'template-valores-operadora.csv',
  },
  porteAnestesico: {
    separador: 'vírgula com aspas',
    encoding: 'UTF-8',
    colunas: 'Código,Procedimento,Honorários,VL AMB,VL ENF,VL AP,Porte (8 linhas de header)',
    exemplo:
      '… (8 linhas de header) …\nCódigo,Procedimento,Honorários,VL AMB,VL ENF,VL AP,Porte\n30101050,APENDICE PRE-AURICULAR,"224,64",,"292,5",468,E',
    arquivo: 'exemplo-unimed-jpa.csv',
    obsExtra: 'Arquivo no formato UNIMED JPA — manter o cabeçalho de 8 linhas.',
  },
};

const TIPOS_DISPONIVEIS: { valor: TipoImportacao; label: string }[] = [
  { valor: 'procedimentos', label: 'Procedimentos (TUSS)' },
  { valor: 'valoresOperadora', label: 'Valores por Operadora' },
  { valor: 'porteAnestesico', label: 'Tabela de Porte Anestésico' },
];

@Component({
  selector: 'app-importar-modal',
  templateUrl: './importar-modal.component.html',
  styleUrl: './importar-modal.component.scss',
})
export class ImportarModalComponent implements OnInit {
  @Input() open = false;

  @Output() concluido = new EventEmitter<void>();
  @Output() cancelado = new EventEmitter<void>();

  private readonly _catalogService = inject(CatalogService);

  readonly tiposDisponiveis = TIPOS_DISPONIVEIS;
  readonly tipo = signal<TipoImportacao>('procedimentos');
  readonly operadoras = signal<OperadoraItem[]>([]);
  readonly operadoraId = signal('');
  readonly arquivo = signal<File | null>(null);
  readonly uploading = signal(false);
  readonly resultadoCsv = signal<ImportarCsvResult | null>(null);
  readonly resultadoPorte = signal<ImportarTabelaPorteResult | null>(null);

  readonly requerOperadora = computed(() => this.tipo() !== 'procedimentos');
  readonly formato = computed<FormatoInfo>(() => FORMATOS[this.tipo()]);
  readonly temResultado = computed(
    () => this.resultadoCsv() !== null || this.resultadoPorte() !== null,
  );
  readonly podeImportar = computed(
    () =>
      this.arquivo() !== null &&
      !this.uploading() &&
      (!this.requerOperadora() || this.operadoraId() !== ''),
  );

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

  onTipoChange(novo: TipoImportacao): void {
    this.tipo.set(novo);
    this.resultadoCsv.set(null);
    this.resultadoPorte.set(null);
    this.arquivo.set(null);
  }

  onOperadoraChange(id: string): void {
    this.operadoraId.set(id);
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
    this.arquivo.set(file);
    this.resultadoCsv.set(null);
    this.resultadoPorte.set(null);
  }

  importar(): void {
    const file = this.arquivo();
    if (!file || !this.podeImportar()) {
      return;
    }
    this.uploading.set(true);
    const tipo = this.tipo();
    if (tipo === 'procedimentos') {
      this._catalogService.importarCsv(file).subscribe({
        next: (r) => {
          this.uploading.set(false);
          this.resultadoCsv.set(r);
        },
        error: () => {
          this.uploading.set(false);
        },
      });
      return;
    }
    if (tipo === 'valoresOperadora') {
      this._catalogService.importarTabelaCsv(this.operadoraId(), file).subscribe({
        next: (r) => {
          this.uploading.set(false);
          this.resultadoCsv.set(r);
        },
        error: () => {
          this.uploading.set(false);
        },
      });
      return;
    }
    this._catalogService.importarTabelaPorteAnestesico(this.operadoraId(), file).subscribe({
      next: (r) => {
        this.uploading.set(false);
        this.resultadoPorte.set(r);
      },
      error: () => {
        this.uploading.set(false);
      },
    });
  }

  baixarExemplo(): void {
    const info = this.formato();
    const blob = new Blob([info.exemplo], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = info.arquivo;
    a.click();
    URL.revokeObjectURL(url);
  }

  cancelar(): void {
    this.cancelado.emit();
  }

  concluir(): void {
    this.concluido.emit();
  }
}
