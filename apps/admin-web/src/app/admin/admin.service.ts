import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import type { AdminUserSummary, ProfileSummary, UpdateProfilePayload } from './admin.types';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);

  listUsers(): Observable<AdminUserSummary[]> {
    return this.http.get<AdminUserSummary[]>('/api/v1/admin/users');
  }

  updateUserStatus(userId: string, isActive: boolean): Observable<unknown> {
    return this.http.patch(`/api/v1/admin/users/${userId}/status`, { isActive });
  }

  getProfile(): Observable<ProfileSummary> {
    return this.http.get<ProfileSummary>('/api/v1/admin/profile');
  }

  updateProfile(payload: UpdateProfilePayload): Observable<ProfileSummary> {
    return this.http.patch<ProfileSummary>('/api/v1/admin/profile', payload);
  }
}
