import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import type {
  CreateTenantPayload,
  CreateUserPayload,
  TenantStatus,
  TenantSummary,
  TenantWithOwnerSummary,
  UserSummary,
} from './saas.types';

@Injectable({ providedIn: 'root' })
export class SaasService {
  private readonly http = inject(HttpClient);

  listTenants(): Observable<TenantSummary[]> {
    return this.http.get<TenantSummary[]>('/api/v1/saas/tenants');
  }

  createTenant(payload: CreateTenantPayload): Observable<TenantWithOwnerSummary> {
    return this.http.post<TenantWithOwnerSummary>('/api/v1/saas/tenants', payload);
  }

  updateTenantStatus(tenantId: string, status: TenantStatus): Observable<TenantSummary> {
    return this.http.patch<TenantSummary>(`/api/v1/saas/tenants/${tenantId}/status`, { status });
  }

  listTenantUsers(tenantId: string): Observable<UserSummary[]> {
    return this.http.get<UserSummary[]>(`/api/v1/saas/tenants/${tenantId}/users`);
  }

  createUser(tenantId: string, payload: CreateUserPayload): Observable<UserSummary> {
    return this.http.post<UserSummary>(`/api/v1/saas/tenants/${tenantId}/users`, payload);
  }

  updateUserStatus(tenantId: string, userId: string, isActive: boolean): Observable<unknown> {
    return this.http.patch(`/api/v1/saas/tenants/${tenantId}/users/${userId}/status`, { isActive });
  }
}
