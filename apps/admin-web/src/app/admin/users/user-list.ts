import { Component, inject, OnInit, signal } from '@angular/core';
import { AdminService } from '../admin.service';
import type { AdminUserSummary } from '../admin.types';
import type { UserRole } from '../../saas/saas.types';

@Component({
  selector: 'app-user-list',
  templateUrl: './user-list.html',
  styleUrl: './user-list.scss',
})
export class UserList implements OnInit {
  private readonly adminService = inject(AdminService);

  readonly users = signal<AdminUserSummary[]>([]);

  ngOnInit(): void {
    this.loadUsers();
  }

  toggleStatus(user: AdminUserSummary): void {
    this.adminService.updateUserStatus(user.id, !user.isActive).subscribe({
      next: () => {
        this.loadUsers();
      },
      error: () => undefined,
    });
  }

  roleBadgeClass(role: UserRole): string {
    return role === 'Medico' ? 'badge badge--medico' : 'badge badge--admin';
  }

  statusBadgeClass(isActive: boolean): string {
    return isActive ? 'badge badge--ativo' : 'badge badge--inativo';
  }

  displayNome(user: AdminUserSummary): string {
    return user.nome ?? user.email;
  }

  formatDate(isoDate: string): string {
    const d = new Date(isoDate);
    const day = String(d.getUTCDate()).padStart(2, '0');
    const month = String(d.getUTCMonth() + 1).padStart(2, '0');
    const year = String(d.getUTCFullYear());
    return `${day}/${month}/${year}`;
  }

  private loadUsers(): void {
    this.adminService.listUsers().subscribe({
      next: (u) => {
        this.users.set(u);
      },
      error: () => undefined,
    });
  }
}
