import { TestBed } from '@angular/core/testing';
import { TenantDetail } from './tenant-detail';

describe('TenantDetail (stub)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [TenantDetail] });
  });

  it('should create the component', () => {
    const fixture = TestBed.createComponent(TenantDetail);
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
  });
});
