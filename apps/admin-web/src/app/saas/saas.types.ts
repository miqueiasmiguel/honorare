export type TenantStatus = 'Ativo' | 'Suspenso' | 'Cancelado';

export interface TenantSummary {
  id: string;
  name: string;
  status: TenantStatus;
  createdAt: string;
  totalAdmins: number;
  totalMedicos: number;
}

export interface TenantWithOwnerSummary {
  tenantId: string;
  tenantName: string;
  status: TenantStatus;
  createdAt: string;
  ownerId: string;
  ownerEmail: string;
}

export interface CreateTenantPayload {
  tenantName: string;
  ownerEmail: string;
}

export type UserRole = 'TenantAdmin' | 'Medico';

export interface UserSummary {
  id: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
  medicoId: string | null;
}

export interface CreateUserPayload {
  email: string;
  role: UserRole;
  medicoId?: string;
}
