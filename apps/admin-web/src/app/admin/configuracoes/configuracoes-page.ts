import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { TenantService } from './tenant.service';
import type { TenantSettings } from './tenant.types';

@Component({
  selector: 'app-configuracoes-page',
  imports: [ReactiveFormsModule],
  templateUrl: './configuracoes-page.html',
  styleUrl: './configuracoes-page.scss',
})
export class ConfiguracoesPage implements OnInit {
  private readonly _tenantService = inject(TenantService);

  readonly settings = signal<TenantSettings | null>(null);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly logoUrl = signal<string | null>(null);
  readonly uploadingLogo = signal(false);
  readonly erroValidacao = signal<string | null>(null);

  readonly form = new FormGroup({
    nome: new FormControl('', {
      nonNullable: true,
      validators: [(c) => Validators.required(c), (c) => Validators.maxLength(256)(c)],
    }),
  });

  ngOnInit(): void {
    this._tenantService.getSettings().subscribe({
      next: (s) => {
        this.settings.set(s);
        this.form.controls.nome.setValue(s.name);
        if (s.hasLogo) {
          this._tenantService.downloadLogo().subscribe({
            next: (blob) => {
              this.logoUrl.set(URL.createObjectURL(blob));
            },
            error: () => undefined,
          });
        }
      },
      error: () => {
        this.erroValidacao.set('Erro ao carregar configurações.');
      },
    });
  }

  submit(): void {
    if (this.form.invalid || this.saving()) {
      return;
    }
    this.saving.set(true);
    this.saved.set(false);
    this._tenantService.rename(this.form.controls.nome.value).subscribe({
      next: (s) => {
        this.settings.set(s);
        this.saving.set(false);
        this.saved.set(true);
      },
      error: () => {
        this.saving.set(false);
      },
    });
  }

  selecionarArquivo(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    if (!['image/png', 'image/jpeg'].includes(file.type)) {
      this.erroValidacao.set('Formato inválido. Use PNG ou JPEG.');
      return;
    }
    if (file.size > 2 * 1024 * 1024) {
      this.erroValidacao.set('Arquivo muito grande. Máximo 2 MB.');
      return;
    }
    this.erroValidacao.set(null);
    this.uploadingLogo.set(true);
    this._tenantService.uploadLogo(file).subscribe({
      next: (s) => {
        this.settings.set(s);
        this.uploadingLogo.set(false);
        this.logoUrl.set(URL.createObjectURL(file));
      },
      error: () => {
        this.uploadingLogo.set(false);
        this.erroValidacao.set('Erro ao enviar logo.');
      },
    });
  }

  removerLogo(): void {
    this._tenantService.deleteLogo().subscribe({
      next: () => {
        const current = this.logoUrl();
        if (current) {
          URL.revokeObjectURL(current);
        }
        this.logoUrl.set(null);
        const s = this.settings();
        if (s) {
          this.settings.set({ ...s, hasLogo: false });
        }
      },
      error: () => {
        this.erroValidacao.set('Erro ao remover logo.');
      },
    });
  }
}
