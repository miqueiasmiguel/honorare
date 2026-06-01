import { Component, inject, Input, OnInit, signal } from '@angular/core';
import { CatalogService } from '../../catalog.service';
import type { TabelaPorteAnestesicoItem } from '../../catalog.types';

@Component({
  selector: 'app-portes-anestesicos',
  templateUrl: './portes-anestesicos.component.html',
  styleUrl: './portes-anestesicos.component.scss',
})
export class PortesAnestesicosComponent implements OnInit {
  @Input({ required: true }) operadoraId!: string;

  private readonly _catalogService = inject(CatalogService);

  readonly portes = signal<TabelaPorteAnestesicoItem[]>([]);
  readonly loading = signal(false);

  ngOnInit(): void {
    this._carregar();
  }

  formatarBrl(valor: number | null): string {
    if (valor === null) {
      return '—';
    }
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(valor);
  }

  excluir(id: string): void {
    if (!window.confirm('Remover porte anestésico?')) {
      return;
    }
    this._catalogService.excluirPorteAnestesico(id).subscribe({
      next: () => {
        this._carregar();
      },
      error: () => {
        /* mantém lista atual em caso de erro */
      },
    });
  }

  private _carregar(): void {
    this.loading.set(true);
    this._catalogService.listarPortesAnestesico(this.operadoraId).subscribe({
      next: (items) => {
        const ordenados = [...items].sort((a, b) => a.porteLetra.localeCompare(b.porteLetra));
        this.portes.set(ordenados);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }
}
