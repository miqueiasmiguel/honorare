import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { AuthService } from '../../auth/auth.service';
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
  templateUrl: './tenant-detail.html',
  styleUrl: './tenant-detail.scss',
})
export class TenantDetail implements OnInit {
  private readonly saasService = inject(SaasService);
  private readonly route = inject(ActivatedRoute);
  private readonly _auth = inject(AuthService);
  private readonly _router = inject(Router);

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

  gerenciar(): void {
    const t = this.tenant();
    if (!t) {
      return;
    }
    this._auth.enterImpersonation(t.id, t.name).subscribe({
      next: () => {
        void this._router.navigate(['/admin']);
      },
      error: () => undefined,
    });
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
