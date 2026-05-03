import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import type {
  ImportarCsvResult,
  ListarOperadorasParams,
  ListarOperadorasResult,
  ListarProcedimentosParams,
  ListarProcedimentosResult,
  OperadoraItem,
  ProcedimentoItem,
  SalvarOperadoraPayload,
  SalvarProcedimentoPayload,
} from './catalog.types';

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly _http = inject(HttpClient);

  listarOperadoras(params: ListarOperadorasParams): Observable<ListarOperadorasResult> {
    let httpParams = new HttpParams()
      .set('pagina', params.pagina.toString())
      .set('itensPorPagina', params.itensPorPagina.toString());

    if (params.nome) {
      httpParams = httpParams.set('nome', params.nome);
    }
    if (params.ativa !== undefined) {
      httpParams = httpParams.set('ativa', params.ativa.toString());
    }

    return this._http.get<ListarOperadorasResult>('/api/v1/admin/operadoras', {
      params: httpParams,
    });
  }

  obterOperadora(id: string): Observable<OperadoraItem> {
    return this._http.get<OperadoraItem>(`/api/v1/admin/operadoras/${id}`);
  }

  criarOperadora(payload: SalvarOperadoraPayload): Observable<OperadoraItem> {
    return this._http.post<OperadoraItem>('/api/v1/admin/operadoras', payload);
  }

  atualizarOperadora(id: string, payload: SalvarOperadoraPayload): Observable<OperadoraItem> {
    return this._http.put<OperadoraItem>(`/api/v1/admin/operadoras/${id}`, payload);
  }

  excluirOperadora(id: string): Observable<void> {
    return this._http.delete(`/api/v1/admin/operadoras/${id}`).pipe(
      map(() => {
        return;
      }),
    );
  }

  listarProcedimentos(params: ListarProcedimentosParams): Observable<ListarProcedimentosResult> {
    let httpParams = new HttpParams()
      .set('pagina', params.pagina.toString())
      .set('itensPorPagina', params.itensPorPagina.toString());

    if (params.busca) {
      httpParams = httpParams.set('busca', params.busca);
    }
    if (params.ativo !== undefined) {
      httpParams = httpParams.set('ativo', params.ativo.toString());
    }

    return this._http.get<ListarProcedimentosResult>('/api/v1/admin/procedimentos', {
      params: httpParams,
    });
  }

  obterProcedimento(id: string): Observable<ProcedimentoItem> {
    return this._http.get<ProcedimentoItem>(`/api/v1/admin/procedimentos/${id}`);
  }

  criarProcedimento(payload: SalvarProcedimentoPayload): Observable<ProcedimentoItem> {
    return this._http.post<ProcedimentoItem>('/api/v1/admin/procedimentos', payload);
  }

  atualizarProcedimento(
    id: string,
    payload: SalvarProcedimentoPayload,
  ): Observable<ProcedimentoItem> {
    return this._http.put<ProcedimentoItem>(`/api/v1/admin/procedimentos/${id}`, payload);
  }

  excluirProcedimento(id: string): Observable<void> {
    return this._http.delete(`/api/v1/admin/procedimentos/${id}`).pipe(
      map(() => {
        return;
      }),
    );
  }

  importarCsv(file: File): Observable<ImportarCsvResult> {
    const formData = new FormData();
    formData.append('file', file);
    return this._http.post<ImportarCsvResult>('/api/v1/admin/procedimentos/importar-csv', formData);
  }
}
