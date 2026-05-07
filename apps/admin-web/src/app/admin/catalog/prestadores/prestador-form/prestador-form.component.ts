import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CatalogService } from '../../catalog.service';
import type { DeflatorItem, OperadoraItem, PosicaoExecutor } from '../../catalog.types';

const POSICAO_OPCOES: { value: PosicaoExecutor; label: string }[] = [
  { value: 'Cirurgiao', label: 'Cirurgião' },
  { value: 'PrimeiroAuxiliar', label: '1º Auxiliar' },
  { value: 'SegundoAuxiliar', label: '2º Auxiliar' },
  { value: 'TerceiroAuxiliar', label: '3º Auxiliar' },
  { value: 'Anestesista', label: 'Anestesista' },
  { value: 'ClinicoAssistente', label: 'Clínico Assistente' },
];

@Component({
  selector: 'app-prestador-form',
  imports: [ReactiveFormsModule],
  templateUrl: './prestador-form.component.html',
  styleUrl: './prestador-form.component.scss',
})
export class PrestadorFormComponent implements OnInit {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _catalogService = inject(CatalogService);

  readonly prestadorId = signal<string | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly savingDeflator = signal(false);
  readonly deflatores = signal<DeflatorItem[]>([]);
  readonly operadoras = signal<OperadoraItem[]>([]);
  readonly mostrarFormDeflator = signal(false);
  readonly posicaoOpcoes = POSICAO_OPCOES;

  readonly form = new FormGroup({
    nome: new FormControl('', {
      nonNullable: true,
      validators: [(c) => Validators.required(c)],
    }),
    registroProfissional: new FormControl('', { nonNullable: true }),
    ativo: new FormControl(true, { nonNullable: true }),
  });

  readonly deflatorForm = new FormGroup({
    operadoraId: new FormControl('', {
      nonNullable: true,
      validators: [(c) => Validators.required(c)],
    }),
    posicao: new FormControl<PosicaoExecutor>('Cirurgiao', {
      nonNullable: true,
      validators: [(c) => Validators.required(c)],
    }),
    percentual: new FormControl(100, {
      nonNullable: true,
      validators: [
        (c) => Validators.required(c),
        (c) => Validators.min(0.01)(c),
        (c) => Validators.max(200)(c),
      ],
    }),
  });

  get modoEdicao(): boolean {
    return this.prestadorId() !== null;
  }

  get titulo(): string {
    return this.modoEdicao ? 'Editar prestador' : 'Novo prestador';
  }

  ngOnInit(): void {
    this._carregarOperadoras();

    const id = this._route.snapshot.paramMap.get('id');
    if (id) {
      this.prestadorId.set(id);
      this.loading.set(true);
      this._catalogService.obterPrestador(id).subscribe({
        next: (p) => {
          this.form.patchValue({
            nome: p.nome,
            registroProfissional: p.registroProfissional ?? '',
            ativo: p.ativo,
          });
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
      this._recarregarDeflatores();
    }
  }

  salvar(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving()) {
      return;
    }

    const raw = this.form.getRawValue();
    const payload = {
      nome: raw.nome,
      registroProfissional: raw.registroProfissional || null,
      ativo: raw.ativo,
    };

    this.saving.set(true);

    const id = this.prestadorId();
    const op$ =
      id !== null
        ? this._catalogService.atualizarPrestador(id, payload)
        : this._catalogService.criarPrestador(payload);

    op$.subscribe({
      next: () => {
        this.saving.set(false);
        void this._router.navigate(['/admin/catalog/prestadores']);
      },
      error: () => {
        this.saving.set(false);
      },
    });
  }

  cancelar(): void {
    void this._router.navigate(['/admin/catalog/prestadores']);
  }

  abrirFormDeflator(): void {
    this.deflatorForm.reset();
    this.mostrarFormDeflator.set(true);
  }

  cancelarDeflator(): void {
    this.mostrarFormDeflator.set(false);
  }

  salvarDeflator(): void {
    this.deflatorForm.markAllAsTouched();
    if (this.deflatorForm.invalid || this.savingDeflator()) {
      return;
    }

    const id = this.prestadorId();
    if (!id) {
      return;
    }

    const raw = this.deflatorForm.getRawValue();
    this.savingDeflator.set(true);

    this._catalogService
      .criarDeflator(id, {
        operadoraId: raw.operadoraId,
        posicao: raw.posicao,
        percentual: raw.percentual,
      })
      .subscribe({
        next: () => {
          this.savingDeflator.set(false);
          this.mostrarFormDeflator.set(false);
          this._recarregarDeflatores();
        },
        error: () => {
          this.savingDeflator.set(false);
        },
      });
  }

  excluirDeflator(deflatorId: string): void {
    if (!window.confirm('Excluir deflator?')) {
      return;
    }
    const id = this.prestadorId();
    if (!id) {
      return;
    }
    this._catalogService.excluirDeflator(id, deflatorId).subscribe({
      next: () => {
        this._recarregarDeflatores();
      },
      error: () => undefined,
    });
  }

  posicaoLabel(posicao: PosicaoExecutor): string {
    return POSICAO_OPCOES.find((p) => p.value === posicao)?.label ?? posicao;
  }

  operadoraNome(operadoraId: string): string {
    return this.operadoras().find((op) => op.id === operadoraId)?.nome ?? operadoraId;
  }

  private _carregarOperadoras(): void {
    this._catalogService.listarOperadoras({ pagina: 1, itensPorPagina: 200 }).subscribe({
      next: (result) => {
        this.operadoras.set(result.itens);
      },
      error: () => undefined,
    });
  }

  private _recarregarDeflatores(): void {
    const id = this.prestadorId();
    if (!id) {
      return;
    }
    this._catalogService.listarDeflatores(id).subscribe({
      next: (deflatores) => {
        this.deflatores.set(deflatores);
      },
      error: () => undefined,
    });
  }
}
