import {
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { debounceTime, Subject } from 'rxjs';
import { CatalogService } from '../../catalog.service';
import type { OperadoraItem, ProcedimentoItem, TabelaItem } from '../../catalog.types';

@Component({
  selector: 'app-tabela-form',
  imports: [ReactiveFormsModule],
  templateUrl: './tabela-form.component.html',
  styleUrl: './tabela-form.component.scss',
})
export class TabelaFormComponent implements OnInit {
  @Input() tabelaId: string | null = null;
  @Input() preselectedOperadoraId: string | null = null;

  @Output() salvo = new EventEmitter<TabelaItem>();
  @Output() cancelado = new EventEmitter<void>();

  private readonly _catalogService = inject(CatalogService);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _buscaProc$ = new Subject<string>();

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly operadoras = signal<OperadoraItem[]>([]);
  readonly procedimentosBusca = signal<ProcedimentoItem[]>([]);
  readonly buscaTexto = signal('');

  readonly form = new FormGroup({
    operadoraId: new FormControl('', {
      nonNullable: true,
      validators: [(c) => Validators.required(c)],
    }),
    procedimentoId: new FormControl('', {
      nonNullable: true,
      validators: [(c) => Validators.required(c)],
    }),
    valor: new FormControl<number | null>(null, {
      validators: [(c) => Validators.required(c), (c) => Validators.min(0.01)(c)],
    }),
  });

  get modoEdicao(): boolean {
    return this.tabelaId !== null;
  }

  get titulo(): string {
    return this.modoEdicao ? 'Editar entrada' : 'Nova entrada';
  }

  ngOnInit(): void {
    this._buscaProc$
      .pipe(debounceTime(300), takeUntilDestroyed(this._destroyRef))
      .subscribe((busca) => {
        if (busca.length >= 2) {
          this._catalogService
            .listarProcedimentos({ busca, ativo: true, pagina: 1, itensPorPagina: 20 })
            .subscribe({
              next: (r) => {
                this.procedimentosBusca.set(r.itens);
              },
              error: () => undefined,
            });
        } else {
          this.procedimentosBusca.set([]);
        }
      });

    this._catalogService
      .listarOperadoras({ pagina: 1, itensPorPagina: 200, ativa: true })
      .subscribe({
        next: (r) => {
          this.operadoras.set(r.itens);
          if (this.preselectedOperadoraId) {
            this.form.controls.operadoraId.setValue(this.preselectedOperadoraId);
          }
        },
        error: () => undefined,
      });

    if (this.tabelaId) {
      this.loading.set(true);
      this._catalogService.obterTabela(this.tabelaId).subscribe({
        next: (t) => {
          this.form.patchValue({
            operadoraId: t.operadoraId,
            procedimentoId: t.procedimentoId,
            valor: t.valor,
          });
          this.buscaTexto.set(`${t.codigoTuss} — ${t.descricao}`);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
    }
  }

  onBuscaProcedimento(value: string): void {
    this.buscaTexto.set(value);
    this.form.controls.procedimentoId.setValue('');
    this._buscaProc$.next(value);
  }

  selecionarProcedimento(proc: ProcedimentoItem): void {
    this.form.controls.procedimentoId.setValue(proc.id);
    this.buscaTexto.set(`${proc.codigoTuss} — ${proc.descricao}`);
    this.procedimentosBusca.set([]);
  }

  salvar(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving()) {
      return;
    }

    const raw = this.form.getRawValue();
    const valor = raw.valor;
    if (valor === null) {
      return;
    }

    const payload = {
      operadoraId: raw.operadoraId,
      procedimentoId: raw.procedimentoId,
      valor,
    };

    this.saving.set(true);

    const op$ = this.tabelaId
      ? this._catalogService.atualizarTabela(this.tabelaId, payload)
      : this._catalogService.criarTabela(payload);

    op$.subscribe({
      next: (t) => {
        this.saving.set(false);
        this.salvo.emit(t);
      },
      error: () => {
        this.saving.set(false);
      },
    });
  }

  cancelar(): void {
    this.cancelado.emit();
  }
}
