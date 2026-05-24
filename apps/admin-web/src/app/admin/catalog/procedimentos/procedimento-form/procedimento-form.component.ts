import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CatalogService } from '../../catalog.service';
import type { SalvarProcedimentoPayload } from '../../catalog.types';

@Component({
  selector: 'app-procedimento-form',
  imports: [ReactiveFormsModule],
  templateUrl: './procedimento-form.component.html',
  styleUrl: './procedimento-form.component.scss',
})
export class ProcedimentoFormComponent implements OnInit {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _catalogService = inject(CatalogService);

  readonly procedimentoId = signal<string | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly porteAnestesico = signal<string>('');

  readonly form = new FormGroup({
    codigoTuss: new FormControl('', {
      nonNullable: true,
      validators: [(c) => Validators.required(c), (c) => Validators.maxLength(10)(c)],
    }),
    descricao: new FormControl('', {
      nonNullable: true,
      validators: [(c) => Validators.required(c)],
    }),
    porte: new FormControl('', { nonNullable: true }),
    ehSadt: new FormControl(false, { nonNullable: true }),
    temPorteProprioVideo: new FormControl(false, { nonNullable: true }),
    ativo: new FormControl(true, { nonNullable: true }),
  });

  get modoEdicao(): boolean {
    return this.procedimentoId() !== null;
  }

  get titulo(): string {
    return this.modoEdicao ? 'Editar procedimento' : 'Novo procedimento';
  }

  ngOnInit(): void {
    const id = this._route.snapshot.paramMap.get('id');
    if (id) {
      this.procedimentoId.set(id);
      this.loading.set(true);
      this._catalogService.obterProcedimento(id).subscribe({
        next: (proc) => {
          this.form.patchValue({
            codigoTuss: proc.codigoTuss,
            descricao: proc.descricao,
            porte: proc.porte ?? '',
            ehSadt: proc.ehSadt,
            temPorteProprioVideo: proc.temPorteProprioVideo,
            ativo: proc.ativo,
          });
          this.porteAnestesico.set(proc.porteAnestesico ?? '');
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
    const payload: SalvarProcedimentoPayload = {
      codigoTuss: raw.codigoTuss,
      descricao: raw.descricao,
      porte: raw.porte || null,
      porteAnestesico: this.porteAnestesico() || null,
      ehSadt: raw.ehSadt,
      temPorteProprioVideo: raw.temPorteProprioVideo,
      ativo: raw.ativo,
    };

    this.saving.set(true);

    const id = this.procedimentoId();
    const op$ =
      id !== null
        ? this._catalogService.atualizarProcedimento(id, payload)
        : this._catalogService.criarProcedimento(payload);

    op$.subscribe({
      next: () => {
        this.saving.set(false);
        void this._router.navigate(['/admin/catalog/procedimentos']);
      },
      error: () => {
        this.saving.set(false);
      },
    });
  }

  cancelar(): void {
    void this._router.navigate(['/admin/catalog/procedimentos']);
  }
}
