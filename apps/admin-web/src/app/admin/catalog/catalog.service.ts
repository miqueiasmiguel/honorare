import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import type {
  AtualizarBeneficiarioPayload,
  AtualizarPrestadorPayload,
  BeneficiarioItem,
  CriarPrestadorPayload,
  DeflatorItem,
  ImportarCsvResult,
  ImportarTabelaPorteResult,
  ListarBeneficiariosParams,
  ListarBeneficiariosResult,
  ListarOperadorasParams,
  ListarOperadorasResult,
  ListarPrestadoresParams,
  ListarPrestadoresResult,
  ListarProcedimentosParams,
  ListarProcedimentosResult,
  ListarTabelasParams,
  ListarTabelasResult,
  LookupOrCreateResult,
  OperadoraItem,
  PrestadorItem,
  ProcedimentoItem,
  ProcedimentoValorOperadoraItem,
  SalvarDeflatorPayload,
  SalvarOperadoraPayload,
  SalvarProcedimentoPayload,
  SalvarTabelaPayload,
  TabelaItem,
  TabelaPorteAnestesicoItem,
  UpsertValorPayload,
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

  listarPrestadores(params: ListarPrestadoresParams): Observable<ListarPrestadoresResult> {
    let httpParams = new HttpParams()
      .set('pagina', params.pagina.toString())
      .set('itensPorPagina', params.itensPorPagina.toString());

    if (params.busca) {
      httpParams = httpParams.set('busca', params.busca);
    }
    if (params.ativo !== undefined) {
      httpParams = httpParams.set('ativo', params.ativo.toString());
    }

    return this._http.get<ListarPrestadoresResult>('/api/v1/admin/prestadores', {
      params: httpParams,
    });
  }

  obterPrestador(id: string): Observable<PrestadorItem> {
    return this._http.get<PrestadorItem>(`/api/v1/admin/prestadores/${id}`);
  }

  criarPrestador(payload: CriarPrestadorPayload): Observable<PrestadorItem> {
    return this._http.post<PrestadorItem>('/api/v1/admin/prestadores', payload);
  }

  atualizarPrestador(id: string, payload: AtualizarPrestadorPayload): Observable<PrestadorItem> {
    return this._http.put<PrestadorItem>(`/api/v1/admin/prestadores/${id}`, payload);
  }

  excluirPrestador(id: string): Observable<void> {
    return this._http.delete(`/api/v1/admin/prestadores/${id}`).pipe(
      map(() => {
        return;
      }),
    );
  }

  listarDeflatores(prestadorId: string): Observable<DeflatorItem[]> {
    return this._http.get<DeflatorItem[]>(`/api/v1/admin/prestadores/${prestadorId}/deflatores`);
  }

  criarDeflator(prestadorId: string, payload: SalvarDeflatorPayload): Observable<DeflatorItem> {
    return this._http.post<DeflatorItem>(
      `/api/v1/admin/prestadores/${prestadorId}/deflatores`,
      payload,
    );
  }

  atualizarDeflator(
    prestadorId: string,
    id: string,
    payload: SalvarDeflatorPayload,
  ): Observable<DeflatorItem> {
    return this._http.put<DeflatorItem>(
      `/api/v1/admin/prestadores/${prestadorId}/deflatores/${id}`,
      payload,
    );
  }

  excluirDeflator(prestadorId: string, id: string): Observable<void> {
    return this._http.delete(`/api/v1/admin/prestadores/${prestadorId}/deflatores/${id}`).pipe(
      map(() => {
        return;
      }),
    );
  }

  listarTabelas(params: ListarTabelasParams): Observable<ListarTabelasResult> {
    let httpParams = new HttpParams()
      .set('pagina', params.pagina.toString())
      .set('itensPorPagina', params.itensPorPagina.toString());

    if (params.operadoraId) {
      httpParams = httpParams.set('operadoraId', params.operadoraId);
    }
    if (params.codigoTuss) {
      httpParams = httpParams.set('codigoTuss', params.codigoTuss);
    }

    return this._http.get<ListarTabelasResult>('/api/v1/admin/tabelas', { params: httpParams });
  }

  obterTabela(id: string): Observable<TabelaItem> {
    return this._http.get<TabelaItem>(`/api/v1/admin/tabelas/${id}`);
  }

  criarTabela(payload: SalvarTabelaPayload): Observable<TabelaItem> {
    return this._http.post<TabelaItem>('/api/v1/admin/tabelas', payload);
  }

  atualizarTabela(id: string, payload: SalvarTabelaPayload): Observable<TabelaItem> {
    return this._http.put<TabelaItem>(`/api/v1/admin/tabelas/${id}`, payload);
  }

  excluirTabela(id: string): Observable<void> {
    return this._http.delete(`/api/v1/admin/tabelas/${id}`).pipe(
      map(() => {
        return;
      }),
    );
  }

  importarTabelaCsv(operadoraId: string, file: File): Observable<ImportarCsvResult> {
    const formData = new FormData();
    formData.append('file', file);
    const httpParams = new HttpParams().set('operadoraId', operadoraId);
    return this._http.post<ImportarCsvResult>('/api/v1/admin/tabelas/importar-csv', formData, {
      params: httpParams,
    });
  }

  // ── Valores por procedimento (camada de conveniência) ──────────────────────

  listarValoresPorProcedimento(procId: string): Observable<ProcedimentoValorOperadoraItem[]> {
    return this._http.get<ProcedimentoValorOperadoraItem[]>(
      `/api/v1/admin/procedimentos/${procId}/valores`,
    );
  }

  upsertValorPorProcedimento(
    procId: string,
    operadoraId: string,
    payload: UpsertValorPayload,
  ): Observable<TabelaItem> {
    return this._http.put<TabelaItem>(
      `/api/v1/admin/procedimentos/${procId}/valores/${operadoraId}`,
      payload,
    );
  }

  excluirValorPorProcedimento(procId: string, operadoraId: string): Observable<void> {
    return this._http.delete(`/api/v1/admin/procedimentos/${procId}/valores/${operadoraId}`).pipe(
      map(() => {
        return;
      }),
    );
  }

  // ── Beneficiários ────────────────────────────────────────────────────────────

  listarBeneficiarios(params: ListarBeneficiariosParams): Observable<ListarBeneficiariosResult> {
    let httpParams = new HttpParams()
      .set('pagina', params.pagina.toString())
      .set('itensPorPagina', params.itensPorPagina.toString());

    if (params.carteira) {
      httpParams = httpParams.set('carteira', params.carteira);
    }
    if (params.nome) {
      httpParams = httpParams.set('nome', params.nome);
    }

    return this._http.get<ListarBeneficiariosResult>('/api/v1/admin/beneficiarios', {
      params: httpParams,
    });
  }

  obterBeneficiario(id: string): Observable<BeneficiarioItem> {
    return this._http.get<BeneficiarioItem>(`/api/v1/admin/beneficiarios/${id}`);
  }

  lookupOrCreateBeneficiario(carteira: string, nome: string): Observable<LookupOrCreateResult> {
    return this._http.post<LookupOrCreateResult>('/api/v1/admin/beneficiarios/lookup-or-create', {
      carteira,
      nome,
    });
  }

  atualizarBeneficiario(
    id: string,
    payload: AtualizarBeneficiarioPayload,
  ): Observable<BeneficiarioItem> {
    return this._http.put<BeneficiarioItem>(`/api/v1/admin/beneficiarios/${id}`, payload);
  }

  excluirBeneficiario(id: string): Observable<void> {
    return this._http.delete(`/api/v1/admin/beneficiarios/${id}`).pipe(
      map(() => {
        return;
      }),
    );
  }

  // ── Tabela Porte Anestésico ────────────────────────────────────────────────

  listarPortesAnestesico(operadoraId: string): Observable<TabelaPorteAnestesicoItem[]> {
    const params = new HttpParams().set('operadoraId', operadoraId);
    return this._http.get<TabelaPorteAnestesicoItem[]>('/api/v1/admin/tabelas-porte-anestesico', {
      params,
    });
  }

  importarTabelaPorteAnestesico(
    operadoraId: string,
    file: File,
  ): Observable<ImportarTabelaPorteResult> {
    const form = new FormData();
    form.append('file', file);
    const params = new HttpParams().set('operadoraId', operadoraId);
    return this._http.post<ImportarTabelaPorteResult>(
      '/api/v1/admin/tabelas-porte-anestesico/importar-unimed-csv',
      form,
      { params },
    );
  }
}
