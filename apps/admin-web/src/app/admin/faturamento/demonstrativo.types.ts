export interface DemonstrativoForm {
  operadoraId: string;
  competencia: string;
  dataRecebimento: string;
  observacao: string | null;
}

export interface ItemDemonstrativoForm {
  senha: string;
  codigoTuss: string;
  descricao: string | null;
  valorApresentado: number;
  valorPago: number;
  motivoGlosa: string | null;
}

export interface DemonstrativoDto {
  id: string;
  operadoraId: string;
  operadoraNome: string;
  competencia: string;
  dataRecebimento: string;
  observacao: string | null;
  totalItens: number;
  itensConciliados: number;
  criadoEm: string;
}

export interface ItemDemonstrativoDto {
  id: string;
  senha: string;
  codigoTuss: string;
  descricao: string | null;
  valorApresentado: number;
  valorPago: number;
  valorGlosado: number;
  motivoGlosa: string | null;
  itemGuiaId: string | null;
  conciliado: boolean;
}

export interface DemonstrativoDetalheDto {
  header: DemonstrativoDto;
  itens: ItemDemonstrativoDto[];
}

export interface ListarDemonstrativosParams {
  operadoraId?: string;
  competencia?: string;
  pagina: number;
  itensPorPagina: number;
}

export interface ListarDemonstrativosResult {
  itens: DemonstrativoDto[];
  total: number;
  pagina: number;
  itensPorPagina: number;
}
