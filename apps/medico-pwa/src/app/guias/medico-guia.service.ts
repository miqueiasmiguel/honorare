import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import type { ListarGuiasParams, MedicoListarGuiasResult } from './medico-guia.types';

@Injectable({ providedIn: 'root' })
export class MedicoGuiaService {
  private readonly _http = inject(HttpClient);

  listar(params: ListarGuiasParams): Observable<MedicoListarGuiasResult> {
    let httpParams = new HttpParams()
      .set('pagina', params.pagina.toString())
      .set('itensPorPagina', params.itensPorPagina.toString());

    if (params.operadoraId) {
      httpParams = httpParams.set('operadoraId', params.operadoraId);
    }
    if (params.dataInicio) {
      httpParams = httpParams.set('dataInicio', params.dataInicio);
    }
    if (params.dataFim) {
      httpParams = httpParams.set('dataFim', params.dataFim);
    }

    return this._http.get<MedicoListarGuiasResult>('/api/v1/medico/guias', {
      params: httpParams,
    });
  }
}
