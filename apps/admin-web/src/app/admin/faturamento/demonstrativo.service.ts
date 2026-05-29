import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import type {
  DemonstrativoDetalheDto,
  DemonstrativoDto,
  DemonstrativoForm,
  ItemDemonstrativoForm,
  ListarDemonstrativosParams,
  ListarDemonstrativosResult,
  ResultadoImportacaoDto,
} from './demonstrativo.types';

@Injectable({ providedIn: 'root' })
export class DemonstrativoService {
  private readonly _http = inject(HttpClient);

  listar(params: ListarDemonstrativosParams): Observable<ListarDemonstrativosResult> {
    let httpParams = new HttpParams()
      .set('pagina', params.pagina.toString())
      .set('itensPorPagina', params.itensPorPagina.toString());

    if (params.operadoraId) {
      httpParams = httpParams.set('operadoraId', params.operadoraId);
    }
    if (params.competencia) {
      httpParams = httpParams.set('competencia', params.competencia);
    }

    return this._http.get<ListarDemonstrativosResult>('/api/v1/admin/demonstrativos', {
      params: httpParams,
    });
  }

  obterPorId(id: string): Observable<DemonstrativoDetalheDto> {
    return this._http.get<DemonstrativoDetalheDto>(`/api/v1/admin/demonstrativos/${id}`);
  }

  criar(payload: DemonstrativoForm): Observable<DemonstrativoDetalheDto> {
    return this._http.post<DemonstrativoDetalheDto>('/api/v1/admin/demonstrativos', payload);
  }

  atualizar(id: string, payload: DemonstrativoForm): Observable<DemonstrativoDto> {
    return this._http.put<DemonstrativoDto>(`/api/v1/admin/demonstrativos/${id}`, payload);
  }

  excluir(id: string): Observable<void> {
    return this._http.delete(`/api/v1/admin/demonstrativos/${id}`).pipe(
      map(() => {
        return;
      }),
    );
  }

  adicionarItem(id: string, payload: ItemDemonstrativoForm): Observable<DemonstrativoDetalheDto> {
    return this._http.post<DemonstrativoDetalheDto>(
      `/api/v1/admin/demonstrativos/${id}/itens`,
      payload,
    );
  }

  removerItem(id: string, itemId: string): Observable<void> {
    return this._http
      .delete(`/api/v1/admin/demonstrativos/${id}/itens/${itemId}`)
      .pipe(map(() => undefined));
  }

  conciliarItem(id: string, itemId: string, itemGuiaId: string): Observable<void> {
    return this._http
      .post(`/api/v1/admin/demonstrativos/${id}/itens/${itemId}/conciliar`, { itemGuiaId })
      .pipe(map(() => undefined));
  }

  desconciliarItem(id: string, itemId: string): Observable<void> {
    return this._http
      .delete(`/api/v1/admin/demonstrativos/${id}/itens/${itemId}/conciliar`)
      .pipe(map(() => undefined));
  }

  importarCsv(
    arquivo: File,
    prestadorId: string,
    operadoraId: string,
    somenteValidar: boolean,
  ): Observable<ResultadoImportacaoDto> {
    const formData = new FormData();
    formData.append('arquivo', arquivo);
    formData.append('prestadorId', prestadorId);
    formData.append('operadoraId', operadoraId);
    formData.append('somenteValidar', String(somenteValidar));
    return this._http.post<ResultadoImportacaoDto>(
      '/api/v1/admin/demonstrativos/importar-csv',
      formData,
    );
  }
}
