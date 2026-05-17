import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { DatePipe } from '@angular/common';
import { RecursoService } from '../recurso.service';
import { GuiaService } from '../guia.service';
import type { GuiaItem } from '../guia.types';
import type { GuiaNoRecursoDto, RecursoDto } from '../recurso.types';

@Component({
  selector: 'app-recurso-guias',
  template: `
    <div class="recurso-guias">
      <div class="recurso-guias__header">
        <div class="recurso-guias__header-info">
          <span class="recurso-guias__operadora">{{ recurso()?.operadoraNome }}</span>
          <span class="recurso-guias__prestador">{{ recurso()?.prestadorNome }}</span>
          <span class="recurso-guias__numero">{{ recurso()?.numero }}</span>
        </div>
        <button class="recurso-guias__btn-pdf" type="button" (click)="baixarPdf()">
          Baixar PDF
        </button>
      </div>

      <div class="recurso-guias__vinculadas">
        <h3 class="recurso-guias__secao-titulo">Guias vinculadas</h3>
        @for (guia of guias(); track guia.id) {
          <div class="recurso-guias__guia-row">
            <span class="recurso-guias__guia-senha">{{ guia.senha }}</span>
            <span class="recurso-guias__guia-data">{{
              guia.dataAtendimento | date: 'dd/MM/yyyy'
            }}</span>
            <span class="recurso-guias__guia-beneficiario">{{ guia.beneficiarioNome }}</span>
            <span class="recurso-guias__badge--situacao">{{ guia.situacao }}</span>
            <span class="recurso-guias__guia-itens">{{ guia.totalItens }} itens</span>
            <button class="recurso-guias__btn-remover" type="button" (click)="removerGuia(guia.id)">
              Remover
            </button>
          </div>
        } @empty {
          <p class="recurso-guias__vazio">Nenhuma guia vinculada.</p>
        }
      </div>

      <div class="recurso-guias__busca">
        <h3 class="recurso-guias__secao-titulo">Adicionar guias</h3>
        <input
          class="recurso-guias__busca-input"
          type="text"
          placeholder="Buscar por senha"
          [value]="buscaSenha()"
          (input)="onBuscaChange($any($event.target).value)"
        />

        @for (guia of guiasBusca(); track guia.id) {
          <div class="recurso-guias__resultado">
            <span class="recurso-guias__resultado-senha">{{ guia.senha }}</span>
            <span class="recurso-guias__resultado-data">{{
              guia.dataAtendimento | date: 'dd/MM/yyyy'
            }}</span>
            <span class="recurso-guias__resultado-beneficiario">{{ guia.beneficiarioNome }}</span>
            <span class="recurso-guias__resultado-itens">{{ guia.totalItens }} itens</span>
            <button
              class="recurso-guias__btn-adicionar"
              type="button"
              (click)="adicionarGuia(guia)"
            >
              Adicionar
            </button>
          </div>
        }
      </div>

      @if (erro()) {
        <p class="recurso-guias__erro">{{ erro() }}</p>
      }
    </div>
  `,
  styleUrl: './recurso-guias.component.scss',
  imports: [DatePipe],
})
export class RecursoGuiasComponent implements OnInit {
  private readonly _recursoService = inject(RecursoService);
  private readonly _guiaService = inject(GuiaService);
  private readonly _route = inject(ActivatedRoute);
  private _debounceTimer: ReturnType<typeof setTimeout> | null = null;

  readonly recursoId = signal<string | null>(null);
  readonly recurso = signal<RecursoDto | null>(null);
  readonly guias = signal<GuiaNoRecursoDto[]>([]);
  readonly buscaSenha = signal('');
  readonly guiasBusca = signal<GuiaItem[]>([]);
  readonly erro = signal('');

  ngOnInit(): void {
    const id = this._route.snapshot.paramMap.get('id');
    if (id) {
      this.recursoId.set(id);
      this._carregar(id);
    }
  }

  baixarPdf(): void {
    const id = this.recursoId();
    if (id) {
      this._recursoService.baixarPdf(id);
    }
  }

  onBuscaChange(valor: string): void {
    this.buscaSenha.set(valor);
    if (this._debounceTimer !== null) {
      clearTimeout(this._debounceTimer);
    }
    if (!valor.trim()) {
      this.guiasBusca.set([]);
      return;
    }
    this._debounceTimer = setTimeout(() => {
      this._buscarGuias(valor);
    }, 400);
  }

  adicionarGuia(guia: GuiaItem): void {
    const id = this.recursoId();
    if (!id) {
      return;
    }
    this.erro.set('');
    this._recursoService.adicionarGuia(id, guia.id).subscribe({
      next: () => {
        this.guiasBusca.update((prev) => prev.filter((g) => g.id !== guia.id));
        this._carregar(id);
      },
      error: () => {
        this.erro.set('Erro ao adicionar guia. Tente novamente.');
      },
    });
  }

  removerGuia(guiaId: string): void {
    const id = this.recursoId();
    if (!id) {
      return;
    }
    this.erro.set('');
    this._recursoService.removerGuia(id, guiaId).subscribe({
      next: () => {
        this.guias.update((prev) => prev.filter((g) => g.id !== guiaId));
      },
      error: () => {
        this.erro.set('Erro ao remover guia. Tente novamente.');
      },
    });
  }

  private _carregar(id: string): void {
    this._recursoService.obterPorId(id).subscribe({
      next: (detalhe) => {
        this.recurso.set(detalhe.header);
        this.guias.set(detalhe.guias);
      },
      error: () => {
        this.erro.set('Erro ao carregar recurso.');
      },
    });
  }

  private _buscarGuias(senha: string): void {
    this._guiaService.listar({ senha, pagina: 1, itensPorPagina: 20 }).subscribe({
      next: (result) => {
        this.guiasBusca.set(result.itens.filter((g) => g.situacao !== 'EmRecurso'));
      },
      error: () => {
        this.guiasBusca.set([]);
      },
    });
  }
}
