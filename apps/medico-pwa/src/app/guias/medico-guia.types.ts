export type SituacaoGuia = 'Apresentada' | 'Liquidada' | 'EmRecurso';
export type SituacaoCalculo =
  | 'Calculado'
  | 'SemTabela'
  | 'Indeterminado'
  | 'Pacote'
  | 'NaoCalculado';

export interface MedicoGuiaSummaryItem {
  id: string;
  operadoraNome: string;
  beneficiarioNome: string | null;
  beneficiarioCarteira: string | null;
  numeroGuia: string | null;
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

export interface MedicoItemGuiaDto {
  id: string;
  codigoTuss: string;
  descricaoProcedimento: string;
  posicaoExecutor: string;
  valorApurado: number | null;
  valorLiquidado: number | null;
  situacaoCalculo: SituacaoCalculo;
}

export interface MedicoGuiaDetalheDto {
  id: string;
  operadoraNome: string;
  beneficiarioNome: string | null;
  beneficiarioCarteira: string | null;
  dataAtendimento: string;
  numeroGuia: string | null;
  situacao: SituacaoGuia;
  observacao: string | null;
  itens: MedicoItemGuiaDto[];
}
