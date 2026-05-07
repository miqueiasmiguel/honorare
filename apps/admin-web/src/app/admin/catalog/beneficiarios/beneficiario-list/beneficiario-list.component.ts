import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { debounceTime, Subject } from 'rxjs';
import { CatalogService } from '../../catalog.service';
import type { BeneficiarioItem } from '../../catalog.types';

@Component({
  selector: 'app-beneficiario-list',
  templateUrl: './beneficiario-list.component.html',
  styleUrl: './beneficiario-list.component.scss',
  imports: [DatePipe],
})
export class BeneficiarioListComponent implements OnInit {
  private readonly _catalogService = inject(CatalogService);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _filtro$ = new Subject<void>();

  readonly beneficiarios = signal<BeneficiarioItem[]>([]);
  readonly loading = signal(false);
  readonly total = signal(0);
  readonly pagina = signal(1);
  readonly itensPorPagina = signal(20);
  readonly filtroCarteira = signal('');
  readonly filtroNome = signal('');
  readonly editandoId = signal<string | null>(null);
  readonly editandoNome = signal('');
  readonly erroExclusao = signal('');
  readonly erroEdicao = signal('');

  ngOnInit(): void {
    this._filtro$.pipe(debounceTime(400), takeUntilDestroyed(this._destroyRef)).subscribe(() => {
      this.pagina.set(1);
      this._carregar();
    });
    this._carregar();
  }

  onFiltroCarteiraChange(value: string): void {
    this.filtroCarteira.set(value);
    this._filtro$.next();
  }

  onFiltroNomeChange(value: string): void {
    this.filtroNome.set(value);
    this._filtro$.next();
  }

  iniciarEdicao(b: BeneficiarioItem): void {
    this.editandoId.set(b.id);
    this.editandoNome.set(b.nome);
    this.erroEdicao.set('');
  }

  cancelarEdicao(): void {
    this.editandoId.set(null);
    this.editandoNome.set('');
    this.erroEdicao.set('');
  }

  salvarEdicao(id: string): void {
    this.erroEdicao.set('');
    this._catalogService.atualizarBeneficiario(id, { nome: this.editandoNome() }).subscribe({
      next: () => {
        this.editandoId.set(null);
        this.editandoNome.set('');
        this._carregar();
      },
      error: (err: unknown) => {
        if (err instanceof HttpErrorResponse && err.status === 400) {
          this.erroEdicao.set('Nome inválido. Verifique e tente novamente.');
        } else {
          this.erroEdicao.set('Erro ao salvar. Tente novamente.');
        }
      },
    });
  }

  excluir(b: BeneficiarioItem): void {
    if (!window.confirm(`Excluir beneficiário "${b.nome}"?`)) {
      return;
    }
    this.erroExclusao.set('');
    this._catalogService.excluirBeneficiario(b.id).subscribe({
      next: () => {
        this._carregar();
      },
      error: (err: unknown) => {
        if (err instanceof HttpErrorResponse && err.status === 409) {
          this.erroExclusao.set('Beneficiário possui guias associadas e não pode ser excluído.');
        } else {
          this.erroExclusao.set('Erro ao excluir. Tente novamente.');
        }
      },
    });
  }

  proximaPagina(): void {
    if (this.pagina() * this.itensPorPagina() < this.total()) {
      this.pagina.set(this.pagina() + 1);
      this._carregar();
    }
  }

  paginaAnterior(): void {
    if (this.pagina() > 1) {
      this.pagina.set(this.pagina() - 1);
      this._carregar();
    }
  }

  private _carregar(): void {
    this.loading.set(true);
    this._catalogService
      .listarBeneficiarios({
        carteira: this.filtroCarteira() || undefined,
        nome: this.filtroNome() || undefined,
        pagina: this.pagina(),
        itensPorPagina: this.itensPorPagina(),
      })
      .subscribe({
        next: (result) => {
          this.beneficiarios.set(result.itens);
          this.total.set(result.total);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }
}
