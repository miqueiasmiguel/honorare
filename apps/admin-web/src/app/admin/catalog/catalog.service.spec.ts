import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CatalogService } from './catalog.service';
import type {
  BeneficiarioItem,
  ImportarTabelaPorteResult,
  ListarBeneficiariosResult,
  LookupOrCreateResult,
  TabelaPorteAnestesicoItem,
} from './catalog.types';

const BENEFICIARIO: BeneficiarioItem = {
  id: 'ben-1',
  carteira: '0001234567',
  nome: 'JOÃO SILVA',
  criadoEm: '2026-01-01T00:00:00Z',
};

function makeListResult(itens: BeneficiarioItem[] = [BENEFICIARIO]): ListarBeneficiariosResult {
  return { itens, total: itens.length, pagina: 1, itensPorPagina: 20 };
}

describe('CatalogService — Beneficiários', () => {
  let service: CatalogService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), CatalogService],
    });
    service = TestBed.inject(CatalogService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('listarBeneficiarios_chamaCaminhoCorreto', () => {
    let result: ListarBeneficiariosResult | undefined;
    service.listarBeneficiarios({ pagina: 1, itensPorPagina: 20 }).subscribe((v) => (result = v));

    const req = httpMock.expectOne(
      (r) =>
        r.url === '/api/v1/admin/beneficiarios' &&
        r.params.get('pagina') === '1' &&
        r.params.get('itensPorPagina') === '20',
    );
    expect(req.request.method).toBe('GET');
    req.flush(makeListResult());

    expect(result?.itens).toHaveLength(1);
    expect(result?.itens[0].carteira).toBe('0001234567');
  });

  it('lookupOrCreateBeneficiario_enviaCamposNormalizados', () => {
    const expected: LookupOrCreateResult = { ...BENEFICIARIO, criado: true };
    let result: LookupOrCreateResult | undefined;
    service.lookupOrCreateBeneficiario('0001234567', 'João Silva').subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/beneficiarios/lookup-or-create');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ carteira: '0001234567', nome: 'João Silva' });
    req.flush(expected);

    expect(result?.criado).toBe(true);
  });

  it('atualizarBeneficiario_enviaPUT', () => {
    let result: BeneficiarioItem | undefined;
    service
      .atualizarBeneficiario('ben-1', { nome: 'João da Silva' })
      .subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/beneficiarios/ben-1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ nome: 'João da Silva' });
    req.flush({ ...BENEFICIARIO, nome: 'JOÃO DA SILVA' });

    expect(result?.nome).toBe('JOÃO DA SILVA');
  });

  it('excluirBeneficiario_enviaDELETE', () => {
    let completed = false;
    service.excluirBeneficiario('ben-1').subscribe({ complete: () => (completed = true) });

    const req = httpMock.expectOne('/api/v1/admin/beneficiarios/ben-1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);

    expect(completed).toBe(true);
  });
});

const PORTE_ANESTESICO: TabelaPorteAnestesicoItem = {
  id: 'porte-1',
  porteletra: 'J',
  valorEnfermaria: 526.5,
  valorApartamento: 842.4,
  valorAmbulatorial: null,
  atualizadoEm: '2026-01-01T00:00:00Z',
};

const IMPORT_PORTE_RESULT: ImportarTabelaPorteResult = {
  portesAtualizados: 2,
  procedimentosAtualizados: 2,
  procedimentosNaoEncontrados: [],
  erros: [],
};

describe('CatalogService — TabelaPorteAnestesico', () => {
  let service: CatalogService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), CatalogService],
    });
    service = TestBed.inject(CatalogService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('listarPortesAnestesico_chamaCaminhoCorretoComOperadoraId', () => {
    let result: TabelaPorteAnestesicoItem[] | undefined;
    service.listarPortesAnestesico('op-123').subscribe((v) => (result = v));

    const req = httpMock.expectOne(
      (r) =>
        r.url === '/api/v1/admin/tabelas-porte-anestesico' &&
        r.params.get('operadoraId') === 'op-123',
    );
    expect(req.request.method).toBe('GET');
    req.flush([PORTE_ANESTESICO]);

    expect(result).toHaveLength(1);
    expect(result?.[0].porteletra).toBe('J');
    expect(result?.[0].valorEnfermaria).toBe(526.5);
  });

  it('importarTabelaPorteAnestesico_enviaFormDataComOperadoraId', () => {
    const file = new File(['csv content'], 'tabela.csv', { type: 'text/csv' });
    let result: ImportarTabelaPorteResult | undefined;
    service.importarTabelaPorteAnestesico('op-123', file).subscribe((v) => (result = v));

    const req = httpMock.expectOne(
      (r) =>
        r.url === '/api/v1/admin/tabelas-porte-anestesico/importar-unimed-csv' &&
        r.params.get('operadoraId') === 'op-123',
    );
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeInstanceOf(FormData);
    req.flush(IMPORT_PORTE_RESULT);

    expect(result?.portesAtualizados).toBe(2);
    expect(result?.procedimentosAtualizados).toBe(2);
    expect(result?.procedimentosNaoEncontrados).toHaveLength(0);
  });

  it('importarTabelaPorteAnestesico_resultadoComNaoEncontrados', () => {
    const file = new File(['csv content'], 'tabela.csv', { type: 'text/csv' });
    let result: ImportarTabelaPorteResult | undefined;
    service.importarTabelaPorteAnestesico('op-123', file).subscribe((v) => (result = v));

    const req = httpMock.expectOne(
      (r) => r.url === '/api/v1/admin/tabelas-porte-anestesico/importar-unimed-csv',
    );
    req.flush({ ...IMPORT_PORTE_RESULT, procedimentosNaoEncontrados: ['99999999'] });

    expect(result?.procedimentosNaoEncontrados).toContain('99999999');
  });
});
