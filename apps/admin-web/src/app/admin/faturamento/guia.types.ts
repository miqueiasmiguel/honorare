import type { PosicaoExecutor } from '../catalog/catalog.types';

export type { PosicaoExecutor };

export type SituacaoGuia = 'Apresentada' | 'Liquidada' | 'EmRecurso';
export type ViaAcesso =
  | 'Convencional'
  | 'Videolaparoscopia'
  | 'Endoscopica'
  | 'Percutanea'
  | 'NaoAplicavel';
export type Acomodacao = 'Enfermaria' | 'Apartamento' | 'Ambulatorial';

export type GuiaOrdenacao =
  | 'dataAtendimento'
  | 'numeroGuia'
  | 'prestadorNome'
  | 'operadoraNome'
  | 'beneficiarioNome'
  | 'situacao';

export interface ItemGuiaItem {
  id: string;
  procedimentoId: string;
  codigoTuss: string;
  descricaoProcedimento: string;
  posicaoExecutor: PosicaoExecutor;
  percentualOrdem: number;
  viaAcesso: ViaAcesso;
  acomodacao: Acomodacao;
  ehUrgencia: boolean;
  valorApurado: number | null;
  valorLiquidado: number | null;
  motivoGlosa: string | null;
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
  numeroGuia: string;
  dataAtendimento: string;
  situacao: SituacaoGuia;
  ehPacote: boolean;
  observacao: string;
  localAtendimento: string;
  totalItens: number;
  criadoEm: string;
  atualizadoEm: string;
}

export interface GuiaDetalheItem extends GuiaItem {
  itens: ItemGuiaItem[];
}

export interface ListarGuiasParams {
  prestadorId?: string;
  operadoraId?: string;
  dataInicio?: string;
  dataFim?: string;
  situacao?: SituacaoGuia;
  numeroGuia?: string;
  beneficiario?: string;
  semRecurso?: boolean;
  somenteComGlosa?: boolean;
  ordenarPor?: GuiaOrdenacao;
  descendente?: boolean;
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
  percentualOrdem: number;
  viaAcesso: ViaAcesso;
  acomodacao: Acomodacao;
  ehUrgencia: boolean;
  valorApurado: number | null;
  tempoAnestesicoMin?: number | null;
}

export interface ItemGuiaDisplay extends CriarItemGuiaPayload {
  id?: string;
  codigoTuss?: string;
  descricaoProcedimento?: string;
  valorLiquidado?: number | null;
  motivoGlosa?: string | null;
}

export interface CriarGuiaPayload {
  prestadorId: string;
  operadoraId: string;
  beneficiarioId: string | null;
  numeroGuia: string;
  dataAtendimento: string;
  ehPacote: boolean;
  observacao: string;
  localAtendimento: string;
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

export interface ErroImportacaoDto {
  linha: number;
  mensagem: string;
}

export interface AlertaImportacaoDto {
  linha: number;
  mensagem: string;
}

export interface ResultadoImportacaoGuiaDto {
  identificadorPagamento: string;
  somenteValidar: boolean;
  guiasCriadas: number;
  guiasAtualizadas: number;
  itensCriados: number;
  itensAtualizados: number;
  itensIgnorados: number;
  beneficiariosCriados: number;
  guiasPrevistas: number;
  itensPrevistas: number;
  erros: ErroImportacaoDto[];
  alertas: AlertaImportacaoDto[];
}
