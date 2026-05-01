import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { TenantDetail } from './tenant-detail';
import type { TenantSummary, UserSummary } from '../saas.types';

const TENANT_ATIVO: TenantSummary = {
  id: 'tenant-1',
  name: 'Clínica Alfa',
  status: 'Ativo',
  createdAt: '2024-01-15T00:00:00Z',
  totalAdmins: 2,
  totalMedicos: 5,
};

const TENANT_CANCELADO: TenantSummary = {
  id: 'tenant-1',
  name: 'Clínica Alfa',
  status: 'Cancelado',
  createdAt: '2024-01-15T00:00:00Z',
  totalAdmins: 2,
  totalMedicos: 5,
};

const USERS: UserSummary[] = [
  {
    id: 'user-1',
    email: 'admin@alfa.com',
    role: 'TenantAdmin',
    isActive: true,
    createdAt: '2024-01-15T00:00:00Z',
    medicoId: null,
  },
  {
    id: 'user-2',
    email: 'medico@alfa.com',
    role: 'Medico',
    isActive: false,
    createdAt: '2024-02-01T00:00:00Z',
    medicoId: 'med-1',
  },
];

const MOCK_ROUTE = {
  snapshot: {
    paramMap: {
      get: (): string | null => 'tenant-1',
    },
  },
};

describe('TenantDetail', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TenantDetail],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: MOCK_ROUTE },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  function createAndFlush(tenant: TenantSummary = TENANT_ATIVO, users: UserSummary[] = USERS) {
    const fixture = TestBed.createComponent(TenantDetail);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/saas/tenants').flush([tenant]);
    httpMock.expectOne('/api/v1/saas/tenants/tenant-1/users').flush(users);
    fixture.detectChanges();
    return fixture;
  }

  it('TenantDetail_RendersNameAndStatus', () => {
    const fixture = createAndFlush();
    const el = fixture.nativeElement as HTMLElement;
    const header = el.querySelector('.tenant-detail__header');
    const text = header?.textContent ?? '';
    expect(text).toContain('Clínica Alfa');
    expect(text).toContain('Ativo');
  });

  it('TenantDetail_ShowsSuspenderButtonWhenAtivo', () => {
    const fixture = createAndFlush(TENANT_ATIVO);
    const el = fixture.nativeElement as HTMLElement;
    const btn = el.querySelector('[data-testid="btn-suspender"]');
    expect(btn).toBeTruthy();
  });

  it('TenantDetail_HidesAcoesWhenCancelado', () => {
    const fixture = createAndFlush(TENANT_CANCELADO);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="btn-suspender"]')).toBeNull();
    expect(el.querySelector('[data-testid="btn-reativar"]')).toBeNull();
    expect(el.querySelector('[data-testid="btn-cancelar"]')).toBeNull();
  });

  it('TenantDetail_ToggleUserStatus_CallsUpdateUserStatus', () => {
    const fixture = createAndFlush();
    const el = fixture.nativeElement as HTMLElement;

    const toggleBtn = el.querySelector<HTMLButtonElement>('.tenant-detail__toggle');
    toggleBtn?.click();
    fixture.detectChanges();

    const req = httpMock.expectOne('/api/v1/saas/tenants/tenant-1/users/user-1/status');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ isActive: false });
    req.flush(null);

    httpMock.expectOne('/api/v1/saas/tenants/tenant-1/users').flush(USERS);
    fixture.detectChanges();
  });

  it('TenantDetail_AddUser_SubmitCallsCreateUser', () => {
    const fixture = createAndFlush();

    fixture.componentInstance.openModal();
    fixture.componentInstance.form.setValue({
      email: 'novo@alfa.com',
      role: 'TenantAdmin',
      medicoId: '',
    });
    fixture.detectChanges();

    fixture.componentInstance.submitAddUser();

    const req = httpMock.expectOne('/api/v1/saas/tenants/tenant-1/users');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'novo@alfa.com', role: 'TenantAdmin' });
    req.flush(USERS[0]);
    fixture.detectChanges();

    expect(fixture.componentInstance.showModal()).toBe(false);

    httpMock.expectOne('/api/v1/saas/tenants/tenant-1/users').flush(USERS);
    fixture.detectChanges();
  });

  it('TenantDetail_AddUser_MedicoIdFieldAppearsOnlyWhenRoleIsMedico', () => {
    const fixture = createAndFlush();

    fixture.componentInstance.openModal();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('#medicoId')).toBeNull();

    fixture.componentInstance.form.controls.role.setValue('Medico');
    fixture.detectChanges();

    expect(el.querySelector('#medicoId')).toBeTruthy();
  });
});
