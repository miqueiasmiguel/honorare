import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SaasService } from './saas.service';
import type { TenantSummary, TenantWithOwnerSummary } from './saas.types';

const TENANT_SUMMARY: TenantSummary = {
  id: 'tid-1',
  name: 'Acme',
  status: 'Ativo',
  createdAt: '2024-01-01T00:00:00Z',
  totalAdmins: 2,
  totalMedicos: 5,
};

const TENANT_WITH_OWNER: TenantWithOwnerSummary = {
  tenantId: 'tid-1',
  tenantName: 'Acme',
  status: 'Ativo',
  createdAt: '2024-01-01T00:00:00Z',
  ownerId: 'oid-1',
  ownerEmail: 'owner@acme.com',
};

describe('SaasService', () => {
  let service: SaasService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), SaasService],
    });
    service = TestBed.inject(SaasService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('listTenants_CallsCorrectEndpoint', () => {
    let result: TenantSummary[] | undefined;
    service.listTenants().subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/saas/tenants');
    expect(req.request.method).toBe('GET');
    req.flush([TENANT_SUMMARY]);

    expect(result).toEqual([TENANT_SUMMARY]);
  });

  it('createTenant_PostsPayloadAndReturnsCreated', () => {
    const payload = { tenantName: 'Acme', ownerEmail: 'owner@acme.com' };
    let created: TenantWithOwnerSummary | undefined;
    service.createTenant(payload).subscribe((v) => (created = v));

    const req = httpMock.expectOne('/api/v1/saas/tenants');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush(TENANT_WITH_OWNER);

    expect(created).toEqual(TENANT_WITH_OWNER);
  });

  it('updateTenantStatus_PatchesCorrectEndpoint', () => {
    let result: TenantSummary | undefined;
    service.updateTenantStatus('tid-1', 'Suspenso').subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/saas/tenants/tid-1/status');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ status: 'Suspenso' });
    req.flush({ ...TENANT_SUMMARY, status: 'Suspenso' });

    expect(result?.status).toBe('Suspenso');
  });

  it('listTenantUsers_CallsCorrectEndpoint', () => {
    service.listTenantUsers('tid-1').subscribe();

    const req = httpMock.expectOne('/api/v1/saas/tenants/tid-1/users');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('createUser_PostsToCorrectEndpoint', () => {
    const payload = { email: 'admin@acme.com', role: 'TenantAdmin' as const };
    service.createUser('tid-1', payload).subscribe();

    const req = httpMock.expectOne('/api/v1/saas/tenants/tid-1/users');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({
      id: 'uid-1',
      ...payload,
      isActive: true,
      createdAt: '2024-01-01T00:00:00Z',
      medicoId: null,
    });
  });

  it('updateUserStatus_PatchesCorrectEndpoint', () => {
    service.updateUserStatus('tid-1', 'uid-1', false).subscribe();

    const req = httpMock.expectOne('/api/v1/saas/tenants/tid-1/users/uid-1/status');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ isActive: false });
    req.flush(null);
  });
});
