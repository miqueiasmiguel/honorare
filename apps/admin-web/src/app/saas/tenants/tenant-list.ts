import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { SaasService } from '../saas.service';
import type { TenantStatus, TenantSummary } from '../saas.types';

@Component({
  selector: 'app-tenant-list',
  imports: [RouterLink, ReactiveFormsModule],
  template: `
    <div class="tenant-list">
      <div class="tenant-list__cards">
        <div class="tenant-list__card" data-testid="card-ativos">
          <div class="tenant-list__card-label">Tenants Ativos</div>
          <div class="tenant-list__card-value">{{ totalAtivos() }}</div>
        </div>
        <div class="tenant-list__card" data-testid="card-suspensos">
          <div class="tenant-list__card-label">Tenants Suspensos</div>
          <div class="tenant-list__card-value">{{ totalSuspensos() }}</div>
        </div>
        <div class="tenant-list__card" data-testid="card-medicos">
          <div class="tenant-list__card-label">Total de Médicos</div>
          <div class="tenant-list__card-value">{{ totalMedicos() }}</div>
        </div>
      </div>

      <div class="tenant-list__toolbar">
        <h2 class="tenant-list__title">Tenants</h2>
        <button class="tenant-list__btn-novo" type="button" (click)="openModal()">
          Novo Tenant
        </button>
      </div>

      <table class="tenant-list__table">
        <thead>
          <tr>
            <th>Nome</th>
            <th>Status</th>
            <th>Admins</th>
            <th>Médicos</th>
            <th>Criado em</th>
            <th>Ações</th>
          </tr>
        </thead>
        <tbody>
          @for (tenant of tenants(); track tenant.id) {
            <tr class="tenant-list__row">
              <td>{{ tenant.name }}</td>
              <td>
                <span [class]="badgeClass(tenant.status)">{{ tenant.status }}</span>
              </td>
              <td>{{ tenant.totalAdmins }}</td>
              <td>{{ tenant.totalMedicos }}</td>
              <td>{{ formatDate(tenant.createdAt) }}</td>
              <td>
                <button
                  class="tenant-list__btn-ver"
                  type="button"
                  [routerLink]="['/saas/tenants', tenant.id]"
                >
                  Ver
                </button>
              </td>
            </tr>
          }
        </tbody>
      </table>

      @if (showModal()) {
        <div
          class="tenant-list__overlay"
          role="dialog"
          aria-modal="true"
          aria-labelledby="modal-title"
        >
          <div class="tenant-list__modal">
            <h3 id="modal-title" class="tenant-list__modal-title">Novo Tenant</h3>
            <form [formGroup]="form" (ngSubmit)="submitCreate()">
              <div class="tenant-list__field">
                <label for="tenantName" class="tenant-list__label">Nome do tenant</label>
                <input
                  id="tenantName"
                  class="tenant-list__input"
                  formControlName="tenantName"
                  placeholder="Nome do tenant"
                />
                @if (form.controls.tenantName.invalid && form.controls.tenantName.touched) {
                  <span class="tenant-list__error">Nome é obrigatório</span>
                }
              </div>
              <div class="tenant-list__field">
                <label for="ownerEmail" class="tenant-list__label">E-mail do owner</label>
                <input
                  id="ownerEmail"
                  class="tenant-list__input"
                  formControlName="ownerEmail"
                  type="email"
                  placeholder="E-mail do owner"
                />
                @if (form.controls.ownerEmail.invalid && form.controls.ownerEmail.touched) {
                  <span class="tenant-list__error tenant-list__error--email">E-mail inválido</span>
                }
              </div>
              <div class="tenant-list__modal-footer">
                <button type="button" class="tenant-list__btn-cancel" (click)="closeModal()">
                  Cancelar
                </button>
                <button type="submit" class="tenant-list__btn-criar" [disabled]="creating()">
                  Criar
                </button>
              </div>
            </form>
          </div>
        </div>
      }
    </div>
  `,
  styles: [
    `
      .tenant-list {
        padding: 1.5rem;
      }

      .tenant-list__cards {
        display: flex;
        gap: 1rem;
        margin-bottom: 1.5rem;
      }

      .tenant-list__card {
        flex: 1;
        padding: 1rem;
        background: #fff;
        border: 1px solid #e0e0e0;
        border-radius: 8px;
      }

      .tenant-list__card-label {
        font-size: 0.85rem;
        color: #666;
        margin-bottom: 0.25rem;
      }

      .tenant-list__card-value {
        font-size: 1.75rem;
        font-weight: 700;
        color: #1a1a2e;
      }

      .tenant-list__toolbar {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-bottom: 1rem;
      }

      .tenant-list__title {
        font-size: 1.25rem;
        font-weight: 600;
        margin: 0;
      }

      .tenant-list__btn-novo {
        padding: 0.5rem 1rem;
        background: #1a1a2e;
        color: #fff;
        border: none;
        border-radius: 4px;
        cursor: pointer;
      }

      .tenant-list__table {
        width: 100%;
        border-collapse: collapse;
      }

      .tenant-list__table th {
        padding: 0.75rem 1rem;
        text-align: left;
        border-bottom: 2px solid #e0e0e0;
        font-weight: 600;
      }

      .tenant-list__table td {
        padding: 0.75rem 1rem;
        border-bottom: 1px solid #e0e0e0;
      }

      .tenant-list__btn-ver {
        padding: 0.25rem 0.75rem;
        border: 1px solid #1a1a2e;
        background: transparent;
        border-radius: 4px;
        cursor: pointer;
      }

      .badge {
        display: inline-block;
        padding: 0.25rem 0.6rem;
        border-radius: 12px;
        font-size: 0.8rem;
        font-weight: 500;
      }

      .badge--ativo {
        background: #d4edda;
        color: #155724;
      }

      .badge--suspenso {
        background: #fff3cd;
        color: #856404;
      }

      .badge--cancelado {
        background: #f8d7da;
        color: #721c24;
      }

      .tenant-list__overlay {
        position: fixed;
        inset: 0;
        background: rgba(0, 0, 0, 0.5);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 1000;
      }

      .tenant-list__modal {
        background: #fff;
        border-radius: 8px;
        padding: 1.5rem;
        width: 400px;
        max-width: 90%;
      }

      .tenant-list__modal-title {
        margin: 0 0 1rem;
        font-size: 1.1rem;
      }

      .tenant-list__field {
        margin-bottom: 1rem;
      }

      .tenant-list__label {
        display: block;
        margin-bottom: 0.25rem;
        font-size: 0.9rem;
        font-weight: 500;
      }

      .tenant-list__input {
        width: 100%;
        padding: 0.5rem;
        border: 1px solid #ccc;
        border-radius: 4px;
        box-sizing: border-box;
      }

      .tenant-list__error {
        display: block;
        color: #721c24;
        font-size: 0.8rem;
        margin-top: 0.25rem;
      }

      .tenant-list__modal-footer {
        display: flex;
        justify-content: flex-end;
        gap: 0.75rem;
        margin-top: 1.5rem;
      }

      .tenant-list__btn-cancel {
        padding: 0.5rem 1rem;
        border: 1px solid #ccc;
        background: transparent;
        border-radius: 4px;
        cursor: pointer;
      }

      .tenant-list__btn-criar {
        padding: 0.5rem 1rem;
        background: #1a1a2e;
        color: #fff;
        border: none;
        border-radius: 4px;
        cursor: pointer;
      }

      .tenant-list__btn-criar:disabled {
        opacity: 0.6;
        cursor: not-allowed;
      }
    `,
  ],
})
export class TenantList implements OnInit {
  private readonly saasService = inject(SaasService);

  readonly tenants = signal<TenantSummary[]>([]);
  readonly showModal = signal(false);
  readonly creating = signal(false);

  readonly totalAtivos = computed(() => this.tenants().filter((t) => t.status === 'Ativo').length);
  readonly totalSuspensos = computed(
    () => this.tenants().filter((t) => t.status === 'Suspenso').length,
  );
  readonly totalMedicos = computed(() =>
    this.tenants().reduce((sum, t) => sum + t.totalMedicos, 0),
  );

  readonly form = new FormGroup({
    tenantName: new FormControl('', {
      nonNullable: true,
      validators: [(ctrl) => Validators.required(ctrl)],
    }),
    ownerEmail: new FormControl('', {
      nonNullable: true,
      validators: [(ctrl) => Validators.required(ctrl), (ctrl) => Validators.email(ctrl)],
    }),
  });

  ngOnInit(): void {
    this.loadTenants();
  }

  openModal(): void {
    this.form.reset();
    this.showModal.set(true);
  }

  closeModal(): void {
    this.showModal.set(false);
  }

  submitCreate(): void {
    if (this.form.invalid || this.creating()) {
      return;
    }
    const { tenantName, ownerEmail } = this.form.getRawValue();
    this.creating.set(true);
    this.saasService.createTenant({ tenantName, ownerEmail }).subscribe({
      next: () => {
        this.showModal.set(false);
        this.form.reset();
        this.creating.set(false);
        this.loadTenants();
      },
      error: () => {
        this.creating.set(false);
      },
    });
  }

  badgeClass(status: TenantStatus): string {
    const map: Record<TenantStatus, string> = {
      Ativo: 'badge badge--ativo',
      Suspenso: 'badge badge--suspenso',
      Cancelado: 'badge badge--cancelado',
    };
    return map[status];
  }

  formatDate(isoDate: string): string {
    const d = new Date(isoDate);
    const day = String(d.getUTCDate()).padStart(2, '0');
    const month = String(d.getUTCMonth() + 1).padStart(2, '0');
    const year = String(d.getUTCFullYear());
    return `${day}/${month}/${year}`;
  }

  private loadTenants(): void {
    this.saasService.listTenants().subscribe({
      next: (t) => {
        this.tenants.set(t);
      },
      error: () => undefined,
    });
  }
}
