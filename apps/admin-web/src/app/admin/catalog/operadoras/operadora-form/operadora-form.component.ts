import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CatalogService } from '../../catalog.service';
import type { TipoRuleSet } from '../../catalog.types';

const TIPO_RULESET_OPCOES: { value: TipoRuleSet; label: string }[] = [
  { value: 'Unimed', label: 'UNIMED' },
  { value: 'Nulo', label: 'Sem apuração' },
];

@Component({
  selector: 'app-operadora-form',
  imports: [ReactiveFormsModule],
  templateUrl: './operadora-form.component.html',
  styleUrl: './operadora-form.component.scss',
})
export class OperadoraFormComponent implements OnInit {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _catalogService = inject(CatalogService);

  readonly operadoraId = signal<string | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly tipoOpcoes = TIPO_RULESET_OPCOES;

  readonly form = new FormGroup({
    nome: new FormControl('', {
      nonNullable: true,
      validators: [(c) => Validators.required(c)],
    }),
    registroAns: new FormControl('', {
      nonNullable: true,
      validators: [(c) => Validators.pattern(/^(\d{6})?$/)(c)],
    }),
    cnpj: new FormControl('', {
      nonNullable: true,
      validators: [(c) => Validators.pattern(/^(\d{14})?$/)(c)],
    }),
    tipoRuleSet: new FormControl<TipoRuleSet>('Unimed', {
      nonNullable: true,
      validators: [(c) => Validators.required(c)],
    }),
    ativa: new FormControl(true, { nonNullable: true }),
  });

  get modoEdicao(): boolean {
    return this.operadoraId() !== null;
  }

  get titulo(): string {
    return this.modoEdicao ? 'Editar operadora' : 'Nova operadora';
  }

  ngOnInit(): void {
    const id = this._route.snapshot.paramMap.get('id');
    if (id) {
      this.operadoraId.set(id);
      this.loading.set(true);
      this._catalogService.obterOperadora(id).subscribe({
        next: (op) => {
          this.form.patchValue({
            nome: op.nome,
            registroAns: op.registroAns ?? '',
            cnpj: op.cnpj ?? '',
            tipoRuleSet: op.tipoRuleSet,
            ativa: op.ativa,
          });
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
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
      registroAns: raw.registroAns || null,
      cnpj: raw.cnpj || null,
      tipoRuleSet: raw.tipoRuleSet,
      ativa: raw.ativa,
    };

    this.saving.set(true);

    const id = this.operadoraId();
    const op$ =
      id !== null
        ? this._catalogService.atualizarOperadora(id, payload)
        : this._catalogService.criarOperadora(payload);

    op$.subscribe({
      next: () => {
        this.saving.set(false);
        void this._router.navigate(['/admin/catalog/operadoras']);
      },
      error: () => {
        this.saving.set(false);
      },
    });
  }

  cancelar(): void {
    void this._router.navigate(['/admin/catalog/operadoras']);
  }
}
