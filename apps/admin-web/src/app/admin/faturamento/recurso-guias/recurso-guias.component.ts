import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { RecursoService } from '../recurso.service';
import { GuiaService } from '../guia.service';
import type { GuiaItem, SituacaoGuia } from '../guia.types';
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

      <section class="recurso-guias__secao">
        <h3 class="recurso-guias__secao-titulo">Guias vinculadas</h3>
        <table class="recurso-guias__tabela">
          <thead>
            <tr>
              <th>Senha</th>
              <th>Data</th>
              <th>Beneficiário</th>
              <th>Situação</th>
              <th>Itens</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (guia of guias(); track guia.id) {
              <tr class="recurso-guias__linha-guia">
                <td>{{ guia.senha }}</td>
                <td>{{ formatarData(guia.dataAtendimento) }}</td>
                <td>{{ guia.beneficiarioNome }}</td>
                <td>{{ guia.situacao }}</td>
                <td>{{ guia.itens.length }}</td>
                <td>
                  <button
                    class="recurso-guias__btn-remover"
                    type="button"
                    (click)="removerGuia(guia.id)"
                  >
                    Remover
                  </button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="6">
                  <p class="recurso-guias__vazio">Nenhuma guia vinculada.</p>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </section>

      <section class="recurso-guias__secao">
        <h3 class="recurso-guias__secao-titulo">Adicionar guias</h3>
        <div class="recurso-guias__filtros">
          <input
            class="recurso-guias__filtro-input"
            type="text"
            placeholder="Senha"
            [value]="filtroSenha()"
            (input)="filtroSenha.set($any($event.target).value)"
          />
          <input
            class="recurso-guias__filtro-input"
            type="text"
            placeholder="Beneficiário"
            [value]="filtroBeneficiario()"
            (input)="filtroBeneficiario.set($any($event.target).value)"
          />
          <input
            class="recurso-guias__filtro-input"
            type="date"
            [value]="filtroDataInicio()"
            (input)="filtroDataInicio.set($any($event.target).value)"
          />
          <input
            class="recurso-guias__filtro-input"
            type="date"
            [value]="filtroDataFim()"
            (input)="filtroDataFim.set($any($event.target).value)"
          />
          <select
            class="recurso-guias__filtro-select"
            (change)="filtroSituacao.set($any($event.target).value)"
          >
            <option value="" [selected]="!filtroSituacao()">Todas</option>
            <option value="Apresentada" [selected]="filtroSituacao() === 'Apresentada'">
              Apresentada
            </option>
            <option value="Liquidada" [selected]="filtroSituacao() === 'Liquidada'">
              Liquidada
            </option>
          </select>
          <label class="recurso-guias__filtro-toggle">
            <input
              type="checkbox"
              [checked]="filtroSomenteGlosa()"
              (change)="filtroSomenteGlosa.set($any($event.target).checked)"
            />
            Só com glosa
          </label>
          <button class="recurso-guias__btn-filtrar" type="button" (click)="filtrar()">
            Filtrar
          </button>
        </div>

        @if (!filtroAplicado()) {
          <p class="recurso-guias__hint">Aplique filtros para buscar guias disponíveis.</p>
        }

        @if (filtroAplicado() && candidatas().length > 0) {
          <div class="recurso-guias__acoes-candidatas">
            <button
              class="recurso-guias__btn-adicionar-todas"
              type="button"
              (click)="adicionarTodas()"
            >
              Adicionar todas ({{ totalCandidatas() }})
            </button>
          </div>
        }

        @if (filtroAplicado()) {
          <table class="recurso-guias__tabela-candidatas">
            <thead>
              <tr>
                <th>Senha</th>
                <th>Data</th>
                <th>Beneficiário</th>
                <th>Situação</th>
                <th>Itens</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (candidata of candidatas(); track candidata.id) {
                <tr class="recurso-guias__linha-candidata">
                  <td>{{ candidata.senha }}</td>
                  <td>{{ formatarData(candidata.dataAtendimento) }}</td>
                  <td>{{ candidata.beneficiarioNome }}</td>
                  <td>{{ candidata.situacao }}</td>
                  <td>{{ candidata.totalItens }}</td>
                  <td>
                    <button
                      class="recurso-guias__btn-adicionar"
                      type="button"
                      (click)="adicionarGuia(candidata)"
                    >
                      Adicionar
                    </button>
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="6">
                    <p class="recurso-guias__vazio">Nenhuma guia encontrada.</p>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </section>

      @if (erro()) {
        <p class="recurso-guias__erro">{{ erro() }}</p>
      }
    </div>
  `,
  styleUrl: './recurso-guias.component.scss',
})
export class RecursoGuiasComponent implements OnInit {
  private readonly _recursoService = inject(RecursoService);
  private readonly _guiaService = inject(GuiaService);
  private readonly _route = inject(ActivatedRoute);

  readonly recursoId = signal<string | null>(null);
  readonly recurso = signal<RecursoDto | null>(null);
  readonly guias = signal<GuiaNoRecursoDto[]>([]);

  readonly prestadorId = signal('');
  readonly operadoraId = signal('');

  readonly filtroSenha = signal('');
  readonly filtroBeneficiario = signal('');
  readonly filtroDataInicio = signal('');
  readonly filtroDataFim = signal('');
  readonly filtroSituacao = signal<SituacaoGuia | ''>('');
  readonly filtroSomenteGlosa = signal(false);

  readonly candidatas = signal<GuiaItem[]>([]);
  readonly totalCandidatas = signal(0);
  readonly carregandoCandidatas = signal(false);
  readonly filtroAplicado = signal(false);
  readonly erro = signal('');

  ngOnInit(): void {
    const id = this._route.snapshot.paramMap.get('id');
    if (id) {
      this.recursoId.set(id);
      this._carregar(id);
    }
  }

  filtrar(): void {
    const id = this.recursoId();
    if (!id) {
      return;
    }
    this.carregandoCandidatas.set(true);
    this.erro.set('');
    const situacao = this.filtroSituacao();
    this._guiaService
      .listar({
        prestadorId: this.prestadorId(),
        operadoraId: this.operadoraId(),
        semRecurso: true,
        senha: this.filtroSenha() || undefined,
        beneficiario: this.filtroBeneficiario() || undefined,
        dataInicio: this.filtroDataInicio() || undefined,
        dataFim: this.filtroDataFim() || undefined,
        situacao: situacao !== '' ? situacao : undefined,
        somenteComGlosa: this.filtroSomenteGlosa() || undefined,
        pagina: 1,
        itensPorPagina: 50,
      })
      .subscribe({
        next: (result) => {
          this.candidatas.set(result.itens);
          this.totalCandidatas.set(result.total);
          this.filtroAplicado.set(true);
          this.carregandoCandidatas.set(false);
        },
        error: () => {
          this.erro.set('Erro ao buscar guias.');
          this.carregandoCandidatas.set(false);
        },
      });
  }

  adicionarTodas(): void {
    const id = this.recursoId();
    if (!id) {
      return;
    }
    this.erro.set('');
    const situacao = this.filtroSituacao();
    this._recursoService
      .adicionarGuiasLote(id, {
        prestadorId: this.prestadorId(),
        operadoraId: this.operadoraId(),
        dataInicio: this.filtroDataInicio() || undefined,
        dataFim: this.filtroDataFim() || undefined,
        situacao: situacao !== '' ? situacao : undefined,
        senha: this.filtroSenha() || undefined,
        beneficiario: this.filtroBeneficiario() || undefined,
        somenteComGlosa: this.filtroSomenteGlosa() || undefined,
      })
      .subscribe({
        next: () => {
          this._carregar(id);
          this.filtrar();
        },
        error: () => {
          this.erro.set('Erro ao adicionar guias em lote.');
        },
      });
  }

  adicionarGuia(guia: GuiaItem): void {
    const id = this.recursoId();
    if (!id) {
      return;
    }
    this.erro.set('');
    this._recursoService.adicionarGuia(id, guia.id).subscribe({
      next: () => {
        this.candidatas.update((prev) => prev.filter((g) => g.id !== guia.id));
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

  baixarPdf(): void {
    const id = this.recursoId();
    if (id) {
      this._recursoService.baixarPdf(id);
    }
  }

  formatarData(iso: string): string {
    return new Intl.DateTimeFormat('pt-BR').format(new Date(`${iso}T00:00:00`));
  }

  private _carregar(id: string): void {
    this._recursoService.obterPorId(id).subscribe({
      next: (detalhe) => {
        this.recurso.set(detalhe.header);
        this.guias.set(detalhe.guias);
        this.prestadorId.set(detalhe.header.prestadorId);
        this.operadoraId.set(detalhe.header.operadoraId);
      },
      error: () => {
        this.erro.set('Erro ao carregar recurso.');
      },
    });
  }
}
