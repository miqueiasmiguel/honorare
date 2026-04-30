import { TestBed } from '@angular/core/testing';
import { TenantList } from './tenant-list';

describe('TenantList (stub)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [TenantList] });
  });

  it('should create the component', () => {
    const fixture = TestBed.createComponent(TenantList);
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
  });
});
