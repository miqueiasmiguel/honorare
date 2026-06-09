import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TenantService } from './tenant.service';
import type { TenantSettings } from './tenant.types';

const SETTINGS: TenantSettings = {
  id: 'tenant-1',
  name: 'Clínica Alpha',
  hasLogo: false,
};

describe('TenantService', () => {
  let service: TenantService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), TenantService],
    });
    service = TestBed.inject(TenantService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('getSettings_chamaCaminhoCorreto', () => {
    let result: TenantSettings | undefined;
    service.getSettings().subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/tenant');
    expect(req.request.method).toBe('GET');
    req.flush(SETTINGS);

    expect(result?.name).toBe('Clínica Alpha');
    expect(result?.hasLogo).toBe(false);
  });

  it('rename_enviaPatchComName', () => {
    let result: TenantSettings | undefined;
    service.rename('Nova Clínica').subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/tenant');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ name: 'Nova Clínica' });
    req.flush({ ...SETTINGS, name: 'Nova Clínica' });

    expect(result?.name).toBe('Nova Clínica');
  });

  it('uploadLogo_enviaFormDataComArquivo', () => {
    const file = new File(['img'], 'logo.png', { type: 'image/png' });
    let result: TenantSettings | undefined;
    service.uploadLogo(file).subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/v1/admin/tenant/logo');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeInstanceOf(FormData);
    req.flush({ ...SETTINGS, hasLogo: true });

    expect(result?.hasLogo).toBe(true);
  });

  it('deleteLogo_enviaDELETE', () => {
    let completed = false;
    service.deleteLogo().subscribe({ complete: () => (completed = true) });

    const req = httpMock.expectOne('/api/v1/admin/tenant/logo');
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(completed).toBe(true);
  });
});
