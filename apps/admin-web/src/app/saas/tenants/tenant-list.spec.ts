import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { TenantList } from './tenant-list';
import type { TenantSummary, TenantWithOwnerSummary } from '../saas.types';

const TENANTS: TenantSummary[] = [
  {
    id: '1',
    name: 'Tenant Ativo',
    status: 'Ativo',
    createdAt: '2024-01-15T00:00:00Z',
    totalAdmins: 2,
    totalMedicos: 5,
  },
  {
    id: '2',
    name: 'Tenant Suspenso',
    status: 'Suspenso',
    createdAt: '2024-02-20T00:00:00Z',
    totalAdmins: 1,
    totalMedicos: 3,
  },
];

const NEW_TENANT: TenantWithOwnerSummary = {
  tenantId: '3',
  tenantName: 'Novo Tenant',
  status: 'Ativo',
  createdAt: '2024-03-01T00:00:00Z',
  ownerId: 'oid-3',
  ownerEmail: 'novo@tenant.com',
};

describe('TenantList', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TenantList],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  function createAndFlush(tenants: TenantSummary[] = TENANTS) {
    const fixture = TestBed.createComponent(TenantList);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/saas/tenants').flush(tenants);
    fixture.detectChanges();
    return fixture;
  }

  it('TenantList_RendersCardWithTotalAtivos', () => {
    const fixture = createAndFlush();
    const el = fixture.nativeElement as HTMLElement;
    const card = el.querySelector('[data-testid="card-ativos"]');
    const text = card?.textContent ?? '';
    // TENANTS has 1 ativo
    expect(text).toContain('1');
  });

  it('TenantList_RendersTenantRowsFromService', () => {
    const fixture = createAndFlush();
    const el = fixture.nativeElement as HTMLElement;
    const rows = el.querySelectorAll('.tenant-list__row');
    expect(rows.length).toBe(TENANTS.length);
    const firstRow = el.querySelector('.tenant-list__row');
    const firstRowText = firstRow?.textContent ?? '';
    expect(firstRowText).toContain('Tenant Ativo');
  });

  it('TenantList_OpenModalOnClickNovo', () => {
    const fixture = createAndFlush();
    const el = fixture.nativeElement as HTMLElement;

    expect(fixture.componentInstance.showModal()).toBe(false);
    expect(el.querySelector('.tenant-list__modal')).toBeNull();

    const btn = el.querySelector<HTMLButtonElement>('.tenant-list__btn-novo');
    btn?.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.showModal()).toBe(true);
    expect(el.querySelector('.tenant-list__modal')).toBeTruthy();
  });

  it('TenantList_SubmitFormCallsCreateTenantAndRefreshesTable', () => {
    const fixture = createAndFlush();

    // Abre modal e preenche o formulário
    fixture.componentInstance.openModal();
    fixture.componentInstance.form.setValue({
      tenantName: 'Novo Tenant',
      ownerEmail: 'novo@tenant.com',
    });
    fixture.detectChanges();

    // Dispara submit
    fixture.componentInstance.submitCreate();

    // Deve emitir POST para /api/v1/saas/tenants
    const postReq = httpMock.expectOne('/api/v1/saas/tenants');
    expect(postReq.request.method).toBe('POST');
    expect(postReq.request.body).toEqual({
      tenantName: 'Novo Tenant',
      ownerEmail: 'novo@tenant.com',
    });
    postReq.flush(NEW_TENANT);
    fixture.detectChanges();

    // Modal deve fechar
    expect(fixture.componentInstance.showModal()).toBe(false);

    // Deve refrescar a tabela (novo GET)
    const refreshReq = httpMock.expectOne('/api/v1/saas/tenants');
    refreshReq.flush(TENANTS);
    fixture.detectChanges();

    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('.tenant-list__row');
    expect(rows.length).toBe(TENANTS.length);
  });

  it('TenantList_ShowsValidationErrorWhenEmailInvalid', () => {
    const fixture = createAndFlush([]);

    // Abre modal
    fixture.componentInstance.openModal();
    fixture.detectChanges();

    // Define e-mail inválido e marca como tocado
    fixture.componentInstance.form.controls.ownerEmail.setValue('not-an-email');
    fixture.componentInstance.form.controls.ownerEmail.markAsTouched();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const error = el.querySelector('.tenant-list__error--email');
    expect(error).toBeTruthy();
  });
});
