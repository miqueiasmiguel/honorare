import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { RecursoService } from './recurso.service';
import type {
  AdicionarGuiasLoteParams,
  ListarRecursosResult,
  RecursoDetalheDto,
  RecursoDto,
  RecursoForm,
} from './recurso.types';

const RECURSO: RecursoDto = {
  id: 'rec-1',
  operadoraId: 'op-1',
  operadoraNome: 'UNIMED',
  prestadorId: 'prest-1',
  prestadorNome: 'Dr. Fulano',
  prestadorRegistroProfissional: null,
  numero: '202601-001',
  dataEmissao: '2026-01-15',
  observacao: null,
  totalGuias: 3,
  criadoEm: '2026-01-15T00:00:00Z',
};

const DETALHE: RecursoDetalheDto = { header: RECURSO, guias: [] };

const FORM: RecursoForm = {
  operadoraId: 'op-1',
  prestadorId: 'prest-1',
  dataEmissao: '2026-01-15',
  observacao: null,
};

function makeListResult(itens: RecursoDto[] = []): ListarRecursosResult {
  return { itens, total: itens.length, pagina: 1, itensPorPagina: 20 };
}

describe('RecursoService', () => {
  let service: RecursoService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), RecursoService],
    });
    service = TestBed.inject(RecursoService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('listar_chamaCaminhoCorretoComParams', () => {
    let result: ListarRecursosResult | undefined;
    service
      .listar({ pagina: 1, itensPorPagina: 20, operadoraId: 'op-1' })
      .subscribe((v) => (result = v));

    const req = httpMock.expectOne(
      (r) =>
        r.url === '/api/v1/admin/recursos' &&
        r.params.get('pagina') === '1' &&
        r.params.get('operadoraId') === 'op-1',
    );
    expect(req.request.method).toBe('GET');
    req.flush(makeListResult());

    expect(result?.total).toBe(0);
  });

  it('obterPorId_chamaCaminhoCorreto', () => {
    let result: RecursoDetalheDto | undefined;
    service.obterPorId('rec-1').subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/recursos/rec-1');
    expect(req.request.method).toBe('GET');
    req.flush(DETALHE);

    expect(result?.header.id).toBe('rec-1');
  });

  it('criar_chamaPOST', () => {
    let result: RecursoDto | undefined;
    service.criar(FORM).subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/recursos');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(FORM);
    req.flush(RECURSO);

    expect(result?.id).toBe('rec-1');
  });

  it('atualizar_chamaPUT', () => {
    let result: RecursoDto | undefined;
    service.atualizar('rec-1', FORM).subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/recursos/rec-1');
    expect(req.request.method).toBe('PUT');
    req.flush(RECURSO);

    expect(result?.id).toBe('rec-1');
  });

  it('excluir_chamaDELETE', () => {
    let called = false;
    service.excluir('rec-1').subscribe(() => (called = true));

    const req = httpMock.expectOne('/api/v1/admin/recursos/rec-1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(called).toBe(true);
  });

  it('adicionarGuia_enviaUrlComGuiaIdNaPath', () => {
    let called = false;
    service.adicionarGuia('rec-1', 'guia-99').subscribe(() => (called = true));

    const req = httpMock.expectOne('/api/v1/admin/recursos/rec-1/guias/guia-99');
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(called).toBe(true);
  });

  it('adicionarGuiasLote_enviaFiltrosNoBody', () => {
    const filtros: AdicionarGuiasLoteParams = {
      prestadorId: 'p1',
      operadoraId: 'op1',
      dataInicio: '2026-03-01',
      dataFim: '2026-03-31',
      somenteComGlosa: true,
    };
    service.adicionarGuiasLote('rec-1', filtros).subscribe();

    const req = httpMock.expectOne('/api/v1/admin/recursos/rec-1/guias/lote');
    expect(req.request.method).toBe('POST');
    const body = req.request.body as AdicionarGuiasLoteParams;
    expect(body.prestadorId).toBe('p1');
    expect(body.somenteComGlosa).toBe(true);
    req.flush({ adicionadas: 5 });
  });

  it('adicionarGuiasLote_retornaAdicionadas', () => {
    let result: { adicionadas: number } | undefined;
    const filtros: AdicionarGuiasLoteParams = { prestadorId: 'p1', operadoraId: 'op1' };
    service.adicionarGuiasLote('rec-1', filtros).subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/recursos/rec-1/guias/lote');
    req.flush({ adicionadas: 5 });

    expect(result?.adicionadas).toBe(5);
  });

  it('removerGuia_chamaDELETE', () => {
    let called = false;
    service.removerGuia('rec-1', 'guia-1').subscribe(() => (called = true));

    const req = httpMock.expectOne('/api/v1/admin/recursos/rec-1/guias/guia-1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(called).toBe(true);
  });
});
