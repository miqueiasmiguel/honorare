import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import type {
  AdicionarGuiasLoteParams,
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

  adicionarGuia(recursoId: string, guiaId: string): Observable<void> {
    return this._http
      .post(`/api/v1/admin/recursos/${recursoId}/guias/${guiaId}`, {})
      .pipe(map(() => undefined));
  }

  adicionarGuiasLote(
    recursoId: string,
    filtros: AdicionarGuiasLoteParams,
  ): Observable<{ adicionadas: number }> {
    return this._http.post<{ adicionadas: number }>(
      `/api/v1/admin/recursos/${recursoId}/guias/lote`,
      filtros,
    );
  }

  removerGuia(recursoId: string, guiaId: string): Observable<void> {
    return this._http
      .delete(`/api/v1/admin/recursos/${recursoId}/guias/${guiaId}`)
      .pipe(map(() => undefined));
  }

  alterarInclusaoItem(
    recursoId: string,
    guiaId: string,
    itemId: string,
    incluido: boolean,
  ): Observable<void> {
    return this._http
      .patch(`/api/v1/admin/recursos/${recursoId}/guias/${guiaId}/itens/${itemId}/inclusao`, {
        incluido,
      })
      .pipe(map(() => undefined));
  }

  baixarPdf(id: string): void {
    this._http.get(`/api/v1/admin/recursos/${id}/pdf`, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `RECURSO_${id}.pdf`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
      },
      error: () => undefined,
    });
  }
}
