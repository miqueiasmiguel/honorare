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

export type PosicaoExecutor =
  | 'Cirurgiao'
  | 'PrimeiroAuxiliar'
  | 'SegundoAuxiliar'
  | 'TerceiroAuxiliar'
  | 'Anestesista'
  | 'ClinicoAssistente';

export interface PrestadorItem {
  id: string;
  nome: string;
  registroProfissional: string | null;
  ativo: boolean;
  criadoEm: string;
}

export interface ListarPrestadoresParams {
  busca?: string;
  ativo?: boolean;
  pagina: number;
  itensPorPagina: number;
}

export interface ListarPrestadoresResult {
  itens: PrestadorItem[];
  total: number;
  pagina: number;
  itensPorPagina: number;
}

export interface SalvarPrestadorPayload {
  nome: string;
  registroProfissional: string | null;
  ativo: boolean;
}

export interface DeflatorItem {
  id: string;
  prestadorId: string;
  operadoraId: string;
  posicao: PosicaoExecutor;
  percentual: number;
}

export interface SalvarDeflatorPayload {
  operadoraId: string;
  posicao: PosicaoExecutor;
  percentual: number;
}

export interface TabelaItem {
  id: string;
  operadoraId: string;
  procedimentoId: string;
  codigoTuss: string;
  descricao: string;
  valor: number;
  atualizadoEm: string;
}

export interface ListarTabelasParams {
  operadoraId?: string;
  codigoTuss?: string;
  pagina: number;
  itensPorPagina: number;
}

export interface ListarTabelasResult {
  itens: TabelaItem[];
  total: number;
  pagina: number;
  itensPorPagina: number;
}

export interface SalvarTabelaPayload {
  operadoraId: string;
  procedimentoId: string;
  valor: number;
}
