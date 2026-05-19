export type SituacaoGuia = 'Apresentada' | 'Liquidada' | 'EmRecurso';
export type SituacaoCalculo =
  | 'Calculado'
  | 'SemTabela'
  | 'SemDeflator'
  | 'Indeterminado'
  | 'Pacote'
  | 'NaoCalculado';

export interface MedicoGuiaSummaryItem {
  id: string;
  operadoraNome: string;
  beneficiarioNome: string | null;
  beneficiarioCarteira: string | null;
  senha: string | null;
  dataAtendimento: string;
  situacao: SituacaoGuia;
  totalItens: number;
  temObservacao: boolean;
}

export interface MedicoListarGuiasResult {
  itens: MedicoGuiaSummaryItem[];
  total: number;
  pagina: number;
  itensPorPagina: number;
}

export interface ListarGuiasParams {
  operadoraId?: string;
  dataInicio?: string;
  dataFim?: string;
  pagina: number;
  itensPorPagina: number;
}
