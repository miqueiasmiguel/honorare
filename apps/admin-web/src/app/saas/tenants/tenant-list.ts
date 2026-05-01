import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { SaasService } from '../saas.service';
import type { TenantStatus, TenantSummary } from '../saas.types';

@Component({
  selector: 'app-tenant-list',
  imports: [RouterLink, ReactiveFormsModule],
  templateUrl: './tenant-list.html',
  styleUrl: './tenant-list.scss',
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
