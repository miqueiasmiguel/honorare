import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { TenantService } from './tenant.service';
import { CatalogService } from '../catalog/catalog.service';
import { RecortarLogoModalComponent } from './recortar-logo-modal/recortar-logo-modal.component';
import type { TenantSettings } from './tenant.types';

interface NaoRecorrivelItem {
  codigoTuss: string;
  descricao: string;
}

@Component({
  selector: 'app-configuracoes-page',
  imports: [ReactiveFormsModule, RecortarLogoModalComponent],
  templateUrl: './configuracoes-page.html',
  styleUrl: './configuracoes-page.scss',
})
export class ConfiguracoesPage implements OnInit {
  private readonly _tenantService = inject(TenantService);
  private readonly _catalogService = inject(CatalogService);

  readonly settings = signal<TenantSettings | null>(null);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly logoUrl = signal<string | null>(null);
  readonly uploadingLogo = signal(false);
  readonly erroValidacao = signal<string | null>(null);
  readonly arquivoParaRecorte = signal<File | null>(null);

  readonly naoRecorriveis = signal<NaoRecorrivelItem[]>([]);
  readonly buscaNaoRecorrivel = signal('');
  readonly resultadosBusca = signal<NaoRecorrivelItem[]>([]);
  readonly salvandoNaoRecorriveis = signal(false);

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

        if (s.codigosNaoRecorriveis.length > 0) {
          this._catalogService.listarProcedimentos({ pagina: 1, itensPorPagina: 200 }).subscribe({
            next: (r) => {
              const mapa = new Map(r.itens.map((p) => [p.codigoTuss, p.descricao]));
              this.naoRecorriveis.set(
                s.codigosNaoRecorriveis.map((c) => ({
                  codigoTuss: c,
                  descricao: mapa.get(c) ?? c,
                })),
              );
            },
            error: () => {
              this.naoRecorriveis.set(
                s.codigosNaoRecorriveis.map((c) => ({ codigoTuss: c, descricao: c })),
              );
            },
          });
        }

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
    // Limpa o value para permitir reselecionar o mesmo arquivo depois.
    input.value = '';
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
    this.arquivoParaRecorte.set(file);
  }

  aoRecortar(file: File): void {
    this.arquivoParaRecorte.set(null);
    this.enviarLogo(file);
  }

  aoCancelarRecorte(): void {
    this.arquivoParaRecorte.set(null);
  }

  buscarProcedimentos(busca: string): void {
    this.buscaNaoRecorrivel.set(busca);
    if (!busca.trim()) {
      this.resultadosBusca.set([]);
      return;
    }
    this._catalogService.listarProcedimentos({ busca, pagina: 1, itensPorPagina: 10 }).subscribe({
      next: (r) => {
        this.resultadosBusca.set(
          r.itens.map((p) => ({ codigoTuss: p.codigoTuss, descricao: p.descricao })),
        );
      },
      error: () => {
        this.resultadosBusca.set([]);
      },
    });
  }

  adicionarNaoRecorrivel(proc: NaoRecorrivelItem): void {
    const atual = this.naoRecorriveis();
    if (atual.some((p) => p.codigoTuss === proc.codigoTuss)) {
      return;
    }
    this.naoRecorriveis.set([...atual, proc]);
    this.buscaNaoRecorrivel.set('');
    this.resultadosBusca.set([]);
  }

  removerNaoRecorrivel(codigoTuss: string): void {
    this.naoRecorriveis.set(this.naoRecorriveis().filter((p) => p.codigoTuss !== codigoTuss));
  }

  salvarNaoRecorriveis(): void {
    if (this.salvandoNaoRecorriveis()) {
      return;
    }
    this.salvandoNaoRecorriveis.set(true);
    this._tenantService
      .atualizarCodigosNaoRecorriveis(this.naoRecorriveis().map((p) => p.codigoTuss))
      .subscribe({
        next: (s) => {
          this.settings.set(s);
          this.salvandoNaoRecorriveis.set(false);
        },
        error: () => {
          this.salvandoNaoRecorriveis.set(false);
          this.erroValidacao.set('Erro ao salvar procedimentos não recorríveis.');
        },
      });
  }

  private enviarLogo(file: File): void {
    this.uploadingLogo.set(true);
    this._tenantService.uploadLogo(file).subscribe({
      next: (s) => {
        this.settings.set(s);
        this.uploadingLogo.set(false);
        const previous = this.logoUrl();
        if (previous) {
          URL.revokeObjectURL(previous);
        }
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
