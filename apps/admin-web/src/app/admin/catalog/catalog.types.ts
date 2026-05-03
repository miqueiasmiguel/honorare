// TODO: regenerar após pnpm generate-api-client (TASK-CAT-06 completo)

export type TipoRuleSet = 'Unimed' | 'Nulo';

export interface OperadoraItem {
  id: string;
  nome: string;
  registroAns: string | null;
  cnpj: string | null;
  tipoRuleSet: TipoRuleSet;
  ativa: boolean;
  criadaEm: string;
}

export interface ListarOperadorasParams {
  nome?: string;
  ativa?: boolean;
  pagina: number;
  itensPorPagina: number;
}

export interface ListarOperadorasResult {
  itens: OperadoraItem[];
  total: number;
  pagina: number;
  itensPorPagina: number;
}

export interface SalvarOperadoraPayload {
  nome: string;
  registroAns: string | null;
  cnpj: string | null;
  tipoRuleSet: TipoRuleSet;
  ativa: boolean;
}

export interface ProcedimentoItem {
  id: string;
  codigoTuss: string;
  descricao: string;
  porte: string | null;
  porteAnestesico: number | null;
  ehSadt: boolean;
  temPorteProprioVideo: boolean;
  ativo: boolean;
  criadoEm: string;
}

export interface ListarProcedimentosParams {
  busca?: string;
  ativo?: boolean;
  pagina: number;
  itensPorPagina: number;
}

export interface ListarProcedimentosResult {
  itens: ProcedimentoItem[];
  total: number;
  pagina: number;
  itensPorPagina: number;
}

export interface SalvarProcedimentoPayload {
  codigoTuss: string;
  descricao: string;
  porte: string | null;
  porteAnestesico: number | null;
  ehSadt: boolean;
  temPorteProprioVideo: boolean;
  ativo: boolean;
}

export interface ImportarCsvErro {
  linha: number;
  mensagem: string;
}

export interface ImportarCsvResult {
  inseridos: number;
  atualizados: number;
  ignorados: number;
  erros: ImportarCsvErro[];
}
