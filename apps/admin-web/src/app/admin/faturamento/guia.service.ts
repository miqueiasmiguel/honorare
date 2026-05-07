import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import type {
  AtualizarGuiaPayload,
  CriarGuiaPayload,
  GuiaDetalheItem,
  ListarGuiasParams,
  ListarGuiasResult,
} from './guia.types';

@Injectable({ providedIn: 'root' })
export class GuiaService {
  private readonly _http = inject(HttpClient);

  listar(params: ListarGuiasParams): Observable<ListarGuiasResult> {
    let httpParams = new HttpParams()
      .set('pagina', params.pagina.toString())
      .set('itensPorPagina', params.itensPorPagina.toString());

    if (params.prestadorId) {
      httpParams = httpParams.set('prestadorId', params.prestadorId);
    }
    if (params.dataInicio) {
      httpParams = httpParams.set('dataInicio', params.dataInicio);
    }
    if (params.dataFim) {
      httpParams = httpParams.set('dataFim', params.dataFim);
    }
    if (params.situacao) {
      httpParams = httpParams.set('situacao', params.situacao);
    }

    return this._http.get<ListarGuiasResult>('/api/v1/admin/guias', { params: httpParams });
  }

  obterPorId(id: string): Observable<GuiaDetalheItem> {
    return this._http.get<GuiaDetalheItem>(`/api/v1/admin/guias/${id}`);
  }

  criar(payload: CriarGuiaPayload): Observable<GuiaDetalheItem> {
    return this._http.post<GuiaDetalheItem>('/api/v1/admin/guias', payload);
  }

  atualizar(id: string, payload: AtualizarGuiaPayload): Observable<GuiaDetalheItem> {
    return this._http.put<GuiaDetalheItem>(`/api/v1/admin/guias/${id}`, payload);
  }

  excluir(id: string): Observable<void> {
    return this._http.delete(`/api/v1/admin/guias/${id}`).pipe(
      map(() => {
        return;
      }),
    );
  }
}
