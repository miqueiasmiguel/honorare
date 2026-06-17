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
  porteAnestesico: string | null;
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
  porteAnestesico: string | null;
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
  emailAcesso: string | null;
  temUsuario: boolean;
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

export interface CriarPrestadorPayload {
  nome: string;
  registroProfissional: string | null;
  emailAcesso: string | null;
}

export interface AtualizarPrestadorPayload {
  nome: string;
  registroProfissional: string | null;
  ativo: boolean;
}

export interface DefinirEmailAcessoPayload {
  email: string;
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

export interface ProcedimentoValorOperadoraItem {
  operadoraId: string;
  operadoraNome: string;
  tipoRuleSet: TipoRuleSet;
  tabelaId: string | null;
  valor: number | null;
  atualizadoEm: string | null;
}

export interface UpsertValorPayload {
  valor: number;
}

// ── Beneficiário ────────────────────────────────────────────────────────────

export interface BeneficiarioItem {
  id: string;
  carteira: string;
  nome: string;
  criadoEm: string;
}

export interface ListarBeneficiariosParams {
  carteira?: string;
  nome?: string;
  pagina: number;
  itensPorPagina: number;
}

export interface ListarBeneficiariosResult {
  itens: BeneficiarioItem[];
  total: number;
  pagina: number;
  itensPorPagina: number;
}

export interface LookupOrCreateResult extends BeneficiarioItem {
  criado: boolean;
}

export interface AtualizarBeneficiarioPayload {
  nome: string;
}

// ── Tabela Porte Anestésico ──────────────────────────────────────────────────

export interface TabelaPorteAnestesicoItem {
  id: string;
  porteLetra: string;
  valorEnfermaria: number;
  valorApartamento: number;
  valorAmbulatorial: number | null;
  atualizadoEm: string;
}

export interface ImportarTabelaPorteResult {
  portesAtualizados: number;
  procedimentosAtualizados: number;
  procedimentosNaoEncontrados: string[];
  erros: ImportarCsvErro[];
}
