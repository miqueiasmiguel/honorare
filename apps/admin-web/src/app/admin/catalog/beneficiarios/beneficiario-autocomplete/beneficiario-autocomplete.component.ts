import { Component, DestroyRef, inject, input, OnInit, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, Subject } from 'rxjs';
import { CatalogService } from '../../catalog.service';
import type { BeneficiarioItem } from '../../catalog.types';

type EstadoBusca = 'idle' | 'buscando' | 'encontrado' | 'novo' | 'erro';

@Component({
  selector: 'app-beneficiario-autocomplete',
  templateUrl: './beneficiario-autocomplete.component.html',
  styleUrl: './beneficiario-autocomplete.component.scss',
})
export class BeneficiarioAutocompleteComponent implements OnInit {
  private readonly _catalogService = inject(CatalogService);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _carteira$ = new Subject<string>();

  readonly label = input<string>('Carteira do Beneficiário');
  readonly disabled = input<boolean>(false);
  readonly beneficiarioChange = output<BeneficiarioItem | null>();

  readonly carteira = signal('');
  readonly nomeSelecionado = signal('');
  readonly estado = signal<EstadoBusca>('idle');
  readonly beneficiarioAtual = signal<BeneficiarioItem | null>(null);

  readonly nomeInput = signal('');
  readonly erroNome = signal('');

  ngOnInit(): void {
    this._carteira$
      .pipe(debounceTime(400), takeUntilDestroyed(this._destroyRef))
      .subscribe((carteira) => {
        if (!carteira.trim()) {
          this._limpar();
          return;
        }
        this._buscarExistente(carteira.trim());
      });
  }

  onCarteiraChange(value: string): void {
    this.carteira.set(value);
    this._carteira$.next(value);
  }

  onNomeInputChange(value: string): void {
    this.nomeInput.set(value);
    this.erroNome.set('');
  }

  confirmarNovo(): void {
    const nome = this.nomeInput().trim();
    if (!nome) {
      this.erroNome.set('Nome é obrigatório');
      return;
    }
    this.estado.set('buscando');
    this._catalogService.lookupOrCreateBeneficiario(this.carteira(), nome).subscribe({
      next: (result) => {
        const item: BeneficiarioItem = {
          id: result.id,
          carteira: result.carteira,
          nome: result.nome,
          criadoEm: result.criadoEm,
        };
        this.beneficiarioAtual.set(item);
        this.nomeSelecionado.set(item.nome);
        this.estado.set(result.criado ? 'novo' : 'encontrado');
        this.beneficiarioChange.emit(item);
      },
      error: () => {
        this.estado.set('erro');
      },
    });
  }

  private _buscarExistente(carteira: string): void {
    this.estado.set('buscando');
    this._catalogService.listarBeneficiarios({ carteira, pagina: 1, itensPorPagina: 1 }).subscribe({
      next: (result) => {
        if (result.itens.length > 0) {
          const item = result.itens[0];
          this.beneficiarioAtual.set(item);
          this.nomeSelecionado.set(item.nome);
          this.estado.set('encontrado');
          this.beneficiarioChange.emit(item);
        } else {
          this.beneficiarioAtual.set(null);
          this.nomeSelecionado.set('');
          this.nomeInput.set('');
          this.erroNome.set('');
          this.estado.set('novo');
          this.beneficiarioChange.emit(null);
        }
      },
      error: () => {
        this.estado.set('erro');
      },
    });
  }

  private _limpar(): void {
    this.carteira.set('');
    this.nomeSelecionado.set('');
    this.nomeInput.set('');
    this.erroNome.set('');
    this.estado.set('idle');
    this.beneficiarioAtual.set(null);
    this.beneficiarioChange.emit(null);
  }
}
