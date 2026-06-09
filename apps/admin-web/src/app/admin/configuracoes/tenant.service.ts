import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import type { TenantSettings } from './tenant.types';

@Injectable({ providedIn: 'root' })
export class TenantService {
  private readonly _http = inject(HttpClient);

  getSettings(): Observable<TenantSettings> {
    return this._http.get<TenantSettings>('/api/v1/admin/tenant');
  }

  rename(name: string): Observable<TenantSettings> {
    return this._http.patch<TenantSettings>('/api/v1/admin/tenant', { name });
  }

  uploadLogo(file: File): Observable<TenantSettings> {
    const fd = new FormData();
    fd.append('file', file);
    return this._http.post<TenantSettings>('/api/v1/admin/tenant/logo', fd);
  }

  downloadLogo(): Observable<Blob> {
    return this._http.get('/api/v1/admin/tenant/logo', { responseType: 'blob' });
  }

  deleteLogo(): Observable<unknown> {
    return this._http.delete('/api/v1/admin/tenant/logo');
  }
}
