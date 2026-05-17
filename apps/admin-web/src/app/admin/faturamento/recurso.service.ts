import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import type {
  ListarRecursosParams,
  ListarRecursosResult,
  RecursoDetalheDto,
  RecursoDto,
  RecursoForm,
} from './recurso.types';

@Injectable({ providedIn: 'root' })
export class RecursoService {
  private readonly _http = inject(HttpClient);

  listar(params: ListarRecursosParams): Observable<ListarRecursosResult> {
    let httpParams = new HttpParams()
      .set('pagina', params.pagina.toString())
      .set('itensPorPagina', params.itensPorPagina.toString());

    if (params.operadoraId) {
      httpParams = httpParams.set('operadoraId', params.operadoraId);
    }
    if (params.prestadorId) {
      httpParams = httpParams.set('prestadorId', params.prestadorId);
    }

    return this._http.get<ListarRecursosResult>('/api/v1/admin/recursos', { params: httpParams });
  }

  obterPorId(id: string): Observable<RecursoDetalheDto> {
    return this._http.get<RecursoDetalheDto>(`/api/v1/admin/recursos/${id}`);
  }

  criar(payload: RecursoForm): Observable<RecursoDto> {
    return this._http.post<RecursoDto>('/api/v1/admin/recursos', payload);
  }

  atualizar(id: string, payload: RecursoForm): Observable<RecursoDto> {
    return this._http.put<RecursoDto>(`/api/v1/admin/recursos/${id}`, payload);
  }

  excluir(id: string): Observable<void> {
    return this._http.delete(`/api/v1/admin/recursos/${id}`).pipe(map(() => undefined));
  }

  baixarPdf(id: string): void {
    window.open(`/api/v1/admin/recursos/${id}/pdf`, '_blank');
  }
}
