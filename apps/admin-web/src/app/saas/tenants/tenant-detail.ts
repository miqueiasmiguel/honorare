import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { SaasService } from '../saas.service';
import type {
  CreateUserPayload,
  TenantStatus,
  TenantSummary,
  UserRole,
  UserSummary,
} from '../saas.types';

@Component({
  selector: 'app-tenant-detail',
  imports: [RouterLink, ReactiveFormsModule],
  template: `
    <div class="tenant-detail">
      <nav class="tenant-detail__breadcrumb">
        <a routerLink="/saas/tenants" class="tenant-detail__breadcrumb-link">Tenants</a>
        <span class="tenant-detail__breadcrumb-sep"> / </span>
        <span class="tenant-detail__breadcrumb-current">{{ tenant()?.name ?? '' }}</span>
      </nav>

      @if (tenant(); as t) {
        <div class="tenant-detail__header">
          <div class="tenant-detail__header-left">
            <h2 class="tenant-detail__name">{{ t.name }}</h2>
            <span [class]="badgeClass(t.status)">{{ t.status }}</span>
          </div>
          <div class="tenant-detail__header-actions">
            @if (t.status === 'Ativo') {
              <button
                class="tenant-detail__btn tenant-detail__btn--warn"
                type="button"
                data-testid="btn-suspender"
                (click)="changeStatus('Suspenso')"
              >
                Suspender
              </button>
              <button
                class="tenant-detail__btn tenant-detail__btn--danger"
                type="button"
                data-testid="btn-cancelar"
                (click)="changeStatus('Cancelado')"
              >
                Cancelar
              </button>
            }
            @if (t.status === 'Suspenso') {
              <button
                class="tenant-detail__btn tenant-detail__btn--success"
                type="button"
                data-testid="btn-reativar"
                (click)="changeStatus('Ativo')"
              >
                Reativar
              </button>
              <button
                class="tenant-detail__btn tenant-detail__btn--danger"
                type="button"
                data-testid="btn-cancelar"
                (click)="changeStatus('Cancelado')"
              >
                Cancelar
              </button>
            }
          </div>
        </div>
      }

      <div class="tenant-detail__section">
        <div class="tenant-detail__section-header">
          <h3 class="tenant-detail__section-title">Usuários</h3>
          <button class="tenant-detail__btn" type="button" (click)="openModal()">
            Adicionar usuário
          </button>
        </div>

        <table class="tenant-detail__table">
          <thead>
            <tr>
              <th>E-mail</th>
              <th>Role</th>
              <th>Status</th>
              <th>Criado em</th>
              <th>Ações</th>
            </tr>
          </thead>
          <tbody>
            @for (user of users(); track user.id) {
              <tr class="tenant-detail__user-row">
                <td>{{ user.email }}</td>
                <td>
                  <span [class]="roleBadgeClass(user.role)">{{ user.role }}</span>
                </td>
                <td>{{ user.isActive ? 'Ativo' : 'Inativo' }}</td>
                <td>{{ formatDate(user.createdAt) }}</td>
                <td>
                  <button class="tenant-detail__toggle" type="button" (click)="toggleUser(user)">
                    {{ user.isActive ? 'Desativar' : 'Ativar' }}
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      @if (showModal()) {
        <div
          class="tenant-detail__overlay"
          role="dialog"
          aria-modal="true"
          aria-labelledby="add-user-title"
        >
          <div class="tenant-detail__modal">
            <h3 id="add-user-title" class="tenant-detail__modal-title">Adicionar usuário</h3>
            <form [formGroup]="form" (ngSubmit)="submitAddUser()">
              <div class="tenant-detail__field">
                <label for="email" class="tenant-detail__label">E-mail</label>
                <input
                  id="email"
                  class="tenant-detail__input"
                  formControlName="email"
                  type="email"
                  placeholder="E-mail do usuário"
                />
                @if (form.controls.email.invalid && form.controls.email.touched) {
                  <span class="tenant-detail__error">E-mail inválido</span>
                }
              </div>
              <div class="tenant-detail__field">
                <label for="role" class="tenant-detail__label">Role</label>
                <select id="role" class="tenant-detail__input" formControlName="role">
                  <option value="TenantAdmin">Admin</option>
                  <option value="Medico">Médico</option>
                </select>
              </div>
              @if (showMedicoId()) {
                <div class="tenant-detail__field">
                  <label for="medicoId" class="tenant-detail__label">ID do Médico (UUID)</label>
                  <input
                    id="medicoId"
                    class="tenant-detail__input"
                    formControlName="medicoId"
                    placeholder="UUID do médico"
                  />
                  @if (form.controls.medicoId.invalid && form.controls.medicoId.touched) {
                    <span class="tenant-detail__error">ID do médico é obrigatório</span>
                  }
                </div>
              }
              <div class="tenant-detail__modal-footer">
                <button type="button" class="tenant-detail__btn-cancel" (click)="closeModal()">
                  Cancelar
                </button>
                <button type="submit" class="tenant-detail__btn" [disabled]="submitting()">
                  Adicionar
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
      .tenant-detail {
        padding: 1.5rem;
      }

      .tenant-detail__breadcrumb {
        margin-bottom: 1rem;
        font-size: 0.9rem;
      }

      .tenant-detail__breadcrumb-link {
        color: #1a1a2e;
        text-decoration: none;
      }

      .tenant-detail__breadcrumb-link:hover {
        text-decoration: underline;
      }

      .tenant-detail__breadcrumb-sep {
        color: #999;
        margin: 0 0.25rem;
      }

      .tenant-detail__breadcrumb-current {
        color: #555;
      }

      .tenant-detail__header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-bottom: 1.5rem;
        padding: 1rem;
        background: #fff;
        border: 1px solid #e0e0e0;
        border-radius: 8px;
      }

      .tenant-detail__header-left {
        display: flex;
        align-items: center;
        gap: 0.75rem;
      }

      .tenant-detail__header-actions {
        display: flex;
        gap: 0.5rem;
      }

      .tenant-detail__name {
        margin: 0;
        font-size: 1.25rem;
        font-weight: 600;
      }

      .tenant-detail__section {
        background: #fff;
        border: 1px solid #e0e0e0;
        border-radius: 8px;
        padding: 1rem;
      }

      .tenant-detail__section-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-bottom: 1rem;
      }

      .tenant-detail__section-title {
        margin: 0;
        font-size: 1rem;
        font-weight: 600;
      }

      .tenant-detail__table {
        width: 100%;
        border-collapse: collapse;
      }

      .tenant-detail__table th {
        padding: 0.75rem 1rem;
        text-align: left;
        border-bottom: 2px solid #e0e0e0;
        font-weight: 600;
      }

      .tenant-detail__table td {
        padding: 0.75rem 1rem;
        border-bottom: 1px solid #e0e0e0;
      }

      .tenant-detail__btn {
        padding: 0.4rem 0.9rem;
        background: #1a1a2e;
        color: #fff;
        border: none;
        border-radius: 4px;
        cursor: pointer;
        font-size: 0.875rem;
      }

      .tenant-detail__btn:disabled {
        opacity: 0.6;
        cursor: not-allowed;
      }

      .tenant-detail__btn--warn {
        background: #856404;
      }

      .tenant-detail__btn--danger {
        background: #721c24;
      }

      .tenant-detail__btn--success {
        background: #155724;
      }

      .tenant-detail__toggle {
        padding: 0.25rem 0.6rem;
        border: 1px solid #ccc;
        background: transparent;
        border-radius: 4px;
        cursor: pointer;
        font-size: 0.8rem;
      }

      .tenant-detail__overlay {
        position: fixed;
        inset: 0;
        background: rgba(0, 0, 0, 0.5);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 1000;
      }

      .tenant-detail__modal {
        background: #fff;
        border-radius: 8px;
        padding: 1.5rem;
        width: 420px;
        max-width: 90%;
      }

      .tenant-detail__modal-title {
        margin: 0 0 1rem;
        font-size: 1.1rem;
      }

      .tenant-detail__field {
        margin-bottom: 1rem;
      }

      .tenant-detail__label {
        display: block;
        margin-bottom: 0.25rem;
        font-size: 0.9rem;
        font-weight: 500;
      }

      .tenant-detail__input {
        width: 100%;
        padding: 0.5rem;
        border: 1px solid #ccc;
        border-radius: 4px;
        box-sizing: border-box;
        font-size: 0.9rem;
      }

      .tenant-detail__error {
        display: block;
        color: #721c24;
        font-size: 0.8rem;
        margin-top: 0.25rem;
      }

      .tenant-detail__modal-footer {
        display: flex;
        justify-content: flex-end;
        gap: 0.75rem;
        margin-top: 1.5rem;
      }

      .tenant-detail__btn-cancel {
        padding: 0.4rem 0.9rem;
        border: 1px solid #ccc;
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

      .badge--admin {
        background: #cce5ff;
        color: #004085;
      }

      .badge--medico {
        background: #e2d9f3;
        color: #4b0082;
      }
    `,
  ],
})
export class TenantDetail implements OnInit {
  private readonly saasService = inject(SaasService);
  private readonly route = inject(ActivatedRoute);

  readonly tenantId = signal('');
  readonly tenant = signal<TenantSummary | null>(null);
  readonly users = signal<UserSummary[]>([]);
  readonly showModal = signal(false);
  readonly submitting = signal(false);

  readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [(c) => Validators.required(c), (c) => Validators.email(c)],
    }),
    role: new FormControl<UserRole>('TenantAdmin', {
      nonNullable: true,
      validators: [(c) => Validators.required(c)],
    }),
    medicoId: new FormControl('', { nonNullable: true }),
  });

  readonly selectedRole = toSignal(this.form.controls.role.valueChanges, {
    initialValue: 'TenantAdmin' satisfies UserRole,
  });
  readonly showMedicoId = computed(() => this.selectedRole() === 'Medico');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.tenantId.set(id);
    this.loadTenant(id);
    this.loadUsers(id);
  }

  openModal(): void {
    this.form.reset();
    this.showModal.set(true);
  }

  closeModal(): void {
    this.showModal.set(false);
  }

  changeStatus(newStatus: TenantStatus): void {
    if (!confirm(`Confirmar alteração de status para "${newStatus}"?`)) {
      return;
    }
    const id = this.tenantId();
    this.saasService.updateTenantStatus(id, newStatus).subscribe({
      next: (updated) => {
        this.tenant.set(updated);
      },
      error: () => undefined,
    });
  }

  toggleUser(user: UserSummary): void {
    const id = this.tenantId();
    this.saasService.updateUserStatus(id, user.id, !user.isActive).subscribe({
      next: () => {
        this.loadUsers(id);
      },
      error: () => undefined,
    });
  }

  submitAddUser(): void {
    if (this.form.invalid || this.submitting()) {
      return;
    }
    const { email, role, medicoId } = this.form.getRawValue();
    if (role === 'Medico' && !medicoId.trim()) {
      this.form.controls.medicoId.markAsTouched();
      this.form.controls.medicoId.setErrors({ required: true });
      return;
    }
    const payload: CreateUserPayload =
      role === 'Medico' ? { email, role, medicoId } : { email, role };
    const id = this.tenantId();
    this.submitting.set(true);
    this.saasService.createUser(id, payload).subscribe({
      next: () => {
        this.showModal.set(false);
        this.form.reset();
        this.submitting.set(false);
        this.loadUsers(id);
      },
      error: () => {
        this.submitting.set(false);
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

  roleBadgeClass(role: UserRole): string {
    const map: Record<UserRole, string> = {
      TenantAdmin: 'badge badge--admin',
      Medico: 'badge badge--medico',
    };
    return map[role];
  }

  formatDate(isoDate: string): string {
    const d = new Date(isoDate);
    const day = String(d.getUTCDate()).padStart(2, '0');
    const month = String(d.getUTCMonth() + 1).padStart(2, '0');
    const year = String(d.getUTCFullYear());
    return `${day}/${month}/${year}`;
  }

  private loadTenant(id: string): void {
    this.saasService.listTenants().subscribe({
      next: (tenants) => {
        const found = tenants.find((t) => t.id === id);
        if (found !== undefined) {
          this.tenant.set(found);
        }
      },
      error: () => undefined,
    });
  }

  private loadUsers(id: string): void {
    this.saasService.listTenantUsers(id).subscribe({
      next: (u) => {
        this.users.set(u);
      },
      error: () => undefined,
    });
  }
}
