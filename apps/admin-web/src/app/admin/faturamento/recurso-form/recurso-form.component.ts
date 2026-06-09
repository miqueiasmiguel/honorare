import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import type { OperadoraItem, PrestadorItem } from '../../catalog/catalog.types';
import { CatalogService } from '../../catalog/catalog.service';
import { RecursoService } from '../recurso.service';

@Component({
  selector: 'app-recurso-form',
  template: `
    <form class="recurso-form" (submit)="onSubmit($event)">
      <div class="recurso-form__field">
        <label class="recurso-form__label" for="operadora-id">Operadora</label>
        <select
          id="operadora-id"
          class="recurso-form__select--operadora"
          [value]="operadoraId()"
          (change)="operadoraId.set($any($event.target).value)"
        >
          <option value="">Selecione uma operadora</option>
          @for (o of operadoras(); track o.id) {
            <option [value]="o.id">{{ o.nome }}</option>
          }
        </select>
      </div>

      <div class="recurso-form__field">
        <label class="recurso-form__label" for="prestador-id">Prestador</label>
        <select
          id="prestador-id"
          class="recurso-form__select--prestador"
          [value]="prestadorId()"
          (change)="prestadorId.set($any($event.target).value)"
        >
          <option value="">Selecione um prestador</option>
          @for (p of prestadores(); track p.id) {
            <option [value]="p.id">{{ p.nome }}</option>
          }
        </select>
      </div>

      <div class="recurso-form__field">
        <label class="recurso-form__label" for="data-emissao">Data de Emissão</label>
        <input
          type="date"
          id="data-emissao"
          class="recurso-form__input--data-emissao"
          [value]="dataEmissao()"
          (input)="onDataEmissaoInput($any($event.target).value)"
        />
      </div>

      <div class="recurso-form__field">
        <label class="recurso-form__label" for="numero">Número</label>
        <input
          type="text"
          inputmode="numeric"
          id="numero"
          class="recurso-form__input--numero"
          maxlength="20"
          [value]="numero()"
          (input)="onNumeroInput($event)"
        />
      </div>

      <div class="recurso-form__field">
        <label class="recurso-form__label" for="observacao">Observação</label>
        <textarea
          id="observacao"
          class="recurso-form__textarea--observacao"
          [value]="observacao()"
          (input)="observacao.set($any($event.target).value)"
        ></textarea>
      </div>

      @if (erroValidacao()) {
        <span class="recurso-form__erro-validacao">{{ erroValidacao() }}</span>
      }

      <div class="recurso-form__actions">
        <button type="button" class="recurso-form__btn-cancelar" (click)="cancelar()">
          Cancelar
        </button>
        <button type="submit" class="recurso-form__btn-salvar">
          {{ modoEditar() ? 'Salvar' : 'Criar Recurso' }}
        </button>
      </div>
    </form>
  `,
  styleUrl: './recurso-form.component.scss',
})
export class RecursoFormComponent implements OnInit {
  private readonly _recursoService = inject(RecursoService);
  private readonly _catalogService = inject(CatalogService);
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);

  readonly modoEditar = signal(false);
  readonly recursoId = signal<string | null>(null);
  readonly erroValidacao = signal('');

  readonly operadoras = signal<OperadoraItem[]>([]);
  readonly prestadores = signal<PrestadorItem[]>([]);

  readonly operadoraId = signal('');
  readonly prestadorId = signal('');
  readonly dataEmissao = signal('');
  readonly numero = signal('');
  readonly observacao = signal('');

  /** Marca se o operador editou o número manualmente; enquanto false, o número
   * acompanha a sugestão (mês anterior à data de emissão). */
  private _numeroEditado = false;

  ngOnInit(): void {
    this._carregarOperadoras();
    this._carregarPrestadores();

    const id = this._route.snapshot.paramMap.get('id');
    if (id) {
      this.modoEditar.set(true);
      this.recursoId.set(id);
      this._carregarRecurso(id);
    }
  }

  cancelar(): void {
    void this._router.navigate(['/admin/recursos']);
  }

  onDataEmissaoInput(value: string): void {
    this.dataEmissao.set(value);
    if (!this._numeroEditado) {
      this.numero.set(this._numeroSugerido(value));
    }
  }

  onNumeroInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const numeros = input.value.replace(/\D/g, '').slice(0, 20);
    this._numeroEditado = true;
    this.numero.set(numeros);
    // Reescreve o DOM mesmo quando o signal não muda (ex.: digitou uma letra
    // após dígitos válidos) — sem isso, o caractere inválido fica visível.
    input.value = numeros;
  }

  /** Sugestão de número: yyyyMM do mês anterior à data de emissão. */
  private _numeroSugerido(dataEmissao: string): string {
    const [ano, mes] = dataEmissao.split('-').map(Number);
    if (!ano || !mes) {
      return '';
    }
    let anoSugerido = ano;
    let mesSugerido = mes - 1;
    if (mesSugerido === 0) {
      mesSugerido = 12;
      anoSugerido -= 1;
    }
    return `${String(anoSugerido)}${String(mesSugerido).padStart(2, '0')}`;
  }

  onSubmit(event: Event): void {
    event.preventDefault();
    this.erroValidacao.set('');

    if (!this.operadoraId()) {
      this.erroValidacao.set('Selecione uma operadora.');
      return;
    }
    if (!this.prestadorId()) {
      this.erroValidacao.set('Selecione um prestador.');
      return;
    }
    if (!this.dataEmissao()) {
      this.erroValidacao.set('Informe a data de emissão.');
      return;
    }
    if (!this.numero()) {
      this.erroValidacao.set('Informe o número do recurso.');
      return;
    }

    const payload = {
      operadoraId: this.operadoraId(),
      prestadorId: this.prestadorId(),
      dataEmissao: this.dataEmissao(),
      numero: this.numero(),
      observacao: this.observacao() || null,
    };

    const editId = this.recursoId();
    if (this.modoEditar() && editId !== null) {
      this._recursoService.atualizar(editId, payload).subscribe({
        next: () => {
          void this._router.navigate(['/admin/recursos']);
        },
        error: () => {
          this.erroValidacao.set('Erro ao salvar. Tente novamente.');
        },
      });
    } else {
      this._recursoService.criar(payload).subscribe({
        next: (recurso) => {
          void this._router.navigate(['/admin/recursos', recurso.id, 'guias']);
        },
        error: () => {
          this.erroValidacao.set('Erro ao criar. Tente novamente.');
        },
      });
    }
  }

  private _carregarOperadoras(): void {
    this._catalogService
      .listarOperadoras({ ativa: true, pagina: 1, itensPorPagina: 200 })
      .subscribe({
        next: (result) => {
          this.operadoras.set(result.itens);
        },
      });
  }

  private _carregarPrestadores(): void {
    this._catalogService
      .listarPrestadores({ ativo: true, pagina: 1, itensPorPagina: 200 })
      .subscribe({
        next: (result) => {
          this.prestadores.set(result.itens);
        },
      });
  }

  private _carregarRecurso(id: string): void {
    this._recursoService.obterPorId(id).subscribe({
      next: (detalhe) => {
        const h = detalhe.header;
        this.operadoraId.set(h.operadoraId);
        this.prestadorId.set(h.prestadorId);
        this.dataEmissao.set(h.dataEmissao);
        this._numeroEditado = true;
        this.numero.set(h.numero);
        this.observacao.set(h.observacao ?? '');
      },
      error: () => {
        this.erroValidacao.set('Erro ao carregar recurso.');
      },
    });
  }
}
