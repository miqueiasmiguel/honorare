import type { PosicaoExecutor } from '../catalog/catalog.types';

export type { PosicaoExecutor };

export type SituacaoGuia = 'Apresentada' | 'Liquidada' | 'EmRecurso';
export type ViaAcesso =
  | 'Convencional'
  | 'Videolaparoscopia'
  | 'Endoscopica'
  | 'Percutanea'
  | 'NaoAplicavel';
export type OrdemProcedimento =
  | 'Unico'
  | 'Principal'
  | 'SecundarioMesmaVia'
  | 'SecundarioViaDiferente';
export type Acomodacao = 'Enfermaria' | 'Apartamento' | 'Ambulatorial';

export interface ItemGuiaItem {
  id: string;
  procedimentoId: string;
  codigoTuss: string;
  descricaoProcedimento: string;
  posicaoExecutor: PosicaoExecutor;
  ordemProcedimento: OrdemProcedimento;
  viaAcesso: ViaAcesso;
  acomodacao: Acomodacao;
  ehUrgencia: boolean;
  valorApurado: number | null;
  valorLiquidado: number | null;
  tempoAnestesicoMin?: number | null;
}

export interface GuiaItem {
  id: string;
  prestadorId: string;
  prestadorNome: string;
  operadoraId: string;
  operadoraNome: string;
  beneficiarioId: string | null;
  beneficiarioNome: string;
  beneficiarioCarteira: string;
  senha: string;
  dataAtendimento: string;
  situacao: SituacaoGuia;
  ehPacote: boolean;
  observacao: string;
  totalItens: number;
  criadoEm: string;
  atualizadoEm: string;
}

export interface GuiaDetalheItem extends GuiaItem {
  itens: ItemGuiaItem[];
}

export interface ListarGuiasParams {
  prestadorId?: string;
  dataInicio?: string;
  dataFim?: string;
  situacao?: SituacaoGuia;
  pagina: number;
  itensPorPagina: number;
}

export interface ListarGuiasResult {
  itens: GuiaItem[];
  total: number;
  pagina: number;
  itensPorPagina: number;
}

export interface CriarItemGuiaPayload {
  procedimentoId: string;
  posicaoExecutor: PosicaoExecutor;
  ordemProcedimento: OrdemProcedimento;
  viaAcesso: ViaAcesso;
  acomodacao: Acomodacao;
  ehUrgencia: boolean;
  valorApurado: number | null;
  tempoAnestesicoMin?: number | null;
}

export interface CriarGuiaPayload {
  prestadorId: string;
  operadoraId: string;
  beneficiarioId: string | null;
  senha: string;
  dataAtendimento: string;
  ehPacote: boolean;
  observacao: string;
  itens: CriarItemGuiaPayload[];
}

export type AtualizarGuiaPayload = Omit<CriarGuiaPayload, 'prestadorId'>;

export interface PassoCalculoItem {
  regra: string;
  fator: number;
  valorResultante: number;
}

export interface ItemCalculoItem {
  itemGuiaId: string;
  codigoTuss: string;
  descricaoProcedimento: string;
  situacao: 'Calculado' | 'SemTabela' | 'SemDeflator' | 'Indeterminado' | 'Pacote';
  valorApurado: number | null;
  passos: PassoCalculoItem[];
}

export interface GuiaCalculoResult {
  guiaId: string;
  ehPacote: boolean;
  realizadoEm: string | null;
  itens: ItemCalculoItem[];
}
