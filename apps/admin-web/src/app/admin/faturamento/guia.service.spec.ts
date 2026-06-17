import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { GuiaService } from './guia.service';
import type {
  CriarGuiaPayload,
  CriarItemGuiaPayload,
  AtualizarGuiaPayload,
  GuiaDetalheItem,
  GuiaItem,
  ItemGuiaItem,
  GuiaCalculoResult,
  ListarGuiasResult,
  ResultadoImportacaoGuiaDto,
} from './guia.types';

const GUIA_DETALHE: GuiaDetalheItem = {
  id: 'guia-1',
  prestadorId: 'prest-1',
  prestadorNome: 'Dr. Fulano',
  operadoraId: 'op-1',
  operadoraNome: 'UNIMED',
  beneficiarioId: 'ben-1',
  beneficiarioNome: 'JOÃO SILVA',
  beneficiarioCarteira: '0001234567',
  numeroGuia: '12345',
  dataAtendimento: '2026-05-01',
  situacao: 'Apresentada',
  ehPacote: false,
  observacao: '',
  localAtendimento: '',
  totalItens: 1,
  criadoEm: '2026-05-01T00:00:00Z',
  atualizadoEm: '2026-05-01T00:00:00Z',
  itens: [
    {
      id: 'item-1',
      procedimentoId: 'proc-1',
      codigoTuss: '31303079',
      descricaoProcedimento: 'Consulta médica',
      posicaoExecutor: 'Cirurgiao',
      ordemProcedimento: 'Unico',
      viaAcesso: 'Convencional',
      acomodacao: 'Ambulatorial',
      ehUrgencia: false,
      valorApurado: null,
      valorLiquidado: null,
      motivoGlosa: null,
    },
  ],
};

const CRIAR_PAYLOAD: CriarGuiaPayload = {
  prestadorId: 'prest-1',
  operadoraId: 'op-1',
  beneficiarioId: 'ben-1',
  numeroGuia: '12345',
  dataAtendimento: '2026-05-01',
  ehPacote: false,
  observacao: '',
  localAtendimento: '',
  itens: [
    {
      procedimentoId: 'proc-1',
      posicaoExecutor: 'Cirurgiao',
      ordemProcedimento: 'Unico',
      viaAcesso: 'Convencional',
      acomodacao: 'Ambulatorial',
      ehUrgencia: false,
      valorApurado: null,
    },
  ],
};

function makeListResult(itens: GuiaDetalheItem[] = []): ListarGuiasResult {
  return { itens, total: itens.length, pagina: 1, itensPorPagina: 20 };
}

const GUIA_CALCULO_RESULT: GuiaCalculoResult = {
  guiaId: 'guia-1',
  ehPacote: false,
  realizadoEm: '2026-05-01T00:00:00Z',
  itens: [
    {
      itemGuiaId: 'item-1',
      codigoTuss: '31303079',
      descricaoProcedimento: 'Consulta médica',
      situacao: 'Calculado',
      valorApurado: 200,
      passos: [{ regra: 'ValorBase', fator: 1, valorResultante: 200 }],
    },
  ],
};

describe('GuiaService', () => {
  let service: GuiaService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), GuiaService],
    });
    service = TestBed.inject(GuiaService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('listar_chamaCaminhoCorretoComParams', () => {
    let result: ListarGuiasResult | undefined;
    service
      .listar({ pagina: 1, itensPorPagina: 20, prestadorId: 'prest-1' })
      .subscribe((v) => (result = v));

    const req = httpMock.expectOne(
      (r) =>
        r.url === '/api/v1/admin/guias' &&
        r.params.get('pagina') === '1' &&
        r.params.get('itensPorPagina') === '20' &&
        r.params.get('prestadorId') === 'prest-1',
    );
    expect(req.request.method).toBe('GET');
    req.flush(makeListResult());

    expect(result?.total).toBe(0);
  });

  it('listar_comTodosOsFiltros_configuraTodosOsParams', () => {
    service
      .listar({
        pagina: 2,
        itensPorPagina: 10,
        prestadorId: 'p1',
        operadoraId: 'op1',
        dataInicio: '2026-01-01',
        dataFim: '2026-01-31',
        situacao: 'Liquidada',
        numeroGuia: 'ABC123',
        beneficiario: 'João',
        semRecurso: true,
        somenteComGlosa: false,
      })
      .subscribe();

    const req = httpMock.expectOne(
      (r) =>
        r.url === '/api/v1/admin/guias' &&
        r.params.get('operadoraId') === 'op1' &&
        r.params.get('dataInicio') === '2026-01-01' &&
        r.params.get('dataFim') === '2026-01-31' &&
        r.params.get('situacao') === 'Liquidada' &&
        r.params.get('numeroGuia') === 'ABC123' &&
        r.params.get('beneficiario') === 'João' &&
        r.params.get('semRecurso') === 'true' &&
        r.params.get('somenteComGlosa') === 'false',
    );
    expect(req.request.method).toBe('GET');
    req.flush(makeListResult());
  });

  it('listar_mapeiaOrdenarPorParaNomeDoEnumDoBackend', () => {
    service
      .listar({ pagina: 1, itensPorPagina: 20, ordenarPor: 'prestadorNome', descendente: false })
      .subscribe();

    const req = httpMock.expectOne(
      (r) =>
        r.url === '/api/v1/admin/guias' &&
        r.params.get('ordenarPor') === 'Prestador' &&
        r.params.get('descendente') === 'false',
    );
    expect(req.request.method).toBe('GET');
    req.flush(makeListResult());
  });

  it('listar_mapeiaBeneficiarioNomeEDescendente', () => {
    service
      .listar({ pagina: 1, itensPorPagina: 20, ordenarPor: 'beneficiarioNome', descendente: true })
      .subscribe();

    const req = httpMock.expectOne(
      (r) =>
        r.url === '/api/v1/admin/guias' &&
        r.params.get('ordenarPor') === 'Beneficiario' &&
        r.params.get('descendente') === 'true',
    );
    req.flush(makeListResult());
  });

  it('obterPorId_chamaCaminhoCorreto', () => {
    let result: GuiaDetalheItem | undefined;
    service.obterPorId('guia-1').subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/guias/guia-1');
    expect(req.request.method).toBe('GET');
    req.flush(GUIA_DETALHE);

    expect(result?.id).toBe('guia-1');
  });

  it('criar_chamaPOST', () => {
    let result: GuiaDetalheItem | undefined;
    service.criar(CRIAR_PAYLOAD).subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/guias');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(CRIAR_PAYLOAD);
    req.flush(GUIA_DETALHE);

    expect(result?.id).toBe('guia-1');
  });

  it('adicionarItem_chamaPOSTNaRotaDeItens', () => {
    const payload: CriarItemGuiaPayload = {
      procedimentoId: 'proc-1',
      posicaoExecutor: 'Cirurgiao',
      viaAcesso: 'Convencional',
      acomodacao: 'Ambulatorial',
      ehUrgencia: false,
      valorApurado: null,
      tempoAnestesicoMin: null,
    };
    let result: GuiaDetalheItem | undefined;
    service.adicionarItem('guia-1', payload).subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/guias/guia-1/itens');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush(GUIA_DETALHE);

    expect(result?.id).toBe('guia-1');
  });

  it('atualizar_chamaPUT', () => {
    const payload: AtualizarGuiaPayload = {
      operadoraId: 'op-2',
      beneficiarioId: 'ben-1',
      numeroGuia: '99999',
      dataAtendimento: '2026-05-02',
      ehPacote: false,
      observacao: 'obs',
      itens: [],
    };
    let result: GuiaDetalheItem | undefined;
    service.atualizar('guia-1', payload).subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/guias/guia-1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(payload);
    req.flush(GUIA_DETALHE);

    expect(result?.id).toBe('guia-1');
  });

  it('excluir_chamaDELETE', () => {
    let called = false;
    service.excluir('guia-1').subscribe(() => (called = true));

    const req = httpMock.expectOne('/api/v1/admin/guias/guia-1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(called).toBe(true);
  });

  it('obterCalculo_chamaCaminhoCorreto', () => {
    let result: GuiaCalculoResult | undefined;
    service.obterCalculo('guia-1').subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/guias/guia-1/calculo');
    expect(req.request.method).toBe('GET');
    req.flush(GUIA_CALCULO_RESULT);

    expect(result?.guiaId).toBe('guia-1');
  });

  it('obterCalculo_respostaPassThrough', () => {
    let result: GuiaCalculoResult | undefined;
    service.obterCalculo('guia-2').subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/guias/guia-2/calculo');
    req.flush(GUIA_CALCULO_RESULT);

    expect(result).toEqual(GUIA_CALCULO_RESULT);
  });

  it('atualizarObservacao_chamaPATCH', () => {
    let result: GuiaItem | undefined;
    service.atualizarObservacao('guia-1', 'nova obs').subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/guias/guia-1/observacao');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ observacao: 'nova obs' });
    req.flush(GUIA_DETALHE);

    expect(result?.id).toBe('guia-1');
  });

  it('atualizarValorApuradoItem_chamaPATCH', () => {
    let result: ItemGuiaItem | undefined;
    service.atualizarValorApuradoItem('guia-1', 'item-1', 250.5).subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/guias/guia-1/itens/item-1/valor-apurado');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ valorApurado: 250.5 });
    req.flush(GUIA_DETALHE.itens[0]);

    expect(result?.id).toBe('item-1');
  });

  it('importarCsv_chamaPOSTComFormDataNaRotaCorreta', () => {
    const RESULTADO: ResultadoImportacaoGuiaDto = {
      identificadorPagamento: 'PAG-001',
      somenteValidar: false,
      guiasCriadas: 1,
      guiasAtualizadas: 0,
      itensCriados: 2,
      itensAtualizados: 0,
      itensIgnorados: 0,
      beneficiariosCriados: 1,
      guiasPrevistas: 1,
      itensPrevistas: 2,
      erros: [],
      alertas: [],
    };

    const arquivo = new File(['csv'], 'test.csv', { type: 'text/csv' });
    let result: ResultadoImportacaoGuiaDto | undefined;
    service.importarCsv(arquivo, 'prest-1', 'op-1', false).subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/guias/importar-csv');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeInstanceOf(FormData);
    req.flush(RESULTADO);

    expect(result?.guiasCriadas).toBe(1);
    expect(result?.identificadorPagamento).toBe('PAG-001');
  });

  it('atualizarPagamentoItem_chamaPATCHNaRotaCorreta', () => {
    let result: ItemGuiaItem | undefined;
    service.atualizarPagamentoItem('guia-1', 'item-1', 100.5, 'CB').subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/guias/guia-1/itens/item-1/pagamento');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ valorLiquidado: 100.5, motivoGlosa: 'CB' });
    req.flush(GUIA_DETALHE.itens[0]);

    expect(result?.id).toBe('item-1');
  });

  it('atualizarPagamentoItem_aceitaValoresNulos', () => {
    let result: ItemGuiaItem | undefined;
    service.atualizarPagamentoItem('guia-1', 'item-1', null, null).subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/guias/guia-1/itens/item-1/pagamento');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ valorLiquidado: null, motivoGlosa: null });
    req.flush(GUIA_DETALHE.itens[0]);

    expect(result?.id).toBe('item-1');
  });
});
