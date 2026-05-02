import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { UserList } from './user-list';
import { AdminService } from '../admin.service';
import type { AdminUserSummary } from '../admin.types';

const mockUsers: AdminUserSummary[] = [
  {
    id: '1',
    email: 'admin@test.com',
    nome: 'Dr. Admin',
    role: 'TenantAdmin',
    isActive: true,
    createdAt: '2025-01-15T00:00:00Z',
    medicoId: null,
  },
  {
    id: '2',
    email: 'medico@test.com',
    nome: null,
    role: 'Medico',
    isActive: false,
    createdAt: '2025-02-20T00:00:00Z',
    medicoId: 'med-1',
  },
];

function setup(users: AdminUserSummary[] = mockUsers) {
  const adminServiceSpy = {
    listUsers: vi.fn().mockReturnValue(of(users)),
    updateUserStatus: vi.fn().mockReturnValue(of(undefined)),
  };

  TestBed.configureTestingModule({
    imports: [UserList],
    providers: [{ provide: AdminService, useValue: adminServiceSpy }],
  });

  const fixture = TestBed.createComponent(UserList);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, adminService: adminServiceSpy };
}

describe('UserList', () => {
  it('carrega usuários na inicialização', () => {
    const { component, adminService } = setup();
    expect(adminService.listUsers).toHaveBeenCalledOnce();
    expect(component.users()).toHaveLength(2);
  });

  it('exibe nome quando disponível', () => {
    const { fixture } = setup();
    const el = fixture.nativeElement as HTMLElement;
    const nome = el.querySelector('.user-list__row:nth-child(1) .user-list__nome');
    const text = nome?.textContent ?? '';
    expect(text.trim()).toBe('Dr. Admin');
  });

  it('exibe email quando nome é null', () => {
    const { fixture } = setup();
    const el = fixture.nativeElement as HTMLElement;
    const nome = el.querySelector('.user-list__row:nth-child(2) .user-list__nome');
    const text = nome?.textContent ?? '';
    expect(text.trim()).toBe('medico@test.com');
  });

  it('exibe mensagem de lista vazia quando não há usuários', () => {
    const { fixture } = setup([]);
    const el = fixture.nativeElement as HTMLElement;
    const empty = el.querySelector('.user-list__empty');
    const text = empty?.textContent ?? '';
    expect(text.trim()).toBe('Nenhum usuário encontrado.');
  });

  it('toggleStatus chama updateUserStatus com valor invertido e recarrega lista', () => {
    const { component, adminService } = setup();
    const user = component.users()[0];
    component.toggleStatus(user);
    expect(adminService.updateUserStatus).toHaveBeenCalledWith('1', false);
    expect(adminService.listUsers).toHaveBeenCalledTimes(2);
  });

  it('formatDate formata data no padrão DD/MM/AAAA', () => {
    const { component } = setup();
    expect(component.formatDate('2025-01-15T00:00:00Z')).toBe('15/01/2025');
  });

  it('displayNome retorna nome quando presente', () => {
    const { component } = setup();
    expect(component.displayNome(mockUsers[0])).toBe('Dr. Admin');
  });

  it('displayNome retorna email quando nome é null', () => {
    const { component } = setup();
    expect(component.displayNome(mockUsers[1])).toBe('medico@test.com');
  });
});
