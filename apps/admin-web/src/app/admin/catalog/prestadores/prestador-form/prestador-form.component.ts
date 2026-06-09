import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CatalogService } from '../../catalog.service';

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
  readonly prestadorEmailAcesso = signal<string | null>(null);
  readonly prestadorTemUsuario = signal(false);
  readonly savingEmailAcesso = signal(false);
  readonly erroEmailAcesso = signal<string | null>(null);

  readonly emailAcessoForm = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [(c) => Validators.required(c), (c) => Validators.email(c)],
    }),
  });

  readonly form = new FormGroup({
    nome: new FormControl('', {
      nonNullable: true,
      validators: [(c) => Validators.required(c)],
    }),
    registroProfissional: new FormControl('', { nonNullable: true }),
    ativo: new FormControl(true, { nonNullable: true }),
    emailAcesso: new FormControl('', {
      nonNullable: true,
      validators: [(c) => (c.value ? Validators.email(c) : null)],
    }),
  });

  get modoEdicao(): boolean {
    return this.prestadorId() !== null;
  }

  get titulo(): string {
    return this.modoEdicao ? 'Editar prestador' : 'Novo prestador';
  }

  ngOnInit(): void {
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
          this.prestadorEmailAcesso.set(p.emailAcesso);
          this.prestadorTemUsuario.set(p.temUsuario);
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
    this.saving.set(true);

    const id = this.prestadorId();
    const op$ =
      id !== null
        ? this._catalogService.atualizarPrestador(id, {
            nome: raw.nome,
            registroProfissional: raw.registroProfissional || null,
            ativo: raw.ativo,
          })
        : this._catalogService.criarPrestador({
            nome: raw.nome,
            registroProfissional: raw.registroProfissional || null,
            emailAcesso: raw.emailAcesso || null,
          });

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

  definirEmailAcesso(): void {
    this.emailAcessoForm.markAllAsTouched();
    if (this.emailAcessoForm.invalid || this.savingEmailAcesso()) {
      return;
    }

    const id = this.prestadorId();
    if (!id) {
      return;
    }

    this.savingEmailAcesso.set(true);
    this.erroEmailAcesso.set(null);

    this._catalogService
      .definirEmailAcesso(id, { email: this.emailAcessoForm.getRawValue().email })
      .subscribe({
        next: (p) => {
          this.savingEmailAcesso.set(false);
          this.prestadorEmailAcesso.set(p.emailAcesso);
          this.prestadorTemUsuario.set(p.temUsuario);
          this.emailAcessoForm.reset();
        },
        error: () => {
          this.savingEmailAcesso.set(false);
          this.erroEmailAcesso.set('Erro ao definir e-mail. Verifique se já não está em uso.');
        },
      });
  }
}
