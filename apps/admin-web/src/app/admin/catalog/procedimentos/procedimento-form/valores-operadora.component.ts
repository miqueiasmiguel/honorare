import { Component, inject, Input, OnInit, signal } from '@angular/core';
import { CatalogService } from '../../catalog.service';
import type { ProcedimentoValorOperadoraItem } from '../../catalog.types';

@Component({
  selector: 'app-valores-operadora',
  templateUrl: './valores-operadora.component.html',
  styleUrl: './valores-operadora.component.scss',
})
export class ValoresOperadoraComponent implements OnInit {
  @Input({ required: true }) procedimentoId!: string;

  private readonly _catalogService = inject(CatalogService);

  readonly valores = signal<ProcedimentoValorOperadoraItem[]>([]);
  readonly loading = signal(false);
  readonly editandoOperadoraId = signal<string | null>(null);
  readonly valorEditando = signal<number | null>(null);
  readonly erroPorOperadora = signal<Record<string, string>>({});
  readonly salvando = signal(false);

  ngOnInit(): void {
    this._carregar();
  }

  formatarBrl(valor: number | null): string {
    if (valor === null) {
      return '—';
    }
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL',
    }).format(valor);
  }

  iniciarEdicao(operadoraId: string): void {
    const linha = this.valores().find((l) => l.operadoraId === operadoraId);
    this.editandoOperadoraId.set(operadoraId);
    this.valorEditando.set(linha?.valor ?? null);
    this._limparErro(operadoraId);
  }

  cancelarEdicao(): void {
    this.editandoOperadoraId.set(null);
    this.valorEditando.set(null);
  }

  onValorInput(event: Event): void {
    const raw = (event.target as HTMLInputElement).value;
    const num = raw === '' ? null : Number(raw);
    this.valorEditando.set(num);
  }

  confirmarEdicao(operadoraId: string): void {
    const valor = this.valorEditando();
    if (valor === null || Number.isNaN(valor) || valor <= 0) {
      this._setErro(operadoraId, 'Valor deve ser maior que zero.');
      return;
    }

    this.salvando.set(true);
    this._catalogService
      .upsertValorPorProcedimento(this.procedimentoId, operadoraId, { valor })
      .subscribe({
        next: () => {
          this.salvando.set(false);
          this.editandoOperadoraId.set(null);
          this.valorEditando.set(null);
          this._limparErro(operadoraId);
          this._carregar();
        },
        error: () => {
          this.salvando.set(false);
          this._setErro(operadoraId, 'Não foi possível salvar o valor. Tente novamente.');
        },
      });
  }

  excluir(operadoraId: string): void {
    if (!window.confirm('Remover valor para essa operadora?')) {
      return;
    }
    this._catalogService.excluirValorPorProcedimento(this.procedimentoId, operadoraId).subscribe({
      next: () => {
        this._limparErro(operadoraId);
        this._carregar();
      },
      error: () => {
        this._setErro(operadoraId, 'Não foi possível remover o valor. Tente novamente.');
      },
    });
  }

  erro(operadoraId: string): string | null {
    return this.erroPorOperadora()[operadoraId] ?? null;
  }

  private _carregar(): void {
    this.loading.set(true);
    this._catalogService.listarValoresPorProcedimento(this.procedimentoId).subscribe({
      next: (linhas) => {
        const ordenadas = [...linhas].sort((a, b) =>
          a.operadoraNome.localeCompare(b.operadoraNome, 'pt-BR'),
        );
        this.valores.set(ordenadas);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  private _setErro(operadoraId: string, mensagem: string): void {
    this.erroPorOperadora.update((prev) => ({ ...prev, [operadoraId]: mensagem }));
  }

  private _limparErro(operadoraId: string): void {
    this.erroPorOperadora.update((prev) =>
      Object.fromEntries(Object.entries(prev).filter(([key]) => key !== operadoraId)),
    );
  }
}
