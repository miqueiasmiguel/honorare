import type { SituacaoGuia } from './guia.types';

export interface RecursoForm {
  operadoraId: string;
  prestadorId: string;
  dataEmissao: string;
  observacao: string | null;
}

export interface RecursoDto {
  id: string;
  operadoraId: string;
  operadoraNome: string;
  prestadorId: string;
  prestadorNome: string;
  prestadorRegistroProfissional: string | null;
  numero: string;
  dataEmissao: string;
  observacao: string | null;
  totalGuias: number;
  criadoEm: string;
}

export interface GuiaNoRecursoDto {
  id: string;
  senha: string;
  dataAtendimento: string;
  beneficiarioNome: string | null;
  beneficiarioCarteira: string | null;
  situacao: string;
  totalItens: number;
}

export interface RecursoDetalheDto {
  header: RecursoDto;
  guias: GuiaNoRecursoDto[];
}

export interface ListarRecursosParams {
  operadoraId?: string;
  prestadorId?: string;
  pagina: number;
  itensPorPagina: number;
}

export interface ListarRecursosResult {
  itens: RecursoDto[];
  total: number;
  pagina: number;
  itensPorPagina: number;
}

export interface AdicionarGuiasLoteParams {
  prestadorId: string;
  operadoraId: string;
  dataInicio?: string;
  dataFim?: string;
  situacao?: SituacaoGuia;
  senha?: string;
  beneficiario?: string;
  somenteComGlosa?: boolean;
}
